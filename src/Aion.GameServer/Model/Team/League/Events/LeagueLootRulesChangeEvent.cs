using Aion.GameServer.Model.Team.Common.Events;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Model.Team.Common.Legacy;

namespace Aion.GameServer.Model.Team.League.Events;

/// <summary>Java parity: model/team/league/events/LeagueLootRulesChangeEvent.</summary>
public class LeagueLootRulesChangeEvent : AlwaysTrueTeamEvent
{
    private readonly League league;
    private readonly LootGroupRules lootGroupRules;

    public LeagueLootRulesChangeEvent(League league, LootGroupRules lootGroupRules)
    {
        this.league = league;
        this.lootGroupRules = lootGroupRules;
    }

    public override void HandleEvent()
    {
        league.SetLootGroupRules(lootGroupRules);
        league.ForEach(alliance => alliance.SendPackets(new SmAllianceInfo(alliance)));
    }
}
