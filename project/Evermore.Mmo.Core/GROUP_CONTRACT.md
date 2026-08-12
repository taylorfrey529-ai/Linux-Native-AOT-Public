# Deterministic MMORPG Group Contract

Revision 21 implements a bounded C# party/raid state kernel. It replays authored invitations, acceptance/decline, member removal/leave, role assignment, leadership transfer, and ready checks in stable action order.

- Authority is `TestHistoryOnly`.
- Group kinds are fixed at parties of at most 5 members and raids of at most 40 members.
- Every character identifier and display name is authored input; the core does not authenticate identities or grant entitlements.
- Membership and leadership changes are rejected while a ready check is active.
- Serialization uses source-generated `System.Text.Json`, SHA-256 integrity, and full semantic replay.
- Region membership is limited to Eversong, Ghostlands, Eastern Plaguelands, and Western Plaguelands by identifier only.

Friends, ignore lists, guilds, chat, calendars, communities, presence, moderation, persistence, networking, matchmaking, server allocation, and proprietary protocols are not implemented by this contract.
