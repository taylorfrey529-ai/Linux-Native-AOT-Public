namespace Evermore.Solune.Core;

public static class SoluneBounds
{
    public const int Unit = 1_000_000;
    public const int PiMicros = 3_141_593;
    public const long StefanBoltzmannNumerator = 5_670_374_419;
    public const int EverturnCount = 14;
    public const int DaysPerEverturn = 26;
    public const int OrdinaryDaysPerEverturn = 25;
    public const int BaseCalendarDays = EverturnCount * DaysPerEverturn;
    public const int EverwakeLengthMicrodays = Unit;
    public const int StillstarLengthMicrodays = Unit / 4;
    public const int ReconciliationLengthMicrodays = EverwakeLengthMicrodays + StillstarLengthMicrodays;
    public const int EvercycleLengthMicrodays = (BaseCalendarDays * Unit) + ReconciliationLengthMicrodays;
    public const int MaximumGeneration = 1_000_000;
    public const int MaximumHistoryPoints = 128;
    public const int MaximumSnapshotEvents = 16;
    public const int MaximumOperationDirectives = 1;
    public const int MaximumIdentifierLength = 96;
    public const int MaximumContinuitySignatureLength = 128;
    public const int MaximumDetailLength = 512;
    public const int MaximumCliInputBytes = 16 * 1_024 * 1_024;

    public const int MinimumBodyRadiusKm = 1;
    public const int MaximumBodyRadiusKm = 100_000;
    public const int MinimumOrbitRadiusKm = 10_000;
    public const int MaximumOrbitRadiusKm = 10_000_000;
    public const long MaximumLuminosityTerawatts = 1_000_000_000_000;
    public const int MaximumPhotosphereKelvin = 50_000;
    public const int MaximumOrbitalPeriodTenThousandthDays = 100_000_000;
    public const int MaximumRevolutionsPerEvercycle = 1_024;
    public const int MaximumApparentDiameterMicrodegrees = 180 * Unit;
    public const int MaximumRotationMicrohours = 2_000 * Unit;
    public const int MaximumTidalForceRatioMicros = 10 * Unit;
    public const int MaximumImpliedMassRatioMicros = 10 * Unit;
    public const int MaximumInclinationMicrodegrees = 180 * Unit;

    public const int MinimumContainmentIntegrityMicros = 950_000;
    public const int MaximumContainedOrbitEccentricityMicros = 10_000;
    public const int MaximumContainedInclinationMicrodegrees = 1_000_000;
    public const int MaximumLuminosityDeviationMicros = 5_000;
    public const int MaximumApparentDiameterDeviationMicrodegrees = 1_000;
    public const int MaximumTidalForceDeviationMicros = 5_000;
    public const int MaximumSolarDayDeviationMicrohours = 10_000;
    public const int MinimumIrradianceMilliwattsPerSquareMeter = 1_200_000;
    public const int MaximumIrradianceMilliwattsPerSquareMeter = 1_500_000;

    public const int ReferenceEarthMoonMassRatioMicros = 12_300;
    public const int ReferenceEarthMoonOrbitKm = 384_400;
    public const int MaximumOrbitDriftKmPerGeneration = 100;
    public const int MaximumLuminosityDriftMicrosPerGeneration = 5_000;
    public const int MaximumTemperatureDriftKelvinPerGeneration = 25;
    public const int MaximumIntegrityLossMicrosPerGeneration = 25_000;

    internal static void RequireIdentifier(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumIdentifierLength)
            throw new ArgumentException($"{parameterName} must contain 1..{MaximumIdentifierLength} nonblank characters.", parameterName);
    }

    internal static void RequireContinuitySignature(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumContinuitySignatureLength)
        {
            throw new ArgumentException(
                $"{parameterName} must contain 1..{MaximumContinuitySignatureLength} nonblank characters.",
                parameterName);
        }
    }

    internal static void RequireDetail(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumDetailLength)
            throw new ArgumentException($"{parameterName} must contain 1..{MaximumDetailLength} nonblank characters.", parameterName);
    }
}
