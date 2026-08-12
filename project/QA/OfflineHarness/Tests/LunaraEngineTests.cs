using System.Text.Json;
using Evermore.Cli;
using Evermore.Lunara.Core;
using Xunit;

namespace EasternKingdoms.Simulation.Tests;

public sealed class LunaraEngineTests
{
    [Fact]
    public void ProcessorIsByteDeterministicForTheSameInput()
    {
        var processor = new DeterministicMoonTideProcessor();

        string first = MoonTideSnapshotSerializer.Serialize(processor.Process(CreateQualifiedInput()));
        string second = MoonTideSnapshotSerializer.Serialize(processor.Process(CreateQualifiedInput()));

        Assert.Equal(first, second);
    }

    [Fact]
    public void LunarAndTideEquationAnchorsAreExact()
    {
        MoonTideSnapshot newMoon = new DeterministicMoonTideProcessor().Process(
            CreateQualifiedInput() with
            {
                LunarPhaseStep = 0,
                RitualAttempts = [],
                ApiResponses = []
            });
        MoonTideSnapshot fullMoon = new DeterministicMoonTideProcessor().Process(
            CreateQualifiedInput() with
            {
                RitualAttempts = [],
                ApiResponses = []
            });

        Assert.Equal(LunarPhaseName.New, newMoon.LunarCycle.Phase);
        Assert.Equal(0, newMoon.LunarCycle.IlluminationMicros);
        Assert.Equal(LunaraBounds.Unit, newMoon.LunarCycle.SpringAlignmentMicros);
        Assert.Equal(LunaraBounds.Unit, newMoon.Tide.SignedWaveMicros);
        Assert.Equal(500_000, newMoon.Tide.AmplitudeMicros);
        Assert.Equal(LunaraBounds.Unit, newMoon.Tide.LevelMicros);
        Assert.Equal(400_000, newMoon.ResonanceMicros);

        Assert.Equal(LunarPhaseName.Full, fullMoon.LunarCycle.Phase);
        Assert.Equal(LunaraBounds.Unit, fullMoon.LunarCycle.IlluminationMicros);
        Assert.Equal(LunaraBounds.Unit, fullMoon.ResonanceMicros);
    }

    [Fact]
    public void QualifiedLocalRitualProducesOnlyAPureDirective()
    {
        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(CreateQualifiedInput());

        Assert.Equal(MoonTideDisposition.Completed, snapshot.Disposition);
        LunaraRelicState relic = Assert.Single(snapshot.Relics);
        Assert.Equal(950_000, relic.ChargeMicros);
        Assert.Equal(3, relic.LocalRitualCount);
        LunaraManifestationDirective directive = Assert.Single(snapshot.ManifestationDirectives);
        Assert.Equal("ritual.42.01", directive.RitualId);
        Assert.Equal("LUNARA-VOICE-GEM-01", directive.SoulSignature);
        Assert.Equal(250_000, directive.ProbabilityMicros);
        Assert.Equal(116_548, directive.RollMicros);
        Assert.Equal("local-cult+local-ritual+containment-cleared", directive.Authority);
    }

    [Fact]
    public void RemoteManifestationRequestContainsBeforeRewardOrRitualMutation()
    {
        MoonTideInput input = CreateQualifiedInput();
        input = input with
        {
            ApiResponses =
            [
                input.ApiResponses[0] with
                {
                    ResponseId = "response.42.hostile",
                    ManifestationRequested = true
                }
            ]
        };

        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(input);

        Assert.Equal(MoonTideDisposition.Contained, snapshot.Disposition);
        Assert.Equal("REMOTE_MANIFESTATION_AUTHORITY_REJECTED", snapshot.ContainmentCode);
        Assert.Empty(snapshot.ManifestationDirectives);
        LunaraRelicState relic = Assert.Single(snapshot.Relics);
        Assert.Equal(700_000, relic.ChargeMicros);
        Assert.Equal(2, relic.LocalRitualCount);
    }

    [Fact]
    public void ServerAcceptanceWithoutLocalValidationCannotAuthorizeManifestation()
    {
        MoonTideInput input = CreateQualifiedInput();
        input = input with
        {
            RitualAttempts =
            [
                input.RitualAttempts[0] with
                {
                    LocalCultAuthorized = false,
                    LocalRitualValid = false
                }
            ]
        };

        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(input);

        Assert.Equal(MoonTideDisposition.Completed, snapshot.Disposition);
        Assert.Empty(snapshot.ManifestationDirectives);
        LunaraRelicState relic = Assert.Single(snapshot.Relics);
        Assert.Equal(950_000, relic.ChargeMicros);
        Assert.Equal(2, relic.LocalRitualCount);
    }

    [Fact]
    public void CoreClampsEachRelicRewardGroupToOneQuarterPerResponse()
    {
        MoonTideInput input = CreateQualifiedInput();
        input = input with
        {
            Relics = [input.Relics[0] with { ChargeMicros = 100_000, LocalRitualCount = 0 }],
            RitualAttempts = [input.RitualAttempts[0] with { LocalCultAuthorized = false }],
            ApiResponses =
            [
                input.ApiResponses[0] with
                {
                    Rewards =
                    [
                        new LunaraApiReward("relic.lunara.01", 500_000),
                        new LunaraApiReward("relic.lunara.01", -100_000)
                    ]
                }
            ],
            History = []
        };

        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(input);

        Assert.Equal(MoonTideDisposition.Completed, snapshot.Disposition);
        Assert.Equal(350_000, Assert.Single(snapshot.Relics).ChargeMicros);
    }

    [Fact]
    public void AggregateRewardAboveGenerationBoundContainsWithoutMutation()
    {
        MoonTideInput input = CreateQualifiedInput();
        input = input with
        {
            ApiResponses =
            [
                input.ApiResponses[0] with { ResponseId = "response.42.01" },
                input.ApiResponses[0] with { ResponseId = "response.42.02" },
                input.ApiResponses[0] with { ResponseId = "response.42.03" }
            ]
        };

        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(input);

        Assert.Equal(MoonTideDisposition.Contained, snapshot.Disposition);
        Assert.Equal("GENERATION_REWARD_LIMIT", snapshot.ContainmentCode);
        Assert.Equal(700_000, Assert.Single(snapshot.Relics).ChargeMicros);
        Assert.Empty(snapshot.ManifestationDirectives);
    }

    [Fact]
    public void SoulDriftAnywhereInHistoryEngagesContainment()
    {
        MoonTideInput input = CreateQualifiedInput();
        input = input with
        {
            History =
            [
                new MoonTideHistoryPoint(
                    40,
                    [new LunaraRelicHistoryState("relic.lunara.01", "LUNARA-VOICE-GEM-ORIGINAL", 700_000, 2)],
                    0),
                new MoonTideHistoryPoint(
                    41,
                    [new LunaraRelicHistoryState("relic.lunara.01", "LUNARA-VOICE-GEM-01", 700_000, 2)],
                    0)
            ]
        };

        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(input);

        Assert.Equal(MoonTideDisposition.Contained, snapshot.Disposition);
        Assert.Equal("SOUL_IDENTITY_DRIFT", snapshot.ContainmentCode);
        Assert.Empty(snapshot.ManifestationDirectives);
    }

    [Fact]
    public void HistoryAndGenerationBoundsAreFinite()
    {
        MoonTideHistoryPoint[] excessiveHistory = Enumerable.Range(0, LunaraBounds.MaximumHistoryPoints + 1)
            .Select(generation => new MoonTideHistoryPoint(generation, [], 0))
            .ToArray();
        MoonTideInput excessiveHistoryInput = CreateQualifiedInput() with
        {
            Generation = 200,
            History = excessiveHistory
        };
        MoonTideInput excessiveGenerationInput = CreateQualifiedInput() with
        {
            Generation = LunaraBounds.MaximumGeneration + 1,
            History = []
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeterministicMoonTideProcessor().Process(excessiveHistoryInput));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DeterministicMoonTideProcessor().Process(excessiveGenerationInput));
    }

    [Fact]
    public void SnapshotEnvelopeRoundTripsAndRejectsIntegrityOrEquationTampering()
    {
        MoonTideSnapshot snapshot = new DeterministicMoonTideProcessor().Process(CreateQualifiedInput());
        string serialized = MoonTideSnapshotSerializer.Serialize(snapshot);

        MoonTideSnapshotEnvelope verified = MoonTideSnapshotSerializer.DeserializeAndVerify(serialized);
        Assert.Equal(MoonTideSnapshotSerializer.SchemaVersion, verified.SchemaVersion);
        Assert.Equal(MoonTideSnapshotSerializer.Authority, verified.Authority);
        Assert.Equal(snapshot.Generation, verified.Payload.Generation);

        string tampered = serialized.Replace(
            "\"chargeMicros\":950000",
            "\"chargeMicros\":949999",
            StringComparison.Ordinal);
        Assert.NotEqual(serialized, tampered);
        Assert.Throws<InvalidDataException>(() => MoonTideSnapshotSerializer.DeserializeAndVerify(tampered));

        MoonTideSnapshot equationTampered = snapshot with
        {
            ResonanceMicros = snapshot.ResonanceMicros - 1
        };
        Assert.Throws<InvalidDataException>(() => MoonTideSnapshotSerializer.Serialize(equationTampered));

        LunaraEvent originalEvent = snapshot.Events[0];
        MoonTideSnapshot eventTampered = snapshot with
        {
            Events = [originalEvent with { Detail = "Rehashed but unsupported event claim." }]
        };
        Assert.Throws<InvalidDataException>(() => MoonTideSnapshotSerializer.Serialize(eventTampered));
    }

    [Fact]
    public async Task ApiClientCorrelatesAndClampsRewardsWhilePreservingRemoteIntent()
    {
        var raw = new LunaraApiRawResponse(
            "response.42.01",
            "ritual.42.01",
            true,
            true,
            [
                new LunaraApiRawReward("relic.b", 0.50m),
                new LunaraApiRawReward("relic.a", 0.20m),
                new LunaraApiRawReward("relic.a", 0.20m),
                new LunaraApiRawReward("relic.a", -1.00m)
            ]);
        var transport = new StubTransport(raw);
        var client = new LunaraApiClient(transport);

        LunaraApiResponse response = await client.PostRitualAttemptAsync(CreatePostRequest());

        Assert.True(response.ManifestationRequested);
        Assert.NotNull(transport.LastRequest);
        Assert.Equal(CreatePostRequest(), transport.LastRequest!);
        Assert.Equal(new[] { "relic.a", "relic.b" }, response.Rewards.Select(reward => reward.RelicId).ToArray());
        Assert.All(response.Rewards, reward => Assert.Equal(250_000, reward.RewardChargeMicros));
    }

    [Fact]
    public void ApiClientRejectsMismatchedRitualCorrelation()
    {
        var raw = new LunaraApiRawResponse(
            "response.42.bad",
            "ritual.other",
            true,
            false,
            []);
        var client = new LunaraApiClient(new StubTransport(raw));

        Assert.Throws<InvalidOperationException>(() =>
            client.PostRitualAttemptAsync(CreatePostRequest()).AsTask().GetAwaiter().GetResult());
    }

    [Fact]
    public async Task CliRunsQualifiedLunaraFixtureWithStableEnvelope()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["lunara", "run", "--input", Fixture("valid-lunara.json"), "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.SuccessExitCode, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        JsonElement root = document.RootElement;
        Assert.Equal(MoonTideSnapshotSerializer.SchemaVersion, root.GetProperty("schemaVersion").GetString());
        JsonElement payload = root.GetProperty("payload");
        Assert.Equal("Completed", payload.GetProperty("disposition").GetString());
        Assert.Equal(950_000, payload.GetProperty("relics")[0].GetProperty("chargeMicros").GetInt32());
        Assert.Equal(1, payload.GetProperty("manifestationDirectives").GetArrayLength());
    }

    [Fact]
    public async Task CliContainsRemoteManifestationFixtureBeforeStateChange()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = await EvermoreCliApp.InvokeAsync(
            ["lunara", "run", "--input", Fixture("remote-manifestation-lunara.json"), "--format", "json"],
            output,
            error);

        Assert.Equal(EvermoreCliApp.LunaraContainmentExitCode, exitCode);
        Assert.Contains("LUNARA_CONTAINMENT: REMOTE_MANIFESTATION_AUTHORITY_REJECTED", error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        JsonElement payload = document.RootElement.GetProperty("payload");
        Assert.Equal("Contained", payload.GetProperty("disposition").GetString());
        Assert.Equal(700_000, payload.GetProperty("relics")[0].GetProperty("chargeMicros").GetInt32());
        Assert.Equal(0, payload.GetProperty("manifestationDirectives").GetArrayLength());
    }

    [Fact]
    public async Task CliRejectsLunaraInputLargerThanTheFiniteFileBound()
    {
        string path = Path.GetTempFileName();
        try
        {
            using (FileStream stream = File.OpenWrite(path))
                stream.SetLength((long)LunaraBounds.MaximumCliInputBytes + 1);
            var error = new StringWriter();

            int exitCode = await EvermoreCliApp.InvokeAsync(
                ["lunara", "run", "--input", path],
                new StringWriter(),
                error);

            Assert.Equal(EvermoreCliApp.LunaraInputFailureExitCode, exitCode);
            Assert.Contains("LUNARA_INPUT_FAILURE", error.ToString());
            Assert.Contains("cannot exceed", error.ToString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static MoonTideInput CreateQualifiedInput() =>
        new(
            529,
            42,
            14_765,
            0,
            500_000,
            0,
            [new LunaraRelicState("relic.lunara.01", "LUNARA-VOICE-GEM-01", 700_000, 2)],
            [new LunaraRitualAttempt("ritual.42.01", "relic.lunara.01", "cult.silvermoon.local", true, true)],
            [
                new LunaraApiResponse(
                    "response.42.01",
                    "ritual.42.01",
                    true,
                    false,
                    [new LunaraApiReward("relic.lunara.01", 250_000)])
            ],
            [
                new MoonTideHistoryPoint(
                    41,
                    [new LunaraRelicHistoryState("relic.lunara.01", "LUNARA-VOICE-GEM-01", 700_000, 2)],
                    0)
            ]);

    private static LunaraApiPostRequest CreatePostRequest() =>
        new("request.42.01", "ritual.42.01", "relic.lunara.01", "cult.silvermoon.local", true, true);

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "CliFixtures", name);

    private sealed class StubTransport : ILunaraApiTransport
    {
        private readonly LunaraApiRawResponse _response;

        public StubTransport(LunaraApiRawResponse response)
        {
            _response = response;
        }

        public LunaraApiPostRequest? LastRequest { get; private set; }

        public ValueTask<LunaraApiRawResponse> PostRitualAttemptAsync(
            LunaraApiPostRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return ValueTask.FromResult(_response);
        }
    }
}
