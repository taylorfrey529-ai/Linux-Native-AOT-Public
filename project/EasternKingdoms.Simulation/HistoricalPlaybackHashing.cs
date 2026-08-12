using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

internal static class HistoricalPlaybackHasher
{
    public static string HashCell(
        string zoneId,
        int generation,
        ClimateZoneState? climate,
        LandscapeZoneState? landscape,
        RegionalBiodiversitySnapshot? biodiversity,
        IEnumerable<HistoricalLineagePresence> lineages,
        IEnumerable<RefugiumRecord> refugia,
        IEnumerable<ExtinctionDebtState> debt,
        IEnumerable<LandscapeChangeEvent> landscapeEvents,
        ReconstructionCompleteness completeness,
        IEnumerable<string> missing,
        IEnumerable<HistoricalEvidenceReference> evidence)
    {
        var b = new StringBuilder();
        b.Append("playback-cell/21|").Append(zoneId).Append("|").Append(generation).Append("|").Append(completeness).AppendLine();
        AppendClimate(b, climate);
        AppendLandscape(b, landscape);
        AppendBiodiversity(b, biodiversity);
        foreach (var x in lineages.OrderBy(x => x.LineageId, StringComparer.Ordinal))
            b.Append("Q|").Append(x.LineageId).Append("|").Append(x.SpeciesFamilyId).Append("|").Append(x.ParentLineageId ?? string.Empty)
                .Append("|").Append(x.EvidenceGeneration).Append("|").Append(x.ApproximateGlobalPopulation).Append("|")
                .Append(x.Authority).Append("|").Append(x.Status).Append("|").Append(F(x.MeanDivergence)).AppendLine();
        foreach (var x in refugia.OrderBy(x => x.RefugiumId, StringComparer.Ordinal))
            b.Append("F|").Append(x.RefugiumId).Append("|").Append(x.LineageId).Append("|").Append(x.ZoneId).Append("|")
                .Append(x.FirstObservedGeneration).Append("|").Append(x.LastObservedGeneration).Append("|").Append(x.ConsecutiveGenerations)
                .Append("|").Append(F(x.MeanSuitability)).Append("|").Append(x.Persistent).Append("|").Append(x.Authority).AppendLine();
        foreach (var x in debt.OrderBy(x => x.LineageId, StringComparer.Ordinal).ThenBy(x => x.ZoneId, StringComparer.Ordinal))
            b.Append("D|").Append(x.LineageId).Append("|").Append(x.ZoneId).Append("|").Append(x.FirstDebtGeneration).Append("|")
                .Append(x.LastGeneration).Append("|").Append(x.ConsecutiveDebtGenerations).Append("|").Append(F(x.DebtScore)).Append("|")
                .Append(x.Realized).Append("|").Append(x.RealizedGeneration?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).AppendLine();
        foreach (var x in landscapeEvents.OrderBy(x => x.Generation).ThenBy(x => x.EventId, StringComparer.Ordinal))
            b.Append("G|").Append(x.EventId).Append("|").Append(x.ZoneId).Append("|").Append(x.Kind).Append("|").Append(x.Generation)
                .Append("|").Append(F(x.PreviousValue)).Append("|").Append(F(x.CurrentValue)).Append("|").Append(F(x.Delta)).AppendLine();
        foreach (string item in missing.OrderBy(x => x, StringComparer.Ordinal))
            b.Append("M|").Append(item).AppendLine();
        AppendEvidence(b, evidence);
        return Hash(b.ToString());
    }

    public static string HashWorld(
        int generation,
        IEnumerable<PaleogeographicCellState> cells,
        ReconstructionCompleteness completeness,
        IEnumerable<string> missing,
        IEnumerable<HistoricalEvidenceReference> evidence)
    {
        var b = new StringBuilder();
        b.Append("playback-world/21|").Append(generation).Append("|").Append(completeness).AppendLine();
        foreach (var cell in cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
            b.Append("C|").Append(cell.ZoneId).Append("|").Append(cell.IntegrityHash).AppendLine();
        foreach (string item in missing.OrderBy(x => x, StringComparer.Ordinal))
            b.Append("M|").Append(item).AppendLine();
        AppendEvidence(b, evidence);
        return Hash(b.ToString());
    }

    public static string HashDelta(int from, int to, IEnumerable<HistoricalCellDelta> cells)
    {
        var b = new StringBuilder();
        b.Append("playback-delta/21|").Append(from).Append("|").Append(to).AppendLine();
        foreach (var x in cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            b.Append("C|").Append(x.ZoneId).Append("|").Append(N(x.TemperatureDelta)).Append("|").Append(N(x.MoistureDelta))
                .Append("|").Append(N(x.ForestCoverDelta)).Append("|").Append(N(x.WetlandExtentDelta)).Append("|")
                .Append(N(x.RiverConnectivityDelta)).Append("|").Append(N(x.CoastalExposureDelta)).Append("|").Append(N(x.TerrainIntegrityDelta)).AppendLine();
            foreach (var lineage in x.GainedLineages.OrderBy(v => v, StringComparer.Ordinal)) b.Append("+|").Append(lineage).AppendLine();
            foreach (var lineage in x.LostLineages.OrderBy(v => v, StringComparer.Ordinal)) b.Append("-|").Append(lineage).AppendLine();
        }
        return Hash(b.ToString());
    }

    public static string HashRuntime(IEnumerable<HistoricalWorldStateSnapshot> worlds)
    {
        var b = new StringBuilder("playback-runtime/21\n");
        foreach (var x in worlds.OrderBy(x => x.Generation).ThenBy(x => x.IntegrityHash, StringComparer.Ordinal))
            b.Append(x.Generation).Append("|").Append(x.IntegrityHash).AppendLine();
        return Hash(b.ToString());
    }

    private static void AppendClimate(StringBuilder b, ClimateZoneState? x)
    {
        if (x is null) { b.AppendLine("CLIMATE|null"); return; }
        b.Append("CLIMATE|").Append(x.Generation).Append("|").Append(x.ZoneId).Append("|").Append(x.Regime).Append("|")
            .Append(F(x.Temperature)).Append("|").Append(F(x.Moisture)).Append("|").Append(F(x.Storminess)).Append("|")
            .Append(F(x.Seasonality)).Append("|").Append(F(x.ResonanceBackground)).Append("|").Append(F(x.ClimateStability)).Append("|")
            .Append(string.Join(",", x.ActiveEpochIds.OrderBy(v => v, StringComparer.Ordinal))).AppendLine();
    }

    private static void AppendLandscape(StringBuilder b, LandscapeZoneState? x)
    {
        if (x is null) { b.AppendLine("LANDSCAPE|null"); return; }
        b.Append("LANDSCAPE|").Append(x.Generation).Append("|").Append(x.ZoneId).Append("|").Append(F(x.ForestCover)).Append("|")
            .Append(F(x.WetlandExtent)).Append("|").Append(F(x.RiverConnectivity)).Append("|").Append(F(x.CoastalExposure)).Append("|")
            .Append(F(x.TerrainIntegrity)).Append("|").Append(F(x.DisturbanceLoad)).Append("|")
            .Append(string.Join(",", x.ActiveDisturbanceIds.OrderBy(v => v, StringComparer.Ordinal))).AppendLine();
    }

    private static void AppendBiodiversity(StringBuilder b, RegionalBiodiversitySnapshot? x)
    {
        if (x is null) { b.AppendLine("BIODIVERSITY|null"); return; }
        b.Append("BIODIVERSITY|").Append(x.Generation).Append("|").Append(x.ZoneId).Append("|").Append(x.LineageRichness).Append("|")
            .Append(x.SpeciesFamilyRichness).Append("|").Append(F(x.ShannonDiversity)).Append("|").Append(x.ApproximatePopulation).Append("|")
            .Append(string.Join(",", x.LineageIds.OrderBy(v => v, StringComparer.Ordinal))).AppendLine();
    }

    private static void AppendEvidence(StringBuilder b, IEnumerable<HistoricalEvidenceReference> evidence)
    {
        foreach (var x in evidence.OrderBy(x => x.Kind).ThenBy(x => x.StartGeneration).ThenBy(x => x.StratumId, StringComparer.Ordinal))
            b.Append("E|").Append(x.Kind).Append("|").Append(x.StratumId).Append("|").Append(x.StartGeneration).Append("|")
                .Append(x.EndGeneration).Append("|").Append(x.IntegrityHash).AppendLine();
    }

    private static string N(double? value) => value.HasValue ? F(value.Value) : "null";
    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Hash(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
