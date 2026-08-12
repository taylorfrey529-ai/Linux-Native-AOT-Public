using System.Security.Cryptography;
using System.Text;

namespace EasternKingdoms.Simulation;

public sealed class HistoricalTimelineIndexer
{
    public HistoricalTimelineIndex Build(IEnumerable<HistoricalPlaybackFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        var ordered = frames.OrderBy(x => x.FrameIndex).ThenBy(x => x.World.Generation).ToArray();
        if (ordered.Select(x => x.FrameIndex).Distinct().Count() != ordered.Length)
            throw new InvalidOperationException("Timeline frame indices must be unique.");
        if (ordered.Select(x => x.World.Generation).Distinct().Count() != ordered.Length)
            throw new InvalidOperationException("Timeline generations must be unique.");
        if (ordered.Any(x => !HistoricalPlaybackVerifier.Verify(x.World)))
            throw new InvalidOperationException("Timeline indexing requires verified historical snapshots.");

        var entries = ordered.Select(x => new HistoricalTimelineIndexEntry(
            x.FrameIndex,
            x.World.Generation,
            x.World.IntegrityHash,
            x.World.Completeness,
            Array.AsReadOnly(x.World.Cells.Select(c => c.ZoneId).OrderBy(v => v, StringComparer.Ordinal).ToArray()),
            x.World.Evidence.Count)).ToArray();

        return new HistoricalTimelineIndex(Array.AsReadOnly(entries), Hash(entries));
    }

    public bool Verify(HistoricalTimelineIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        if (index.Entries.Select(x => x.FrameIndex).Distinct().Count() != index.Entries.Count) return false;
        if (index.Entries.Select(x => x.Generation).Distinct().Count() != index.Entries.Count) return false;
        if (index.Entries.Any(x => x.FrameIndex < 0 || x.Generation < 0 || x.EvidenceCount < 0 || string.IsNullOrWhiteSpace(x.WorldIntegrityHash))) return false;
        if (index.Entries.Any(x => x.ZoneIds.Distinct(StringComparer.Ordinal).Count() != x.ZoneIds.Count)) return false;
        var ordered = index.Entries.OrderBy(x => x.FrameIndex).ThenBy(x => x.Generation).ToArray();
        if (!index.Entries.SequenceEqual(ordered)) return false;
        return StringComparer.Ordinal.Equals(index.IntegrityHash, Hash(index.Entries));
    }

    private static string Hash(IEnumerable<HistoricalTimelineIndexEntry> entries)
    {
        var b = new StringBuilder("timeline-index/22\n");
        foreach (var x in entries.OrderBy(x => x.FrameIndex).ThenBy(x => x.Generation))
        {
            b.Append(x.FrameIndex).Append('|').Append(x.Generation).Append('|').Append(x.WorldIntegrityHash).Append('|')
                .Append(x.Completeness).Append('|').Append(x.EvidenceCount).Append('|')
                .Append(string.Join(",", x.ZoneIds.OrderBy(v => v, StringComparer.Ordinal))).AppendLine();
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.ToString()))).ToLowerInvariant();
    }
}
