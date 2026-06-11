using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>
/// Java parity: model/team/common/events/PlayerStopMentoringEvent (ATracer) : AlwaysTrueTeamEvent. Java &lt;T extends
/// TemporaryPlayerTeam&lt;? extends TeamMember&lt;Player&gt;&gt;&gt; -> where T : class (pillar convention). team.ForEach red-tolerated;
/// SM_SYSTEM_MESSAGE.STR_* catalog deferred/red-tolerated. abstract SendGroupPacketOnMentorEnd resolved by group/alliance subclasses.
/// </summary>
public abstract class PlayerStopMentoringEvent<T> : AlwaysTrueTeamEvent
    where T : class
{
    protected readonly T team;
    protected readonly Player player;

    public PlayerStopMentoringEvent(T team, Player player)
    {
        this.team = team;
        this.player = player;
    }

    public override void HandleEvent()
    {
        player.SetMentor(false);
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_END());
        team.ForEach(member =>
        {
            if (!player.Equals(member))
                PacketSendUtility.SendPacket(member, SM_SYSTEM_MESSAGE.STR_MSG_MENTOR_END_PARTYMSG(player.GetName()));
            SendGroupPacketOnMentorEnd(member);
        });
        PacketSendUtility.BroadcastPacketAndReceive(player, new SM_ABYSS_RANK_UPDATE(2, player));
    }

    protected abstract void SendGroupPacketOnMentorEnd(Player member);
}
