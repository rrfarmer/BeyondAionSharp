using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerStartMentoringEvent (ATracer) : AlwaysTrueTeamEvent. Java `implements Consumer&lt;Player&gt;`
/// -> C# plain Accept(Player) method passed to ForEach as a method group (group.forEach(this) -> group.ForEach(Accept)). Predicates.Players
/// .canBeMentoredBy / AuditLogger / SM_* red-tolerated.
/// </summary>
public class PlayerStartMentoringEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerGroup group;
    private readonly Player player;

    public PlayerStartMentoringEvent(PlayerGroup group, Player player)
    {
        this.group = group;
        this.player = player;
    }

    public override void HandleEvent()
    {
        if (group.FilterMembers(Predicates.Players.CanBeMentoredBy(player)).Count == 0)
        {
            AuditLogger.Log(player, "sent fake start mentoring packet");
            return;
        }
        player.SetMentor(true);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_START());
        group.ForEach(Accept);
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_ABYSS_RANK_UPDATE(2, player));
    }

    public void Accept(Player member)
    {
        if (!player.Equals(member))
        {
            PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_START_PARTYMSG(player.GetName()));
        }
        PacketSendUtility.SendPacket(member, new SM_GROUP_MEMBER_INFO(group, player, GroupEvent.MOVEMENT));
    }
}
