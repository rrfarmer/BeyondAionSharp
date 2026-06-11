using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Model.Team.Common.Legacy;

namespace Aion.GameServer.Model.Team.Alliance.Events;

/// <summary>Java parity: model/team/alliance/events/ChangeAllianceLootRulesEvent.</summary>
public class ChangeAllianceLootRulesEvent : AlwaysTrueTeamEvent
{
    private readonly PlayerAlliance alliance;
    private readonly LootGroupRules lootGroupRules;

    public ChangeAllianceLootRulesEvent(PlayerAlliance alliance, LootGroupRules lootGroupRules)
    {
        this.alliance = alliance;
        this.lootGroupRules = lootGroupRules;
    }

    public override void HandleEvent()
    {
        alliance.SetLootGroupRules(lootGroupRules);
        alliance.SendPackets(new SmAllianceInfo(alliance));
    }
}
