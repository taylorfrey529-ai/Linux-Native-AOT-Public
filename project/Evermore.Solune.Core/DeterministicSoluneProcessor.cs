namespace Evermore.Solune.Core;

public sealed class DeterministicSoluneProcessor
{
    internal const string DirectiveCode = "MAINTAIN_CONTAINED_STELLAR_CONSTRUCT";
    internal const string DirectiveAuthority = "local-construct+equations+continuity+containment-cleared";
    internal const string ModelEvaluatedDetail =
        "Fixed-point orbital, radiance, rotation, tide, and calendar equations evaluated.";
    internal const string ContainmentClearedDetail =
        "Pure maintenance directive only; the core performs no actuation, spawn, state write, or canon promotion.";

    private readonly SoluneContainmentPolicy _containment;

    public DeterministicSoluneProcessor(SoluneContainmentPolicy? containment = null)
    {
        _containment = containment ?? new SoluneContainmentPolicy();
    }

    public SoluneSnapshot Process(
        SoluneInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateInput(input);
        cancellationToken.ThrowIfCancellationRequested();

        SoluneHistoryPoint[] history = input.History
            .OrderBy(point => point.Generation)
            .ToArray();
        SoluneOrbitState orbit = SoluneEquations.ComputeOrbit(input);
        SoluneRadianceState radiance = SoluneEquations.ComputeRadiance(input.Model, orbit);
        EvercycleState calendar = SoluneEquations.ComputeCalendar(input.EvercycleStepMicrodays);

        var identityDrifts = new List<SoluneIdentityDrift>();
        var historyViolations = new List<SoluneHistoryViolation>();
        SoluneHistoryPoint? previous = null;
        foreach (SoluneHistoryPoint point in history)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!StringComparer.Ordinal.Equals(point.ConstructId, input.Model.ConstructId) ||
                !StringComparer.Ordinal.Equals(point.ContinuitySignature, input.Model.ContinuitySignature))
            {
                identityDrifts.Add(new SoluneIdentityDrift(
                    point.Generation,
                    point.ConstructId,
                    point.ContinuitySignature));
            }

            if (previous is not null &&
                StringComparer.Ordinal.Equals(previous.ConstructId, point.ConstructId) &&
                StringComparer.Ordinal.Equals(previous.ContinuitySignature, point.ContinuitySignature))
            {
                AddHistoryViolations(previous, point, historyViolations);
            }

            previous = point;
        }

        if (previous is not null &&
            StringComparer.Ordinal.Equals(previous.ConstructId, input.Model.ConstructId) &&
            StringComparer.Ordinal.Equals(previous.ContinuitySignature, input.Model.ContinuitySignature))
        {
            var current = new SoluneHistoryPoint(
                input.Generation,
                input.Model.ConstructId,
                input.Model.ContinuitySignature,
                input.Model.OrbitRadiusKm,
                input.Model.LuminosityTerawatts,
                input.Model.PhotosphereKelvin,
                input.Model.ContainmentIntegrityMicros);
            AddHistoryViolations(previous, current, historyViolations);
        }

        SoluneContainmentDecision decision = _containment.Evaluate(
            new SoluneContainmentContext(
                input.Model,
                orbit,
                radiance,
                identityDrifts,
                historyViolations));
        if (decision.IsContained)
        {
            string code = decision.Code ?? throw new InvalidOperationException("Contained Solune decision omitted its code.");
            string reason = decision.Reason ?? throw new InvalidOperationException("Contained Solune decision omitted its reason.");
            return new SoluneSnapshot(
                SoluneDisposition.Contained,
                code,
                reason,
                input.Generation,
                input.EvercycleStepMicrodays,
                input.Model,
                history,
                orbit,
                radiance,
                calendar,
                [],
                [new SoluneEvent("CONTAINED", input.Model.ConstructId, 0, reason)]);
        }

        var directive = new SoluneOperationDirective(
            DirectiveCode,
            input.Model.ConstructId,
            input.Generation,
            radiance.IrradianceMilliwattsPerSquareMeter,
            DirectiveAuthority);
        return new SoluneSnapshot(
            SoluneDisposition.Completed,
            null,
            null,
            input.Generation,
            input.EvercycleStepMicrodays,
            input.Model,
            history,
            orbit,
            radiance,
            calendar,
            [directive],
            [
                new SoluneEvent(
                    "MODEL_EVALUATED",
                    input.Model.ConstructId,
                    radiance.IrradianceMilliwattsPerSquareMeter,
                    ModelEvaluatedDetail),
                new SoluneEvent(
                    "CONTAINMENT_CLEARED",
                    input.Model.ConstructId,
                    input.Model.ContainmentIntegrityMicros,
                    ContainmentClearedDetail)
            ]);
    }

    internal static void ValidateInput(SoluneInput input)
    {
        ArgumentNullException.ThrowIfNull(input.Model);
        ArgumentNullException.ThrowIfNull(input.History);
        if (input.Generation is < 0 or > SoluneBounds.MaximumGeneration)
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"{nameof(input.Generation)} must be within 0..{SoluneBounds.MaximumGeneration}.");
        if (input.EvercycleStepMicrodays is < 0 or >= SoluneBounds.EvercycleLengthMicrodays)
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"{nameof(input.EvercycleStepMicrodays)} must be within the finite Evercycle.");
        if (input.History.Count > SoluneBounds.MaximumHistoryPoints)
            throw new ArgumentOutOfRangeException(
                nameof(input),
                $"{nameof(input.History)} cannot exceed {SoluneBounds.MaximumHistoryPoints} points.");

        ValidateModel(input.Model);
        int previousGeneration = -1;
        foreach (SoluneHistoryPoint point in input.History)
        {
            ArgumentNullException.ThrowIfNull(point);
            if (point.Generation < 0 || point.Generation >= input.Generation)
                throw new ArgumentOutOfRangeException(nameof(input), "History generations must precede the current generation.");
            if (point.Generation <= previousGeneration)
                throw new ArgumentException("History generations must be strictly increasing.", nameof(input));
            previousGeneration = point.Generation;
            ValidateHistoryPoint(point);
        }
    }

    private static void ValidateModel(SoluneModelState model)
    {
        SoluneBounds.RequireIdentifier(model.ConstructId, nameof(model.ConstructId));
        SoluneBounds.RequireContinuitySignature(model.ContinuitySignature, nameof(model.ContinuitySignature));
        RequireRange(model.EvermoreRadiusKm, SoluneBounds.MinimumBodyRadiusKm, SoluneBounds.MaximumBodyRadiusKm, nameof(model.EvermoreRadiusKm));
        RequireRange(model.SoluneRadiusKm, SoluneBounds.MinimumBodyRadiusKm, SoluneBounds.MaximumBodyRadiusKm, nameof(model.SoluneRadiusKm));
        RequireRange(model.OrbitRadiusKm, SoluneBounds.MinimumOrbitRadiusKm, SoluneBounds.MaximumOrbitRadiusKm, nameof(model.OrbitRadiusKm));
        if (model.OrbitRadiusKm <= (long)model.EvermoreRadiusKm + model.SoluneRadiusKm)
            throw new ArgumentOutOfRangeException(nameof(model), "Orbit radius must exceed the supplied body radii.");
        RequireRange(model.OrbitalPeriodTenThousandthDays, 1, SoluneBounds.MaximumOrbitalPeriodTenThousandthDays, nameof(model.OrbitalPeriodTenThousandthDays));
        RequireRange(model.RevolutionsPerEvercycle, 1, SoluneBounds.MaximumRevolutionsPerEvercycle, nameof(model.RevolutionsPerEvercycle));
        RequireRange(model.LuminosityTerawatts, 1, SoluneBounds.MaximumLuminosityTerawatts, nameof(model.LuminosityTerawatts));
        RequireRange(model.PhotosphereKelvin, 1, SoluneBounds.MaximumPhotosphereKelvin, nameof(model.PhotosphereKelvin));
        RequireRange(model.ApparentDiameterMicrodegrees, 1, SoluneBounds.MaximumApparentDiameterMicrodegrees, nameof(model.ApparentDiameterMicrodegrees));
        RequireRange(model.SiderealRotationMicrohours, 1, SoluneBounds.MaximumRotationMicrohours, nameof(model.SiderealRotationMicrohours));
        RequireRange(model.LocalSolarDayMicrohours, 1, SoluneBounds.MaximumRotationMicrohours, nameof(model.LocalSolarDayMicrohours));
        long orbitalPeriodMicrohours = checked((long)model.OrbitalPeriodTenThousandthDays * 2_400);
        if (orbitalPeriodMicrohours < (2L * model.SiderealRotationMicrohours))
        {
            throw new ArgumentOutOfRangeException(
                nameof(model),
                "Orbital period must be at least twice the prograde sidereal rotation period within this finite model.");
        }
        RequireRange(model.TidalForceRatioMicros, 0, SoluneBounds.MaximumTidalForceRatioMicros, nameof(model.TidalForceRatioMicros));
        if (model.BarycenterKm < 0 || model.BarycenterKm >= model.OrbitRadiusKm)
            throw new ArgumentOutOfRangeException(nameof(model), $"{nameof(model.BarycenterKm)} must be within the supplied orbit.");
        long impliedMassRatioMicros =
            ((long)model.BarycenterKm * SoluneBounds.Unit) /
            (model.OrbitRadiusKm - model.BarycenterKm);
        if (impliedMassRatioMicros > SoluneBounds.MaximumImpliedMassRatioMicros)
        {
            throw new ArgumentOutOfRangeException(
                nameof(model),
                "The implied barycentric mass ratio exceeds the finite model bound.");
        }
        RequireRange(model.OrbitEccentricityMicros, 0, SoluneBounds.Unit, nameof(model.OrbitEccentricityMicros));
        RequireRange(model.OrbitInclinationMicrodegrees, -SoluneBounds.MaximumInclinationMicrodegrees, SoluneBounds.MaximumInclinationMicrodegrees, nameof(model.OrbitInclinationMicrodegrees));
        RequireRange(model.ContainmentIntegrityMicros, 0, SoluneBounds.Unit, nameof(model.ContainmentIntegrityMicros));
    }

    private static void ValidateHistoryPoint(SoluneHistoryPoint point)
    {
        SoluneBounds.RequireIdentifier(point.ConstructId, nameof(point.ConstructId));
        SoluneBounds.RequireContinuitySignature(point.ContinuitySignature, nameof(point.ContinuitySignature));
        RequireRange(point.OrbitRadiusKm, SoluneBounds.MinimumOrbitRadiusKm, SoluneBounds.MaximumOrbitRadiusKm, nameof(point.OrbitRadiusKm));
        RequireRange(point.LuminosityTerawatts, 1, SoluneBounds.MaximumLuminosityTerawatts, nameof(point.LuminosityTerawatts));
        RequireRange(point.PhotosphereKelvin, 1, SoluneBounds.MaximumPhotosphereKelvin, nameof(point.PhotosphereKelvin));
        RequireRange(point.ContainmentIntegrityMicros, 0, SoluneBounds.Unit, nameof(point.ContainmentIntegrityMicros));
    }

    private static void AddHistoryViolations(
        SoluneHistoryPoint previous,
        SoluneHistoryPoint current,
        ICollection<SoluneHistoryViolation> violations)
    {
        int generations = current.Generation - previous.Generation;
        long allowedOrbitDrift = (long)SoluneBounds.MaximumOrbitDriftKmPerGeneration * generations;
        if (Math.Abs((long)current.OrbitRadiusKm - previous.OrbitRadiusKm) > allowedOrbitDrift)
        {
            violations.Add(new SoluneHistoryViolation(
                current.Generation,
                "orbit-radius",
                previous.OrbitRadiusKm,
                current.OrbitRadiusKm));
        }

        int luminosityDrift = SoluneEquations.RelativeDeviationMicros(
            current.LuminosityTerawatts,
            previous.LuminosityTerawatts);
        long allowedLuminosityDrift = Math.Min(
            SoluneBounds.Unit,
            (long)SoluneBounds.MaximumLuminosityDriftMicrosPerGeneration * generations);
        if (luminosityDrift > allowedLuminosityDrift)
        {
            violations.Add(new SoluneHistoryViolation(
                current.Generation,
                "luminosity",
                previous.LuminosityTerawatts,
                current.LuminosityTerawatts));
        }

        long allowedTemperatureDrift = (long)SoluneBounds.MaximumTemperatureDriftKelvinPerGeneration * generations;
        if (Math.Abs((long)current.PhotosphereKelvin - previous.PhotosphereKelvin) > allowedTemperatureDrift)
        {
            violations.Add(new SoluneHistoryViolation(
                current.Generation,
                "photosphere-temperature",
                previous.PhotosphereKelvin,
                current.PhotosphereKelvin));
        }

        long integrityLoss = (long)previous.ContainmentIntegrityMicros - current.ContainmentIntegrityMicros;
        long allowedIntegrityLoss = (long)SoluneBounds.MaximumIntegrityLossMicrosPerGeneration * generations;
        if (integrityLoss > allowedIntegrityLoss)
        {
            violations.Add(new SoluneHistoryViolation(
                current.Generation,
                "containment-integrity",
                previous.ContainmentIntegrityMicros,
                current.ContainmentIntegrityMicros));
        }
    }

    private static void RequireRange(int value, int minimum, int maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be within {minimum}..{maximum}.");
    }

    private static void RequireRange(long value, long minimum, long maximum, string parameterName)
    {
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be within {minimum}..{maximum}.");
    }
}
