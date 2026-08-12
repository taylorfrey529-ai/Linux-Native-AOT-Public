using System.Text.Json;
using Evermore.Cli;
using Evermore.Solune.Core;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class SoluneEngineTests
{
    [Fact]
    public void ProcessorIsByteDeterministicForTheSameInput()
    {
        var processor = new DeterministicSoluneProcessor();

        string first = SoluneSnapshotSerializer.Serialize(processor.Process(CreateValidInput()));
        string second = SoluneSnapshotSerializer.Serialize(processor.Process(CreateValidInput()));

        Assert.Equal(first, second);
    }

    [Fact]
    public void SuppliedFeasibilityPlateEquationAnchorsAreExact()
    {
        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(CreateValidInput());

        Assert.Equal(260_893, snapshot.Orbit.CalculatedPeriodTenThousandthDays);
        Assert.Equal(12_293, snapshot.Orbit.ImpliedMassRatioMicros);
        Assert.Equal(2_382_441_248L, snapshot.Radiance.CalculatedBlackbodyLuminosityTerawatts);
        Assert.Equal(185, snapshot.Radiance.LuminosityDeviationMicros);
        Assert.Equal(1_361_780L, snapshot.Radiance.IrradianceMilliwattsPerSquareMeter);
        Assert.Equal(533_813, snapshot.Radiance.CalculatedApparentDiameterMicrodegrees);
        Assert.Equal(13, snapshot.Radiance.ApparentDiameterDeviationMicrodegrees);
        Assert.Equal(1_093_114L, snapshot.Radiance.CalculatedTidalForceRatioMicros);
        Assert.Equal(886L, snapshot.Radiance.TidalForceDeviationMicros);
        Assert.Equal(23_999_955L, snapshot.Radiance.CalculatedLocalSolarDayMicrohours);
        Assert.Equal(45L, snapshot.Radiance.SolarDayDeviationMicrohours);
    }

    [Fact]
    public void EvercyclePartitionsEverturnEverwakeAndStillstarExactly()
    {
        var processor = new DeterministicSoluneProcessor();
        SoluneSnapshot everturn = processor.Process(CreateValidInput() with { EvercycleStepMicrodays = 0 });
        SoluneSnapshot everwake = processor.Process(CreateValidInput() with
        {
            EvercycleStepMicrodays = 364 * SoluneBounds.Unit
        });
        SoluneSnapshot stillstar = processor.Process(CreateValidInput() with
        {
            EvercycleStepMicrodays = 365 * SoluneBounds.Unit
        });

        Assert.Equal(EvercyclePhase.Everturn, everturn.Calendar.Phase);
        Assert.Equal(1, everturn.Calendar.EverturnNumber);
        Assert.Equal(1, everturn.Calendar.DayWithinEverturn);
        Assert.Equal(EvercyclePhase.Everwake, everwake.Calendar.Phase);
        Assert.Equal(EvercyclePhase.Stillstar, stillstar.Calendar.Phase);
        Assert.Equal(SoluneBounds.EvercycleLengthMicrodays, stillstar.Calendar.EvercycleLengthMicrodays);
    }

    [Fact]
    public void ClearedModelReturnsOnlyAPureMaintenanceDirective()
    {
        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(CreateValidInput());

        Assert.Equal(SoluneDisposition.Completed, snapshot.Disposition);
        Assert.Null(snapshot.ContainmentCode);
        SoluneOperationDirective directive = Assert.Single(snapshot.OperationDirectives);
        Assert.Equal("MAINTAIN_CONTAINED_STELLAR_CONSTRUCT", directive.Code);
        Assert.Equal("construct.solune.01", directive.ConstructId);
        Assert.Equal(42, directive.Generation);
        Assert.Equal(1_361_780L, directive.TargetIrradianceMilliwattsPerSquareMeter);
        Assert.Equal("local-construct+equations+continuity+containment-cleared", directive.Authority);
    }

    [Fact]
    public void LocalAuthorityAndIntegrityAreMandatoryBeforeDirectiveBranching()
    {
        SoluneInput input = CreateValidInput() with { History = [] };
        SoluneSnapshot unauthorized = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { LocalConstructAuthorized = false }
        });
        SoluneSnapshot lowIntegrity = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { ContainmentIntegrityMicros = 900_000 }
        });

        Assert.Equal("LOCAL_CONSTRUCT_AUTHORITY_REQUIRED", unauthorized.ContainmentCode);
        Assert.Empty(unauthorized.OperationDirectives);
        Assert.Equal("CONTAINMENT_INTEGRITY_LOW", lowIntegrity.ContainmentCode);
        Assert.Empty(lowIntegrity.OperationDirectives);
    }

    [Fact]
    public void IrradianceOutsideTheLocalSunEnvelopeContains()
    {
        SoluneInput input = CreateValidInput() with { History = [] };
        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { OrbitRadiusKm = 340_000 }
        });

        Assert.Equal(SoluneDisposition.Contained, snapshot.Disposition);
        Assert.Equal("IRRADIANCE_ENVELOPE_EXCEEDED", snapshot.ContainmentCode);
        Assert.Empty(snapshot.OperationDirectives);
    }

    [Fact]
    public void BarycenterAndOrbitGeometryFailClosed()
    {
        SoluneInput input = CreateValidInput() with { History = [] };
        SoluneSnapshot barycenter = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { BarycenterKm = 7_000 }
        });
        SoluneSnapshot geometry = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { OrbitEccentricityMicros = 20_000 }
        });

        Assert.Equal("BARYCENTER_OUTSIDE_EVERMORE", barycenter.ContainmentCode);
        Assert.Equal("ORBIT_GEOMETRY_OUTSIDE_CONTAINMENT", geometry.ContainmentCode);
        Assert.Empty(barycenter.OperationDirectives);
        Assert.Empty(geometry.OperationDirectives);
    }

    [Fact]
    public void TidalAndSolarDayClaimsMustMatchTheirEquations()
    {
        SoluneInput input = CreateValidInput() with { History = [] };
        SoluneSnapshot tidal = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { TidalForceRatioMicros = 1_200_000 }
        });
        SoluneSnapshot solarDay = new DeterministicSoluneProcessor().Process(input with
        {
            Model = input.Model with { LocalSolarDayMicrohours = 25_000_000 }
        });

        Assert.Equal("TIDAL_FORCE_MODEL_MISMATCH", tidal.ContainmentCode);
        Assert.Equal("SOLAR_DAY_MODEL_MISMATCH", solarDay.ContainmentCode);
    }

    [Fact]
    public void ContinuityIdentityDriftContainsTheWholeEvaluation()
    {
        SoluneInput input = CreateValidInput();
        input = input with
        {
            History =
            [
                input.History[0] with
                {
                    ContinuitySignature = "SOLUNE-HEARTHSTAR-ALTERED"
                }
            ]
        };

        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(input);

        Assert.Equal("CONTINUITY_IDENTITY_DRIFT", snapshot.ContainmentCode);
        Assert.Empty(snapshot.OperationDirectives);
    }

    [Fact]
    public void ExcessiveHistoricalStateDriftContainsTheWholeEvaluation()
    {
        SoluneInput input = CreateValidInput();
        input = input with
        {
            History = [input.History[0] with { OrbitRadiusKm = 372_000 }]
        };

        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(input);

        Assert.Equal("HISTORY_STATE_DRIFT", snapshot.ContainmentCode);
        Assert.Empty(snapshot.OperationDirectives);
    }

    [Fact]
    public void GenerationHistoryAndCalendarBoundsAreFinite()
    {
        SoluneHistoryPoint[] excessiveHistory = Enumerable.Range(0, SoluneBounds.MaximumHistoryPoints + 1)
            .Select(generation => new SoluneHistoryPoint(
                generation,
                "construct.solune.01",
                "SOLUNE-HEARTHSTAR-01",
                373_089,
                2_382_000_000,
                5_768,
                SoluneBounds.Unit))
            .ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeterministicSoluneProcessor().Process(CreateValidInput() with
            {
                Generation = 200,
                History = excessiveHistory
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeterministicSoluneProcessor().Process(CreateValidInput() with
            {
                Generation = SoluneBounds.MaximumGeneration + 1,
                History = []
            }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeterministicSoluneProcessor().Process(CreateValidInput() with
            {
                EvercycleStepMicrodays = SoluneBounds.EvercycleLengthMicrodays
            }));
    }

    [Fact]
    public void SnapshotEnvelopeRoundTripsAndRejectsPayloadTampering()
    {
        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(CreateValidInput());
        string serialized = SoluneSnapshotSerializer.Serialize(snapshot);

        SoluneSnapshotEnvelope verified = SoluneSnapshotSerializer.DeserializeAndVerify(serialized);
        Assert.Equal(SoluneSnapshotSerializer.SchemaVersion, verified.SchemaVersion);
        Assert.Equal(SoluneSnapshotSerializer.Authority, verified.Authority);
        Assert.Equal(snapshot.Generation, verified.Payload.Generation);

        string tampered = serialized.Replace(
            "\"irradianceMilliwattsPerSquareMeter\":1361780",
            "\"irradianceMilliwattsPerSquareMeter\":1361779",
            StringComparison.Ordinal);
        Assert.NotEqual(serialized, tampered);
        Assert.Throws<InvalidDataException>(() => SoluneSnapshotSerializer.DeserializeAndVerify(tampered));
    }

    [Fact]
    public void RehashedSemanticDirectiveOrEquationClaimsAreRejected()
    {
        SoluneSnapshot snapshot = new DeterministicSoluneProcessor().Process(CreateValidInput());
        SoluneSnapshot equationTampered = snapshot with
        {
            Radiance = snapshot.Radiance with
            {
                CalculatedApparentDiameterMicrodegrees =
                    snapshot.Radiance.CalculatedApparentDiameterMicrodegrees - 1
            }
        };
        SoluneOperationDirective directive = Assert.Single(snapshot.OperationDirectives);
        SoluneSnapshot authorityTampered = snapshot with
        {
            OperationDirectives = [directive with { Authority = "remote-authority" }]
        };

        Assert.Throws<InvalidDataException>(() => SoluneSnapshotSerializer.Serialize(equationTampered));
        Assert.Throws<InvalidDataException>(() => SoluneSnapshotSerializer.Serialize(authorityTampered));
    }

    [Fact]
    public async Task CliRunsValidSoluneFixtureWithStableEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["solune", "run", "--input", Fixture("valid-solune.json"), "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(SoluneSnapshotSerializer.SchemaVersion, root.GetProperty("schemaVersion").GetString());
        Assert.Equal(SoluneSnapshotSerializer.Authority, root.GetProperty("authority").GetString());
        JsonElement payload = root.GetProperty("payload");
        Assert.Equal("Completed", payload.GetProperty("disposition").GetString());
        Assert.Equal(1_361_780L, payload.GetProperty("radiance").GetProperty("irradianceMilliwattsPerSquareMeter").GetInt64());
        Assert.Equal(1, payload.GetProperty("operationDirectives").GetArrayLength());
    }

    [Fact]
    public async Task CliContainsLowIntegrityAndRejectsOversizedInput()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int containedExit = await EvermoreCliApp.InvokeAsync(
            ["solune", "run", "--input", Fixture("low-containment-solune.json"), "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.SoluneContainmentExitCode, containedExit);
        Assert.Contains("SOLUNE_CONTAINMENT: CONTAINMENT_INTEGRITY_LOW", error.ToString());
        using (var document = JsonDocument.Parse(output.ToString()))
        {
            JsonElement payload = document.RootElement.GetProperty("payload");
            Assert.Equal("Contained", payload.GetProperty("disposition").GetString());
            Assert.Equal(0, payload.GetProperty("operationDirectives").GetArrayLength());
        }

        string path = Path.GetTempFileName();
        try
        {
            using (FileStream stream = File.OpenWrite(path))
                stream.SetLength((long)SoluneBounds.MaximumCliInputBytes + 1);
            var oversizedError = new StringWriter();

            int oversizedExit = await EvermoreCliApp.InvokeAsync(
                ["solune", "run", "--input", path],
                new StringWriter(),
                oversizedError);

            Assert.Equal(EvermoreCliApp.SoluneInputFailureExitCode, oversizedExit);
            Assert.Contains("SOLUNE_INPUT_FAILURE", oversizedError.ToString());
            Assert.Contains("cannot exceed", oversizedError.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SoluneInput CreateValidInput() =>
        new(
            42,
            0,
            SoluneReferenceModel.Create(),
            [
                new SoluneHistoryPoint(
                    41,
                    "construct.solune.01",
                    "SOLUNE-HEARTHSTAR-01",
                    373_089,
                    2_382_000_000,
                    5_768,
                    SoluneBounds.Unit)
            ]);

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "CliFixtures", name);
}
