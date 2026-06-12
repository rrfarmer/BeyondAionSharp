using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Event;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Model.Team.Common.Events;

/// <summary>
/// Java parity: model/team/common/events/PlayerLeavedEvent (ATracer). Base for "player left a team" events. Java is already two-param
/// (&lt;TM extends TeamMember&lt;Player&gt;, T extends TemporaryPlayerTeam&lt;TM&gt;&gt;) -> direct C# map (no wildcard divergence), so
/// team.HasMember resolves. TeamEvent -> ITeamEvent; nested enum LeaveReson SCREAMING (kept). schedule(Runnable, 30000) -> async
/// ThreadPoolManager idiom (ct => {...; return ValueTask.CompletedTask;}, TimeSpan). TaskId.INSTANCE_KICK -> TaskId.INSTANCE_KICK.
/// SM_*/InstanceService/EventService red-tolerated.
/// </summary>
public abstract class PlayerLeavedEvent<TM, T> : ITeamEvent
    where TM : class, ITeamMember<Player>
    where T : TemporaryPlayerTeam<TM>
{
    public enum LeaveReson
    {
        BAN,
        LEAVE,
        LEAVE_TIMEOUT,
        DISBAND
    }

    protected readonly T team;
    protected readonly Player leavedPlayer;
    protected readonly LeaveReson reason;
    protected readonly string banPersonName;

    public PlayerLeavedEvent(T team, Player player)
        : this(team, player, LeaveReson.LEAVE)
    {
    }

    public PlayerLeavedEvent(T team, Player player, LeaveReson reason)
        : this(team, player, reason, "")
    {
    }

    public PlayerLeavedEvent(T team, Player player, LeaveReson reason, string banPersonName)
    {
        this.team = team;
        this.leavedPlayer = player;
        this.reason = reason;
        this.banPersonName = banPersonName;
    }

    /// <summary>Player should be in team to broadcast this event</summary>
    public virtual bool CheckCondition()
    {
        return team.HasMember(leavedPlayer.GetObjectId());
    }

    public virtual void HandleEvent()
    {
        if (leavedPlayer.IsOnline())
        {
            PacketSendUtility.SendPacket(leavedPlayer, new SM_LEAVE_GROUP_MEMBER());
            if (team.Equals(leavedPlayer.GetPosition().GetWorldMapInstance().GetRegisteredTeam()))
            {
                PacketSendUtility.SendPacket(leavedPlayer, SM_SYSTEM_MESSAGE.STR_MSG_LEAVE_INSTANCE_NOT_PARTY());
                leavedPlayer.GetController().AddTask(TaskId.INSTANCE_KICK, ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    if (leavedPlayer.GetCurrentTeamId() != team.GetObjectId())
                    {
                        if (team.Equals(leavedPlayer.GetPosition().GetWorldMapInstance().GetRegisteredTeam()))
                            InstanceService.MoveToExitPoint(leavedPlayer);
                    }
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(30000)));
            }
        }
        EventService.GetInstance().OnLeftTeam(leavedPlayer, team);
    }
}
