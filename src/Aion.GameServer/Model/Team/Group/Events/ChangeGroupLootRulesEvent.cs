using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Model.Team.Common.Legacy;

namespace Aion.GameServer.Model.Team.Group.Events;

/// <summary>Java parity: model/team/group/events/ChangeGroupLootRulesEvent.</summary>
public class ChangeGroupLootRulesEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerGroup group;
    private readonly LootGroupRules lootGroupRules;

    public ChangeGroupLootRulesEvent(PlayerGroup group, LootGroupRules lootGroupRules)
    {
        this.group = group;
        this.lootGroupRules = lootGroupRules;
    }

    public override void HandleEvent()
    {
        group.SetLootGroupRules(lootGroupRules);
        group.SendPackets(new SmGroupInfo(group));
    }
}
