using System.Numerics;

namespace Evermore.Solune.Core;

internal static class SoluneEquations
{
    private static readonly BigInteger BlackbodyDenominator = BigInteger.Pow(10, 29);
    private static readonly BigInteger IrradianceNumeratorScale = BigInteger.Pow(10, 15);

    internal static SoluneOrbitState ComputeOrbit(SoluneInput input)
    {
        SoluneModelState model = input.Model;
        long periodDenominator = (long)model.RevolutionsPerEvercycle * 100;
        int calculatedPeriod = checked((int)(
            (SoluneBounds.EvercycleLengthMicrodays + (periodDenominator / 2)) /
            periodDenominator));

        long totalOrbitalProgress = checked(
            (long)input.EvercycleStepMicrodays * model.RevolutionsPerEvercycle);
        int revolutionIndex = checked((int)(
            totalOrbitalProgress / SoluneBounds.EvercycleLengthMicrodays));
        long phaseRemainder = totalOrbitalProgress % SoluneBounds.EvercycleLengthMicrodays;
        int phaseMicros = checked((int)(
            (phaseRemainder * SoluneBounds.Unit) / SoluneBounds.EvercycleLengthMicrodays));
        int impliedMassRatioMicros = checked((int)(
            ((long)model.BarycenterKm * SoluneBounds.Unit) /
            (model.OrbitRadiusKm - model.BarycenterKm)));

        return new SoluneOrbitState(
            model.OrbitRadiusKm,
            calculatedPeriod,
            revolutionIndex,
            phaseMicros,
            model.BarycenterKm,
            impliedMassRatioMicros,
            model.OrbitEccentricityMicros,
            model.OrbitInclinationMicrodegrees);
    }

    internal static SoluneRadianceState ComputeRadiance(
        SoluneModelState model,
        SoluneOrbitState orbit)
    {
        BigInteger radiusSquared = (BigInteger)model.SoluneRadiusKm * model.SoluneRadiusKm;
        BigInteger temperatureFourth = BigInteger.Pow(model.PhotosphereKelvin, 4);
        BigInteger blackbodyNumerator =
            4 *
            SoluneBounds.PiMicros *
            SoluneBounds.StefanBoltzmannNumerator *
            radiusSquared *
            temperatureFourth;
        long calculatedLuminosity = ToInt64(
            blackbodyNumerator / BlackbodyDenominator,
            "calculated blackbody luminosity");
        int luminosityDeviation = RelativeDeviationMicros(
            model.LuminosityTerawatts,
            calculatedLuminosity);

        BigInteger irradianceNumerator =
            (BigInteger)model.LuminosityTerawatts * IrradianceNumeratorScale;
        BigInteger irradianceDenominator =
            (BigInteger)4 *
            SoluneBounds.PiMicros *
            model.OrbitRadiusKm *
            model.OrbitRadiusKm;
        long irradiance = ToInt64(
            irradianceNumerator / irradianceDenominator,
            "calculated irradiance");

        BigInteger apparentNumerator =
            (BigInteger)2 *
            model.SoluneRadiusKm *
            180 *
            SoluneBounds.Unit *
            SoluneBounds.Unit;
        BigInteger apparentDenominator =
            (BigInteger)SoluneBounds.PiMicros * model.OrbitRadiusKm;
        int calculatedApparentDiameter = ToInt32(
            apparentNumerator / apparentDenominator,
            "calculated apparent diameter");
        int apparentDeviation = Math.Abs(
            model.ApparentDiameterMicrodegrees - calculatedApparentDiameter);

        BigInteger referenceOrbitCubed = BigInteger.Pow(SoluneBounds.ReferenceEarthMoonOrbitKm, 3);
        BigInteger orbitCubed = BigInteger.Pow(model.OrbitRadiusKm, 3);
        BigInteger tidalNumerator =
            (BigInteger)orbit.ImpliedMassRatioMicros *
            referenceOrbitCubed *
            SoluneBounds.Unit;
        BigInteger tidalDenominator =
            (BigInteger)SoluneBounds.ReferenceEarthMoonMassRatioMicros * orbitCubed;
        long calculatedTidalForce = ToInt64(
            tidalNumerator / tidalDenominator,
            "calculated tidal-force ratio");
        long tidalDeviation = Math.Abs(
            (long)model.TidalForceRatioMicros - calculatedTidalForce);

        long orbitalPeriodMicrohours = checked(
            (long)model.OrbitalPeriodTenThousandthDays * 2_400);
        BigInteger solarDayNumerator =
            (BigInteger)model.SiderealRotationMicrohours * orbitalPeriodMicrohours;
        long calculatedSolarDay = ToInt64(
            solarDayNumerator /
            (orbitalPeriodMicrohours - model.SiderealRotationMicrohours),
            "calculated local solar day");
        long solarDayDeviation = Math.Abs(
            (long)model.LocalSolarDayMicrohours - calculatedSolarDay);

        return new SoluneRadianceState(
            model.LuminosityTerawatts,
            calculatedLuminosity,
            luminosityDeviation,
            irradiance,
            model.ApparentDiameterMicrodegrees,
            calculatedApparentDiameter,
            apparentDeviation,
            model.TidalForceRatioMicros,
            calculatedTidalForce,
            tidalDeviation,
            model.LocalSolarDayMicrohours,
            calculatedSolarDay,
            solarDayDeviation,
            model.PhotosphereKelvin);
    }

    internal static EvercycleState ComputeCalendar(int evercycleStepMicrodays)
    {
        int baseCalendarLength = SoluneBounds.BaseCalendarDays * SoluneBounds.Unit;
        int everwakeEnd = baseCalendarLength + SoluneBounds.EverwakeLengthMicrodays;
        if (evercycleStepMicrodays < baseCalendarLength)
        {
            int everturnLength = SoluneBounds.DaysPerEverturn * SoluneBounds.Unit;
            int offsetWithinEverturn = evercycleStepMicrodays % everturnLength;
            return new EvercycleState(
                evercycleStepMicrodays,
                EvercyclePhase.Everturn,
                (evercycleStepMicrodays / everturnLength) + 1,
                (offsetWithinEverturn / SoluneBounds.Unit) + 1,
                offsetWithinEverturn % SoluneBounds.Unit,
                SoluneBounds.BaseCalendarDays,
                SoluneBounds.ReconciliationLengthMicrodays,
                SoluneBounds.EvercycleLengthMicrodays);
        }

        if (evercycleStepMicrodays < everwakeEnd)
        {
            return new EvercycleState(
                evercycleStepMicrodays,
                EvercyclePhase.Everwake,
                0,
                0,
                evercycleStepMicrodays - baseCalendarLength,
                SoluneBounds.BaseCalendarDays,
                SoluneBounds.ReconciliationLengthMicrodays,
                SoluneBounds.EvercycleLengthMicrodays);
        }

        int stillstarProgress = checked((int)(
            ((long)(evercycleStepMicrodays - everwakeEnd) * SoluneBounds.Unit) /
            SoluneBounds.StillstarLengthMicrodays));
        return new EvercycleState(
            evercycleStepMicrodays,
            EvercyclePhase.Stillstar,
            0,
            0,
            stillstarProgress,
            SoluneBounds.BaseCalendarDays,
            SoluneBounds.ReconciliationLengthMicrodays,
            SoluneBounds.EvercycleLengthMicrodays);
    }

    internal static int RelativeDeviationMicros(long actual, long expected)
    {
        if (expected <= 0)
            return int.MaxValue;

        BigInteger difference = BigInteger.Abs((BigInteger)actual - expected);
        BigInteger deviation = (difference * SoluneBounds.Unit) / expected;
        return deviation > int.MaxValue ? int.MaxValue : (int)deviation;
    }

    private static long ToInt64(BigInteger value, string field)
    {
        if (value < 0 || value > long.MaxValue)
            throw new ArgumentOutOfRangeException(field, $"{field} exceeds the finite Int64 result bound.");
        return (long)value;
    }

    private static int ToInt32(BigInteger value, string field)
    {
        if (value < 0 || value > int.MaxValue)
            throw new ArgumentOutOfRangeException(field, $"{field} exceeds the finite Int32 result bound.");
        return (int)value;
    }
}
