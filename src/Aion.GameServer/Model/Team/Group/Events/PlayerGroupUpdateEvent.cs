using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>
/// Java parity: model/team/group/events/PlayerGroupUpdateEvent (ATracer) : AlwaysTrueTeamEvent. Broadcasts a SM_GROUP_MEMBER_INFO to
/// all members except the subject. Predicates.Players.allExcept -> AllExcept; GroupEvent SCREAMING (legacy). group.SendPacket red-tolerated.
/// </summary>
public class PlayerGroupUpdateEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerGroup group;
    private readonly Player player;
    private readonly GroupEvent groupEvent;
    private readonly int slot;

    public PlayerGroupUpdateEvent(PlayerGroup group, Player player, GroupEvent groupEvent, int slot)
    {
        this.group = group;
        this.player = player;
        this.groupEvent = groupEvent;
        this.slot = slot;
    }

    public PlayerGroupUpdateEvent(PlayerGroup group, Player player, GroupEvent groupEvent)
        : this(group, player, groupEvent, 0)
    {
    }

    public override void HandleEvent()
    {
        group.SendPacket(Predicates.Players.AllExcept(player), new SM_GROUP_MEMBER_INFO(group, player, groupEvent, slot));
    }
}
