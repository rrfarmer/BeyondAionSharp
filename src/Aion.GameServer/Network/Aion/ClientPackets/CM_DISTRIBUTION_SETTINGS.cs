using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Model.Team.League;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DISTRIBUTION_SETTINGS (Lyahim, Simple, xTz). Sets group/alliance/league loot distribution rules + per-grade thresholds. PlayerGroupService/LeagueService/PlayerAllianceService red-tolerated.</summary>
public class CM_DISTRIBUTION_SETTINGS : AionClientPacket
{
    private int isLeague;
    private int lootRule;
    private int misc;
    private LootRuleType lootRules;
    private int commonItemAbove;
    private int superiorItemAbove;
    private int heroicItemAbove;
    private int fabledItemAbove;
    private int ethernalItemAbove;
    private int mythicItemAbove;
    private int unk;

    public CM_DISTRIBUTION_SETTINGS(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        isLeague = ReadD();
        lootRule = ReadD();
        switch (lootRule)
        {
            case 0:
                lootRules = LootRuleType.FREEFORALL;
                break;
            case 1:
                lootRules = LootRuleType.ROUNDROBIN;
                break;
            case 2:
                lootRules = LootRuleType.LEADER;
                break;
            default:
                lootRules = LootRuleType.FREEFORALL;
                break;
        }
        misc = ReadD();
        commonItemAbove = ReadD();
        superiorItemAbove = ReadD();
        heroicItemAbove = ReadD();
        fabledItemAbove = ReadD();
        ethernalItemAbove = ReadD();
        mythicItemAbove = ReadD();
        unk = ReadD();
    }

    protected override void RunImpl()
    {
        Player leader = GetConnection().GetActivePlayer();

        PlayerGroup group = leader.GetPlayerGroup();
        if (group != null)
        {
            PlayerGroupService.ChangeGroupRules(group, new LootGroupRules(lootRules, misc, commonItemAbove, superiorItemAbove, heroicItemAbove,
                fabledItemAbove, ethernalItemAbove, mythicItemAbove));
        }
        PlayerAlliance alliance = leader.GetPlayerAlliance();
        if (alliance != null)
        {
            if (alliance.IsInLeague())
                LeagueService.ChangeGroupRules(alliance.GetLeague(), new LootGroupRules(lootRules, misc, commonItemAbove, superiorItemAbove, heroicItemAbove,
                    fabledItemAbove, ethernalItemAbove, mythicItemAbove));
            else
                PlayerAllianceService.ChangeGroupRules(alliance, new LootGroupRules(lootRules, misc, commonItemAbove, superiorItemAbove, heroicItemAbove,
                    fabledItemAbove, ethernalItemAbove, mythicItemAbove));
        }
    }
}
