namespace Evermore.Solune.Core;

public sealed record SoluneIdentityDrift(
    int Generation,
    string ConstructId,
    string ContinuitySignature);

public sealed record SoluneHistoryViolation(
    int Generation,
    string Field,
    long PreviousValue,
    long CurrentValue);

public sealed record SoluneContainmentContext(
    SoluneModelState Model,
    SoluneOrbitState Orbit,
    SoluneRadianceState Radiance,
    IReadOnlyList<SoluneIdentityDrift> IdentityDrifts,
    IReadOnlyList<SoluneHistoryViolation> HistoryViolations);

public sealed record SoluneContainmentDecision(
    bool IsContained,
    string? Code,
    string? Reason)
{
    public static SoluneContainmentDecision Continue { get; } = new(false, null, null);

    public static SoluneContainmentDecision Contain(string code, string reason) =>
        new(true, code, reason);
}

public sealed class SoluneContainmentPolicy
{
    public SoluneContainmentDecision Evaluate(SoluneContainmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        SoluneModelState model = context.Model;

        if (!model.LocalConstructAuthorized)
        {
            return SoluneContainmentDecision.Contain(
                "LOCAL_CONSTRUCT_AUTHORITY_REQUIRED",
                "Solune may operate only as an explicitly authorized local contained stellar construct.");
        }

        if (context.IdentityDrifts.Count > 0)
        {
            return SoluneContainmentDecision.Contain(
                "CONTINUITY_IDENTITY_DRIFT",
                $"Solune continuity identity changed at generation {context.IdentityDrifts[0].Generation}.");
        }

        if (context.HistoryViolations.Count > 0)
        {
            SoluneHistoryViolation violation = context.HistoryViolations[0];
            return SoluneContainmentDecision.Contain(
                "HISTORY_STATE_DRIFT",
                $"Solune history exceeded the protected {violation.Field} drift bound at generation {violation.Generation}.");
        }

        if (model.RevolutionsPerEvercycle != SoluneBounds.EverturnCount)
        {
            return SoluneContainmentDecision.Contain(
                "EVERCYCLE_REVOLUTION_COUNT_MISMATCH",
                $"Solune requires exactly {SoluneBounds.EverturnCount} revolutions per 365.25-day Evercycle.");
        }

        if (context.Orbit.EccentricityMicros > SoluneBounds.MaximumContainedOrbitEccentricityMicros ||
            Math.Abs(context.Orbit.InclinationMicrodegrees) > SoluneBounds.MaximumContainedInclinationMicrodegrees)
        {
            return SoluneContainmentDecision.Contain(
                "ORBIT_GEOMETRY_OUTSIDE_CONTAINMENT",
                "The proposed Solune orbit is outside the near-circular equatorial containment envelope.");
        }

        if (model.ContainmentIntegrityMicros < SoluneBounds.MinimumContainmentIntegrityMicros)
        {
            return SoluneContainmentDecision.Contain(
                "CONTAINMENT_INTEGRITY_LOW",
                $"Containment integrity is below {SoluneBounds.MinimumContainmentIntegrityMicros} micro-units.");
        }

        if (context.Orbit.BarycenterKm >= model.EvermoreRadiusKm)
        {
            return SoluneContainmentDecision.Contain(
                "BARYCENTER_OUTSIDE_EVERMORE",
                "The calculated system barycenter is not contained within Evermore's supplied radius.");
        }

        if (Math.Abs(
                model.OrbitalPeriodTenThousandthDays -
                context.Orbit.CalculatedPeriodTenThousandthDays) > 1)
        {
            return SoluneContainmentDecision.Contain(
                "EVERCYCLE_PERIOD_MISMATCH",
                "The supplied orbital period does not reconcile fourteen revolutions with the 365.25-day Evercycle.");
        }

        if (context.Radiance.LuminosityDeviationMicros > SoluneBounds.MaximumLuminosityDeviationMicros)
        {
            return SoluneContainmentDecision.Contain(
                "RADIANCE_MODEL_MISMATCH",
                "The supplied luminosity exceeds the allowed deviation from the fixed-point blackbody model.");
        }

        if (context.Radiance.IrradianceMilliwattsPerSquareMeter is
            < SoluneBounds.MinimumIrradianceMilliwattsPerSquareMeter or
            > SoluneBounds.MaximumIrradianceMilliwattsPerSquareMeter)
        {
            return SoluneContainmentDecision.Contain(
                "IRRADIANCE_ENVELOPE_EXCEEDED",
                "The calculated top-of-model irradiance is outside Solune's bounded local-sun envelope.");
        }

        if (context.Radiance.ApparentDiameterDeviationMicrodegrees >
            SoluneBounds.MaximumApparentDiameterDeviationMicrodegrees)
        {
            return SoluneContainmentDecision.Contain(
                "APPARENT_DIAMETER_MISMATCH",
                "The supplied apparent diameter does not match the fixed-point small-angle equation.");
        }

        if (context.Radiance.TidalForceDeviationMicros > SoluneBounds.MaximumTidalForceDeviationMicros)
        {
            return SoluneContainmentDecision.Contain(
                "TIDAL_FORCE_MODEL_MISMATCH",
                "The supplied normalized tidal-force ratio does not match the declared barycentric model.");
        }

        if (context.Radiance.SolarDayDeviationMicrohours >
            SoluneBounds.MaximumSolarDayDeviationMicrohours)
        {
            return SoluneContainmentDecision.Contain(
                "SOLAR_DAY_MODEL_MISMATCH",
                "The supplied local solar day does not match the prograde sidereal/orbital period equation.");
        }

        return SoluneContainmentDecision.Continue;
    }
}
