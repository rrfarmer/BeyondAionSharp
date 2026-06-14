using Aion.GameServer.Model.Team.Common.Events;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>Java parity: model/team/group/events/GroupDisbandEvent.</summary>
public class GroupDisbandEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerGroup group;

    public GroupDisbandEvent(PlayerGroup group)
    {
        this.group = group;
    }

    public override void HandleEvent()
    {
        group.ForEach(member => group.OnEvent(new PlayerGroupLeavedEvent(group, member, LeaveReson.DISBAND)));
    }
}
