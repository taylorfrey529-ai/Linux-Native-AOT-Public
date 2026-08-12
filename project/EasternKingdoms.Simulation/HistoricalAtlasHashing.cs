using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

internal static class HistoricalAtlasHasher
{
    public static string HashScene(
        int frameIndex,
        int generation,
        HistoricalAtlasCameraState camera,
        IEnumerable<HistoricalAtlasLayerState> layers,
        IEnumerable<HistoricalAtlasCellPayload> cells,
        IEnumerable<HistoricalAtlasLineageRange> ranges,
        IEnumerable<HistoricalAtlasProvenancePanel> provenance,
        string sourceWorldIntegrityHash)
    {
        var b = new StringBuilder();
        b.Append("historical-atlas-scene/23|").Append(frameIndex).Append('|').Append(generation).Append('|')
            .Append(sourceWorldIntegrityHash).AppendLine();
        AppendCamera(b, camera);
        foreach (var layer in layers.OrderBy(x => x.Layer))
            b.Append("layer|").Append(layer.Layer).Append('|').Append(layer.Enabled).Append('|').Append(N(layer.Opacity)).AppendLine();
        foreach (var cell in cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            b.Append("cell|").Append(cell.ZoneId).Append('|').Append(cell.RenderAnchorKey).Append('|').Append(cell.Generation).Append('|')
                .Append(cell.ClimateRegime?.ToString() ?? "null").Append('|').Append(N(cell.Temperature)).Append('|').Append(N(cell.Moisture)).Append('|')
                .Append(N(cell.ForestCover)).Append('|').Append(N(cell.WetlandExtent)).Append('|').Append(N(cell.RiverConnectivity)).Append('|')
                .Append(N(cell.CoastalExposure)).Append('|').Append(N(cell.TerrainIntegrity)).Append('|')
                .Append(cell.LineageRichness?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|')
                .Append(cell.SpeciesFamilyRichness?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|')
                .Append(cell.ApproximatePopulation?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|')
                .Append(cell.Completeness).Append('|').Append(cell.SourceIntegrityHash).Append('|')
                .Append(string.Join(',', cell.LivingLineageIds.OrderBy(x => x, StringComparer.Ordinal))).AppendLine();
            foreach (var r in cell.Refugia.OrderBy(x => x.RefugiumId, StringComparer.Ordinal))
                b.Append("refugium|").Append(cell.ZoneId).Append('|').Append(r.RefugiumId).Append('|').Append(r.LineageId).Append('|')
                    .Append(r.Persistent).Append('|').Append(N(r.MeanSuitability)).Append('|').Append(r.FirstObservedGeneration).Append('|')
                    .Append(r.LastObservedGeneration).AppendLine();
            foreach (var d in cell.ExtinctionDebt.OrderBy(x => x.LineageId, StringComparer.Ordinal))
                b.Append("debt|").Append(cell.ZoneId).Append('|').Append(d.LineageId).Append('|').Append(N(d.DebtScore)).Append('|')
                    .Append(d.ConsecutiveDebtGenerations).Append('|').Append(d.Realized).Append('|')
                    .Append(d.RealizedGeneration?.ToString(CultureInfo.InvariantCulture) ?? "null").AppendLine();
        }
        foreach (var range in ranges.OrderBy(x => x.LineageId, StringComparer.Ordinal))
            b.Append("range|").Append(range.LineageId).Append('|').Append(range.SpeciesFamilyId).Append('|')
                .Append(range.Authority).Append('|').Append(range.Status).Append('|').Append(N(range.MeanDivergence)).Append('|')
                .Append(range.ApproximateGlobalPopulation).Append('|')
                .Append(string.Join(',', range.ZoneIds.OrderBy(x => x, StringComparer.Ordinal))).AppendLine();
        foreach (var panel in provenance.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            b.Append("provenance|").Append(panel.ZoneId).Append('|').Append(panel.Generation).Append('|').Append(panel.Completeness).Append('|')
                .Append(panel.SourceIntegrityHash).Append('|').Append(string.Join(',', panel.MissingEvidence.OrderBy(x => x, StringComparer.Ordinal))).AppendLine();
            foreach (var e in panel.Evidence.OrderBy(x => x.Kind).ThenBy(x => x.StratumId, StringComparer.Ordinal))
                b.Append("evidence|").Append(panel.ZoneId).Append('|').Append(e.Kind).Append('|').Append(e.StratumId).Append('|')
                    .Append(e.StartGeneration).Append('|').Append(e.EndGeneration).Append('|').Append(e.IntegrityHash).AppendLine();
        }
        return Hash(b.ToString());
    }

    public static string HashComparison(
        HistoricalAtlasComparisonMode mode,
        HistoricalAtlasScenePayload from,
        HistoricalAtlasScenePayload to,
        IEnumerable<HistoricalChangeOverlay> overlays)
    {
        var b = new StringBuilder("historical-atlas-comparison/23|");
        b.Append(mode).Append('|').Append(from.IntegrityHash).Append('|').Append(to.IntegrityHash).AppendLine();
        foreach (var overlay in overlays.OrderBy(x => x.Metric))
            b.Append(overlay.Metric).Append('|').Append(overlay.FromGeneration).Append('|').Append(overlay.ToGeneration).Append('|')
                .Append(overlay.IntegrityHash).AppendLine();
        return Hash(b.ToString());
    }

    public static string HashSession(
        int frameIndex,
        int generation,
        HistoricalAtlasComparisonMode comparisonMode,
        int? comparisonGeneration,
        HistoricalAtlasCameraState camera,
        IEnumerable<HistoricalAtlasLayerState> layers,
        string? selectedZoneId,
        string? selectedLineageId)
    {
        var b = new StringBuilder("historical-atlas-session/23|");
        b.Append(frameIndex).Append('|').Append(generation).Append('|').Append(comparisonMode).Append('|')
            .Append(comparisonGeneration?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|')
            .Append(selectedZoneId ?? "null").Append('|').Append(selectedLineageId ?? "null").AppendLine();
        AppendCamera(b, camera);
        foreach (var layer in layers.OrderBy(x => x.Layer))
            b.Append(layer.Layer).Append('|').Append(layer.Enabled).Append('|').Append(N(layer.Opacity)).AppendLine();
        return Hash(b.ToString());
    }

    public static string HashCamera(HistoricalAtlasCameraState camera)
    {
        var b = new StringBuilder("historical-atlas-camera/23|");
        AppendCamera(b, camera);
        return Hash(b.ToString());
    }

    private static void AppendCamera(StringBuilder b, HistoricalAtlasCameraState camera) =>
        b.Append("camera|").Append(camera.Projection).Append('|').Append(N(camera.YawDegrees)).Append('|')
            .Append(N(camera.PitchDegrees)).Append('|').Append(N(camera.RollDegrees)).Append('|').Append(N(camera.Zoom)).Append('|')
            .Append(camera.FocusZoneId ?? "null").AppendLine();

    private static string N(double? value) => value.HasValue ? N(value.Value) : "null";
    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Hash(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
