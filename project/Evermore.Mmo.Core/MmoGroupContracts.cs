using System.Text.Json.Serialization;

namespace Evermore.Mmo.Core;

public enum MmoGroupKind
{
    Party,
    Raid
}

public enum MmoGroupRole
{
    Unassigned,
    Tank,
    Healer,
    Damage
}

public enum MmoReadyResponse
{
    Pending,
    Ready,
    NotReady
}

public enum MmoGroupActionKind
{
    InviteMember,
    AcceptInvitation,
    DeclineInvitation,
    RemoveMember,
    LeaveGroup,
    TransferLeadership,
    AssignRole,
    StartReadyCheck,
    RespondReadyCheck
}

public enum MmoGroupEventKind
{
    InvitationIssued,
    InvitationAccepted,
    InvitationDeclined,
    MemberRemoved,
    MemberLeft,
    LeadershipTransferred,
    RoleAssigned,
    ReadyCheckStarted,
    ReadyResponseRecorded,
    ReadyCheckCompleted
}

public sealed record MmoGroupParticipantDefinition(
    [property: JsonPropertyOrder(0), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName);

public sealed record MmoGroupActionInput(
    [property: JsonPropertyOrder(0), JsonRequired] int Sequence,
    [property: JsonPropertyOrder(1), JsonRequired] long Tick,
    [property: JsonPropertyOrder(2), JsonRequired] MmoGroupActionKind Kind,
    [property: JsonPropertyOrder(3), JsonRequired] string ActorCharacterId,
    [property: JsonPropertyOrder(4), JsonRequired] string SubjectCharacterId,
    [property: JsonPropertyOrder(5), JsonRequired] MmoGroupRole Role,
    [property: JsonPropertyOrder(6), JsonRequired] MmoReadyResponse ReadyResponse);

public sealed record MmoGroupInput(
    [property: JsonPropertyOrder(0), JsonRequired] string GroupId,
    [property: JsonPropertyOrder(1), JsonRequired] ulong Seed,
    [property: JsonPropertyOrder(2), JsonRequired] long StartTick,
    [property: JsonPropertyOrder(3), JsonRequired] string RegionZoneId,
    [property: JsonPropertyOrder(4), JsonRequired] MmoGroupKind Kind,
    [property: JsonPropertyOrder(5), JsonRequired] string LeaderCharacterId,
    [property: JsonPropertyOrder(6), JsonRequired] IReadOnlyList<MmoGroupParticipantDefinition> Participants,
    [property: JsonPropertyOrder(7), JsonRequired] IReadOnlyList<MmoGroupActionInput> Actions);

public sealed record MmoGroupMemberSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(1), JsonRequired] string DisplayName,
    [property: JsonPropertyOrder(2), JsonRequired] MmoGroupRole Role,
    [property: JsonPropertyOrder(3), JsonRequired] int JoinSequence,
    [property: JsonPropertyOrder(4), JsonRequired] MmoReadyResponse ReadyResponse);

public sealed record MmoReadyCheckResponseRecord(
    [property: JsonPropertyOrder(0), JsonRequired] string CharacterId,
    [property: JsonPropertyOrder(1), JsonRequired] MmoReadyResponse Response);

public sealed record MmoReadyCheckSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string StartedByCharacterId,
    [property: JsonPropertyOrder(1), JsonRequired] long StartedAtTick,
    [property: JsonPropertyOrder(2), JsonRequired] bool Completed,
    [property: JsonPropertyOrder(3), JsonRequired] bool AllReady,
    [property: JsonPropertyOrder(4), JsonRequired] IReadOnlyList<MmoReadyCheckResponseRecord> Responses);

public sealed record MmoGroupEventRecord(
    [property: JsonPropertyOrder(0), JsonRequired] int Sequence,
    [property: JsonPropertyOrder(1), JsonRequired] int ActionSequence,
    [property: JsonPropertyOrder(2), JsonRequired] long Tick,
    [property: JsonPropertyOrder(3), JsonRequired] MmoGroupEventKind Kind,
    [property: JsonPropertyOrder(4), JsonRequired] string ActorCharacterId,
    [property: JsonPropertyOrder(5), JsonRequired] string SubjectCharacterId,
    [property: JsonPropertyOrder(6), JsonRequired] MmoGroupRole Role,
    [property: JsonPropertyOrder(7), JsonRequired] MmoReadyResponse ReadyResponse);

public sealed record MmoGroupSnapshot(
    [property: JsonPropertyOrder(0), JsonRequired] string Authority,
    [property: JsonPropertyOrder(1), JsonRequired] string Model,
    [property: JsonPropertyOrder(2), JsonRequired] string RegionAuthority,
    [property: JsonPropertyOrder(3), JsonRequired] string SpatialAuthority,
    [property: JsonPropertyOrder(4), JsonRequired] string IdentityAuthority,
    [property: JsonPropertyOrder(5), JsonRequired] string TransportAuthority,
    [property: JsonPropertyOrder(6), JsonRequired] MmoGroupInput Input,
    [property: JsonPropertyOrder(7), JsonRequired] long FinalTick,
    [property: JsonPropertyOrder(8), JsonRequired] string LeaderCharacterId,
    [property: JsonPropertyOrder(9), JsonRequired] IReadOnlyList<MmoGroupMemberSnapshot> Members,
    [property: JsonPropertyOrder(10), JsonRequired] IReadOnlyList<string> PendingInvitations,
    [property: JsonPropertyOrder(11), JsonRequired] MmoReadyCheckSnapshot? ReadyCheck,
    [property: JsonPropertyOrder(12), JsonRequired] IReadOnlyList<MmoGroupEventRecord> Events);

public sealed record MmoGroupEnvelope(
    [property: JsonPropertyOrder(0), JsonRequired] string SchemaVersion,
    [property: JsonPropertyOrder(1), JsonRequired] string Authority,
    [property: JsonPropertyOrder(2), JsonRequired] string PayloadSha256,
    [property: JsonPropertyOrder(3), JsonRequired] MmoGroupSnapshot Payload);
