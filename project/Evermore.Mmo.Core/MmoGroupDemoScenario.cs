using EasternKingdoms.Simulation;

namespace Evermore.Mmo.Core;

public static class MmoGroupDemoScenario
{
    public static MmoGroupInput Create() =>
        new(
            "group.eversong-vanguard",
            529,
            0,
            AzerothRegionAuthority.EversongZoneId,
            MmoGroupKind.Party,
            "character.azeroth-warden",
            [
                new MmoGroupParticipantDefinition("character.azeroth-warden", "Azeroth Warden"),
                new MmoGroupParticipantDefinition("character.lunara-healer", "Lunara Healer"),
                new MmoGroupParticipantDefinition("character.solune-ranger", "Solune Ranger")
            ],
            [
                Action(0, 0, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.lunara-healer"),
                Action(1, 0, MmoGroupActionKind.AcceptInvitation, "character.lunara-healer", "character.lunara-healer"),
                Action(2, 1, MmoGroupActionKind.InviteMember, "character.azeroth-warden", "character.solune-ranger"),
                Action(3, 1, MmoGroupActionKind.AcceptInvitation, "character.solune-ranger", "character.solune-ranger"),
                Action(4, 2, MmoGroupActionKind.AssignRole, "character.azeroth-warden", "character.azeroth-warden", MmoGroupRole.Tank),
                Action(5, 2, MmoGroupActionKind.AssignRole, "character.azeroth-warden", "character.lunara-healer", MmoGroupRole.Healer),
                Action(6, 2, MmoGroupActionKind.AssignRole, "character.azeroth-warden", "character.solune-ranger", MmoGroupRole.Damage),
                Action(7, 3, MmoGroupActionKind.StartReadyCheck, "character.azeroth-warden", "character.azeroth-warden"),
                Action(8, 4, MmoGroupActionKind.RespondReadyCheck, "character.azeroth-warden", "character.azeroth-warden", ready: MmoReadyResponse.Ready),
                Action(9, 4, MmoGroupActionKind.RespondReadyCheck, "character.lunara-healer", "character.lunara-healer", ready: MmoReadyResponse.Ready),
                Action(10, 4, MmoGroupActionKind.RespondReadyCheck, "character.solune-ranger", "character.solune-ranger", ready: MmoReadyResponse.Ready),
                Action(11, 5, MmoGroupActionKind.TransferLeadership, "character.azeroth-warden", "character.lunara-healer"),
                Action(12, 6, MmoGroupActionKind.LeaveGroup, "character.solune-ranger", "character.solune-ranger")
            ]);

    private static MmoGroupActionInput Action(
        int sequence,
        long tick,
        MmoGroupActionKind kind,
        string actor,
        string subject,
        MmoGroupRole role = MmoGroupRole.Unassigned,
        MmoReadyResponse ready = MmoReadyResponse.Pending) =>
        new(sequence, tick, kind, actor, subject, role, ready);
}
