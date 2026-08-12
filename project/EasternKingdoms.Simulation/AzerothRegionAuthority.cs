using System.Text.Json.Serialization;

namespace EasternKingdoms.Simulation;

public sealed record AzerothNegotiatedRegion(
    [property: JsonPropertyOrder(0), JsonRequired] int Ordinal,
    [property: JsonPropertyOrder(1), JsonRequired] string RegionName,
    [property: JsonPropertyOrder(2), JsonRequired] string ZoneId);

public sealed record AzerothRegionAuthoritySnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string World,
    [property: JsonPropertyOrder(1), JsonRequired] string Continent,
    [property: JsonPropertyOrder(2), JsonRequired] string Scope,
    [property: JsonPropertyOrder(3), JsonRequired] string Authority,
    [property: JsonPropertyOrder(4), JsonRequired] string ExpansionPolicy,
    [property: JsonPropertyOrder(5), JsonRequired] string SpatialAuthority,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<AzerothNegotiatedRegion> Regions);

public sealed record AzerothRegionAuthorityEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] AzerothRegionAuthoritySnapshot Payload);

public static class AzerothRegionAuthority
{
    public const string EversongZoneId = "ek.north.eversong";
    public const string GhostlandsZoneId = "ek.north.ghostlands";
    public const string EasternPlaguelandsZoneId = "ek.north.eastern-plaguelands";
    public const string WesternPlaguelandsZoneId = "ek.north.western-plaguelands";
    public const string Authority = "owner-negotiated-four-region-only";
    public const string Scope = "eversong-ghostlands-eastern-western-plaguelands-only";
    public const string Coverage = "authorized-northern-scar-corridor";
    public const string ExpansionPolicy = "owner-explicit-renegotiation-required";
    public const string SpatialAuthority = "region-and-zone-identifiers-only-no-coordinate-or-geometry-authority";
    public const string ObservationSource = "azeroth-negotiated-region-authority";

    public static AzerothRegionAuthoritySnapshot CreateSnapshot() =>
        new(
            "Azeroth",
            "Eastern Kingdoms",
            Scope,
            Authority,
            ExpansionPolicy,
            SpatialAuthority,
            [
                new AzerothNegotiatedRegion(0, "Eversong", EversongZoneId),
                new AzerothNegotiatedRegion(1, "Ghostlands", GhostlandsZoneId),
                new AzerothNegotiatedRegion(2, "Eastern Plaguelands", EasternPlaguelandsZoneId),
                new AzerothNegotiatedRegion(3, "Western Plaguelands", WesternPlaguelandsZoneId)
            ]);

    public static string[] CreateOrderedZoneIds() =>
        CreateSnapshot().Regions
            .Select(region => region.ZoneId)
            .OrderBy(zoneId => zoneId, StringComparer.Ordinal)
            .ToArray();

    public static void RequireExactSet(IEnumerable<string> zoneIds)
    {
        ArgumentNullException.ThrowIfNull(zoneIds);
        string[] supplied = zoneIds.ToArray();
        string[] expected = CreateOrderedZoneIds();
        if (supplied.Length != expected.Length ||
            supplied.Any(string.IsNullOrWhiteSpace) ||
            supplied.Distinct(StringComparer.Ordinal).Count() != supplied.Length ||
            !supplied.OrderBy(zoneId => zoneId, StringComparer.Ordinal)
                .SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Azeroth authority permits exactly Eversong, Ghostlands, Eastern Plaguelands, and Western Plaguelands.",
                nameof(zoneIds));
        }
    }
}

