using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

internal static class HistoricalVisualizationHasher
{
    public static string HashOverlay(HistoricalOverlayMetric metric, int fromGeneration, int toGeneration, IEnumerable<HistoricalOverlayCell> cells)
    {
        var b = new StringBuilder();
        b.Append("historical-overlay/22|").Append(metric).Append('|').Append(fromGeneration).Append('|').Append(toGeneration).AppendLine();
        foreach (var x in cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
            b.Append(x.ZoneId).Append('|').Append(N(x.FromValue)).Append('|').Append(N(x.ToValue)).Append('|')
                .Append(N(x.Delta)).Append('|').Append(x.Direction).AppendLine();
        return Hash(b.ToString());
    }

    public static string HashGlobe(int generation, GlobeLayoutPolicy layoutPolicy, IEnumerable<HistoricalGlobeCellState> cells)
    {
        var b = new StringBuilder();
        b.Append("historical-globe/22|").Append(generation).Append('|').Append(layoutPolicy).AppendLine();
        foreach (var x in cells.OrderBy(x => x.ZoneId, StringComparer.Ordinal))
        {
            b.Append(x.ZoneId).Append('|').Append(x.RenderAnchorKey).Append('|').Append(x.ClimateRegime?.ToString() ?? "null").Append('|')
                .Append(N(x.Temperature)).Append('|').Append(N(x.Moisture)).Append('|').Append(N(x.ForestCover)).Append('|')
                .Append(N(x.WetlandExtent)).Append('|').Append(N(x.RiverConnectivity)).Append('|').Append(N(x.CoastalExposure)).Append('|')
                .Append(N(x.TerrainIntegrity)).Append('|').Append(x.LineageRichness?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|')
                .Append(x.SpeciesFamilyRichness?.ToString(CultureInfo.InvariantCulture) ?? "null").Append('|').Append(x.Completeness).Append('|')
                .Append(x.SourceIntegrityHash).AppendLine();
        }
        return Hash(b.ToString());
    }

    private static string N(double? value) => value.HasValue ? value.Value.ToString("R", CultureInfo.InvariantCulture) : "null";
    private static string Hash(string canonical) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
}
