using EasternKingdoms.Simulation;

namespace Evermore.Mmo.Core;

public sealed class MmoGroupEngine
{
    public const int MaximumPartyMembers = 5;
    public const int MaximumRaidMembers = 40;
    public const int MaximumActions = 10_000;
    public const string Authority = MmoSessionEngine.Authority;
    public const string Model = "evermore-deterministic-mmorpg-group-kernel/1.0";
    public const string SpatialAuthority = MmoSessionEngine.SpatialAuthority;
    public const string IdentityAuthority = "authored-character-identifiers-only-no-authentication-or-entitlements";
    public const string TransportAuthority = "ordered-action-replay-only-no-presence-chat-or-network-transport";

    public MmoGroupSnapshot Process(
        MmoGroupInput input,
        CancellationToken cancellationToken = default)
    {
        Validate(input);
        cancellationToken.ThrowIfCancellationRequested();

        var definitions = input.Participants.ToDictionary(
            participant => participant.CharacterId,
            StringComparer.Ordinal);
        MmoGroupParticipantDefinition leaderDefinition = definitions[input.LeaderCharacterId];
        var members = new Dictionary<string, MemberState>(StringComparer.Ordinal)
        {
            [input.LeaderCharacterId] = new(
                leaderDefinition.CharacterId,
                leaderDefinition.DisplayName,
                MmoGroupRole.Unassigned,
                0)
        };
        var invitations = new HashSet<string>(StringComparer.Ordinal);
        var events = new List<MmoGroupEventRecord>();
        ReadyCheckState? readyCheck = null;
        string leaderCharacterId = input.LeaderCharacterId;
        long currentTick = input.StartTick;
        int capacity = GetCapacity(input.Kind);

        foreach (MmoGroupActionInput action in input.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            currentTick = action.Tick;
            switch (action.Kind)
            {
                case MmoGroupActionKind.InviteMember:
                    RequireReadyCheckIdle(readyCheck);
                    RequireLeader(action.ActorCharacterId, leaderCharacterId);
                    if (members.ContainsKey(action.SubjectCharacterId) ||
                        !invitations.Add(action.SubjectCharacterId))
                    {
                        throw new InvalidOperationException($"Character '{action.SubjectCharacterId}' is already a member or invitee.");
                    }
                    if (members.Count + invitations.Count > capacity)
                    {
                        invitations.Remove(action.SubjectCharacterId);
                        throw new InvalidOperationException("The group capacity would be exceeded by this invitation.");
                    }
                    AddEvent(events, action, MmoGroupEventKind.InvitationIssued);
                    break;

                case MmoGroupActionKind.AcceptInvitation:
                    RequireReadyCheckIdle(readyCheck);
                    RequireSelfAction(action);
                    if (!invitations.Remove(action.ActorCharacterId))
                        throw new InvalidOperationException($"Character '{action.ActorCharacterId}' has no pending invitation.");
                    if (members.Count >= capacity)
                        throw new InvalidOperationException("The group capacity was reached before invitation acceptance.");
                    MmoGroupParticipantDefinition accepted = definitions[action.ActorCharacterId];
                    members.Add(
                        accepted.CharacterId,
                        new MemberState(
                            accepted.CharacterId,
                            accepted.DisplayName,
                            MmoGroupRole.Unassigned,
                            action.Sequence + 1));
                    AddEvent(events, action, MmoGroupEventKind.InvitationAccepted);
                    break;

                case MmoGroupActionKind.DeclineInvitation:
                    RequireReadyCheckIdle(readyCheck);
                    RequireSelfAction(action);
                    if (!invitations.Remove(action.ActorCharacterId))
                        throw new InvalidOperationException($"Character '{action.ActorCharacterId}' has no pending invitation.");
                    AddEvent(events, action, MmoGroupEventKind.InvitationDeclined);
                    break;

                case MmoGroupActionKind.RemoveMember:
                    RequireReadyCheckIdle(readyCheck);
                    RequireLeader(action.ActorCharacterId, leaderCharacterId);
                    if (StringComparer.Ordinal.Equals(action.SubjectCharacterId, leaderCharacterId))
                        throw new InvalidOperationException("The group leader cannot remove themselves.");
                    RequireMember(action.SubjectCharacterId, members);
                    members.Remove(action.SubjectCharacterId);
                    AddEvent(events, action, MmoGroupEventKind.MemberRemoved);
                    break;

                case MmoGroupActionKind.LeaveGroup:
                    RequireReadyCheckIdle(readyCheck);
                    RequireSelfAction(action);
                    RequireMember(action.ActorCharacterId, members);
                    if (StringComparer.Ordinal.Equals(action.ActorCharacterId, leaderCharacterId))
                        throw new InvalidOperationException("The group leader must transfer leadership before leaving.");
                    members.Remove(action.ActorCharacterId);
                    AddEvent(events, action, MmoGroupEventKind.MemberLeft);
                    break;

                case MmoGroupActionKind.TransferLeadership:
                    RequireReadyCheckIdle(readyCheck);
                    RequireLeader(action.ActorCharacterId, leaderCharacterId);
                    RequireMember(action.SubjectCharacterId, members);
                    if (StringComparer.Ordinal.Equals(action.SubjectCharacterId, leaderCharacterId))
                        throw new InvalidOperationException("Leadership is already assigned to that character.");
                    leaderCharacterId = action.SubjectCharacterId;
                    AddEvent(events, action, MmoGroupEventKind.LeadershipTransferred);
                    break;

                case MmoGroupActionKind.AssignRole:
                    RequireReadyCheckIdle(readyCheck);
                    RequireLeader(action.ActorCharacterId, leaderCharacterId);
                    MemberState assigned = RequireMember(action.SubjectCharacterId, members);
                    members[action.SubjectCharacterId] = assigned with { Role = action.Role };
                    AddEvent(events, action, MmoGroupEventKind.RoleAssigned);
                    break;

                case MmoGroupActionKind.StartReadyCheck:
                    RequireLeader(action.ActorCharacterId, leaderCharacterId);
                    RequireSelfAction(action);
                    RequireReadyCheckIdle(readyCheck);
                    readyCheck = new ReadyCheckState(
                        action.ActorCharacterId,
                        action.Tick,
                        members.Keys.ToDictionary(
                            characterId => characterId,
                            _ => MmoReadyResponse.Pending,
                            StringComparer.Ordinal));
                    AddEvent(events, action, MmoGroupEventKind.ReadyCheckStarted);
                    break;

                case MmoGroupActionKind.RespondReadyCheck:
                    RequireSelfAction(action);
                    RequireMember(action.ActorCharacterId, members);
                    if (readyCheck is null || readyCheck.Completed)
                        throw new InvalidOperationException("No active ready check is available.");
                    if (readyCheck.Responses[action.ActorCharacterId] != MmoReadyResponse.Pending)
                        throw new InvalidOperationException($"Character '{action.ActorCharacterId}' already answered the ready check.");
                    readyCheck.Responses[action.ActorCharacterId] = action.ReadyResponse;
                    AddEvent(events, action, MmoGroupEventKind.ReadyResponseRecorded);
                    if (readyCheck.Responses.Values.All(response => response != MmoReadyResponse.Pending))
                    {
                        readyCheck.Completed = true;
                        readyCheck.AllReady = readyCheck.Responses.Values.All(response => response == MmoReadyResponse.Ready);
                        AddEvent(
                            events,
                            action,
                            MmoGroupEventKind.ReadyCheckCompleted,
                            input.GroupId,
                            readyCheck.AllReady ? MmoReadyResponse.Ready : MmoReadyResponse.NotReady);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(input), $"Unsupported group action kind: {action.Kind}.");
            }
        }

        MmoReadyCheckSnapshot? readySnapshot = readyCheck is null
            ? null
            : new MmoReadyCheckSnapshot(
                readyCheck.StartedByCharacterId,
                readyCheck.StartedAtTick,
                readyCheck.Completed,
                readyCheck.AllReady,
                readyCheck.Responses
                    .OrderBy(pair => members.TryGetValue(pair.Key, out MemberState? member) ? member.JoinSequence : int.MaxValue)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new MmoReadyCheckResponseRecord(pair.Key, pair.Value))
                    .ToArray());

        return new MmoGroupSnapshot(
            Authority,
            Model,
            AzerothRegionAuthority.Authority,
            SpatialAuthority,
            IdentityAuthority,
            TransportAuthority,
            input,
            currentTick,
            leaderCharacterId,
            members.Values
                .OrderBy(member => member.JoinSequence)
                .ThenBy(member => member.CharacterId, StringComparer.Ordinal)
                .Select(member => new MmoGroupMemberSnapshot(
                    member.CharacterId,
                    member.DisplayName,
                    member.Role,
                    member.JoinSequence,
                    readyCheck is not null && readyCheck.Responses.TryGetValue(member.CharacterId, out MmoReadyResponse response)
                        ? response
                        : MmoReadyResponse.Pending))
                .ToArray(),
            invitations.OrderBy(characterId => characterId, StringComparer.Ordinal).ToArray(),
            readySnapshot,
            events.ToArray());
    }

    private static void AddEvent(
        ICollection<MmoGroupEventRecord> events,
        MmoGroupActionInput action,
        MmoGroupEventKind kind,
        string? subjectCharacterId = null,
        MmoReadyResponse? readyResponse = null) =>
        events.Add(new MmoGroupEventRecord(
            events.Count,
            action.Sequence,
            action.Tick,
            kind,
            action.ActorCharacterId,
            subjectCharacterId ?? action.SubjectCharacterId,
            action.Role,
            readyResponse ?? action.ReadyResponse));

    private static void RequireLeader(string actorCharacterId, string leaderCharacterId)
    {
        if (!StringComparer.Ordinal.Equals(actorCharacterId, leaderCharacterId))
            throw new InvalidOperationException($"Character '{actorCharacterId}' is not the group leader.");
    }

    private static void RequireSelfAction(MmoGroupActionInput action)
    {
        if (!StringComparer.Ordinal.Equals(action.ActorCharacterId, action.SubjectCharacterId))
            throw new InvalidOperationException("The action requires the actor and subject to be the same character.");
    }

    private static MemberState RequireMember(
        string characterId,
        IReadOnlyDictionary<string, MemberState> members)
    {
        if (!members.TryGetValue(characterId, out MemberState? member))
            throw new InvalidOperationException($"Character '{characterId}' is not a group member.");
        return member;
    }

    private static void RequireReadyCheckIdle(ReadyCheckState? readyCheck)
    {
        if (readyCheck is { Completed: false })
            throw new InvalidOperationException("Membership and leadership cannot change during an active ready check.");
    }

    private static int GetCapacity(MmoGroupKind kind) => kind switch
    {
        MmoGroupKind.Party => MaximumPartyMembers,
        MmoGroupKind.Raid => MaximumRaidMembers,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported group kind.")
    };

    private static void Validate(MmoGroupInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateIdentifier(input.GroupId, nameof(input.GroupId));
        ValidateIdentifier(input.LeaderCharacterId, nameof(input.LeaderCharacterId));
        if (input.StartTick < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Group ticks cannot be negative.");
        if (!AzerothRegionAuthority.CreateOrderedZoneIds().Contains(input.RegionZoneId, StringComparer.Ordinal))
            throw new ArgumentException("MMORPG groups are limited to the four owner-negotiated Azeroth regions.", nameof(input));

        int capacity = GetCapacity(input.Kind);
        ArgumentNullException.ThrowIfNull(input.Participants);
        if (input.Participants.Count is < 1 or > MaximumRaidMembers)
            throw new ArgumentOutOfRangeException(nameof(input), "Group participant definitions exceed deterministic bounds.");
        RequireUnique(input.Participants.Select(participant => participant.CharacterId), "participant");
        foreach (MmoGroupParticipantDefinition participant in input.Participants)
        {
            ValidateIdentifier(participant.CharacterId, nameof(input.Participants));
            ValidateDisplayName(participant.DisplayName, nameof(input.Participants));
        }

        var definitions = input.Participants.ToDictionary(
            participant => participant.CharacterId,
            StringComparer.Ordinal);
        if (!definitions.ContainsKey(input.LeaderCharacterId))
            throw new ArgumentException("The initial group leader requires an authored participant definition.", nameof(input));
        if (capacity > MaximumRaidMembers)
            throw new ArgumentOutOfRangeException(nameof(input), "The selected group kind exceeds the maximum capacity.");

        ArgumentNullException.ThrowIfNull(input.Actions);
        if (input.Actions.Count > MaximumActions)
            throw new ArgumentOutOfRangeException(nameof(input), "Too many group actions.");
        long previousTick = input.StartTick;
        for (int index = 0; index < input.Actions.Count; index++)
        {
            MmoGroupActionInput action = input.Actions[index] ??
                throw new ArgumentException("Group actions cannot be null.", nameof(input));
            ValidateIdentifier(action.ActorCharacterId, nameof(input.Actions));
            ValidateIdentifier(action.SubjectCharacterId, nameof(input.Actions));
            if (!definitions.ContainsKey(action.ActorCharacterId) ||
                !definitions.ContainsKey(action.SubjectCharacterId))
            {
                throw new ArgumentException("Every group action actor and subject requires an authored participant definition.", nameof(input));
            }
            if (action.Sequence != index || action.Tick < previousTick)
                throw new ArgumentException("Group actions require contiguous sequence IDs and nondecreasing ticks.", nameof(input));

            bool isRoleAction = action.Kind == MmoGroupActionKind.AssignRole;
            bool isReadyResponse = action.Kind == MmoGroupActionKind.RespondReadyCheck;
            if (isRoleAction != (action.Role != MmoGroupRole.Unassigned) ||
                isReadyResponse != (action.ReadyResponse != MmoReadyResponse.Pending))
            {
                throw new ArgumentException("Only role assignment and ready-response actions may carry their respective values.", nameof(input));
            }
            previousTick = action.Tick;
        }
    }

    private static void RequireUnique(IEnumerable<string> identifiers, string kind)
    {
        string[] values = identifiers.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new ArgumentException($"MMORPG group {kind} identifiers must be nonblank and unique.", kind);
        }
    }

    private static void ValidateIdentifier(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 96 ||
            value.Any(character =>
                !(character is >= 'a' and <= 'z') &&
                !(character is >= '0' and <= '9') &&
                character is not '.' and not '-'))
        {
            throw new ArgumentException("MMORPG identifiers must use lowercase ASCII letters, digits, periods, or hyphens.", name);
        }
    }

    private static void ValidateDisplayName(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 80 || value.Any(char.IsControl))
            throw new ArgumentException("MMORPG display names must be 1..80 printable characters.", name);
    }

    private sealed record MemberState(
        string CharacterId,
        string DisplayName,
        MmoGroupRole Role,
        int JoinSequence);

    private sealed class ReadyCheckState(
        string startedByCharacterId,
        long startedAtTick,
        Dictionary<string, MmoReadyResponse> responses)
    {
        public string StartedByCharacterId { get; } = startedByCharacterId;
        public long StartedAtTick { get; } = startedAtTick;
        public Dictionary<string, MmoReadyResponse> Responses { get; } = responses;
        public bool Completed { get; set; }
        public bool AllReady { get; set; }
    }
}
