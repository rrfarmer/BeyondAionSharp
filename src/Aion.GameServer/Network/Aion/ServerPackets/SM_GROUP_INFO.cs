using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_GROUP_INFO (Lyahim, ATracer, xTz). Group summary: id/leader/loot rules/team type. getType()->GetType_() collision. PlayerGroup/LootGroupRules/TeamType red-tolerated.</summary>
public class SM_GROUP_INFO : AionServerPacket
{
    private readonly LootGroupRules lootRules;
    private readonly int groupId;
    private readonly int leaderId;
    private readonly TeamType type;

    public SM_GROUP_INFO(PlayerGroup group)
    {
        groupId = group.GetObjectId();
        leaderId = group.GetLeader().GetObjectId();
        lootRules = group.GetLootGroupRules();
        type = group.GetTeamType();
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        WriteD(groupId);
        WriteD(leaderId);
        WriteD(player == null || player.GetPosition() == null ? 0 : player.GetWorldId());// mapId
        WriteD(lootRules.GetLootRule().GetId());
        WriteD(lootRules.GetMisc());
        WriteD(lootRules.GetCommonItemAbove());
        WriteD(lootRules.GetSuperiorItemAbove());
        WriteD(lootRules.GetHeroicItemAbove());
        WriteD(lootRules.GetFabledItemAbove());
        WriteD(lootRules.GetEternalItemAbove());
        WriteD(lootRules.GetMythicItemAbove());
        WriteD(0x02);
        WriteC(0x00);
        WriteD(type.GetType_());
        WriteD(type.GetSubType());
        WriteD(0x00); // message id
        WriteS(""); // name
    }
}
