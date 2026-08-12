namespace Evermore.Solune.Core;

public static class SoluneReferenceModel
{
    public static SoluneModelState Create() =>
        new(
            "construct.solune.01",
            "SOLUNE-HEARTHSTAR-01",
            6_378,
            1_738,
            373_089,
            260_893,
            14,
            2_382_000_000,
            5_768,
            533_800,
            23_114_000,
            24_000_000,
            1_094_000,
            4_531,
            0,
            0,
            SoluneBounds.Unit,
            true);
}
