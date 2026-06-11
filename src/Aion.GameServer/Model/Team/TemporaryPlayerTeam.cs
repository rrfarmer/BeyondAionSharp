using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Team.Common.Legacy;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Collections;

namespace Aion.GameServer.Model.Team;

/// <summary>Java parity: model/team/TemporaryPlayerTeam (ATracer). extends GeneralTeam&lt;Player, TM&gt;.</summary>
public abstract class TemporaryPlayerTeam<TM> : GeneralTeam<Aion.GameServer.Model.GameObjects.Players.Player, TM>
    where TM : class, ITeamMember<Aion.GameServer.Model.GameObjects.Players.Player>
{
    private LootGroupRules lootGroupRules = new LootGroupRules();
    protected readonly ConcurrentDictionary<int, int> targetIdsByBrandId = new();

    public TemporaryPlayerTeam(int objId, bool autoReleaseObjectId)
        : base(objId, autoReleaseObjectId)
    {
    }

    /// <summary>Level of the player with lowest exp.</summary>
    public abstract int GetMinExpPlayerLevel();

    /// <summary>Level of the player with highest exp.</summary>
    public abstract int GetMaxExpPlayerLevel();

    public void UpdateBrand(int brandId, int targetObjectId)
    {
        targetIdsByBrandId[brandId] = targetObjectId;
        SendPackets(new Aion.GameServer.Network.Aion.ServerPackets.SmShowBrand(brandId, targetObjectId));
    }

    public void SendBrands(Aion.GameServer.Model.GameObjects.Players.Player member)
    {
        PacketSendUtility.SendPacket(member, new Aion.GameServer.Network.Aion.ServerPackets.SmShowBrand(targetIdsByBrandId));
    }

    public override Race GetRace()
    {
        return GetLeader().GetObject().GetRace();
    }

    public override void SendPackets(params Aion.GameServer.Network.Aion.GameServerPacket[] packets)
    {
        SendPacket(Predicates.AlwaysTrue<Aion.GameServer.Model.GameObjects.Players.Player>(), packets);
    }

    public override void SendPacket(Predicate<Aion.GameServer.Model.GameObjects.Players.Player> predicate, params Aion.GameServer.Network.Aion.GameServerPacket[] packets)
    {
        ForEach(player =>
        {
            if (predicate(player))
            {
                foreach (Aion.GameServer.Network.Aion.GameServerPacket packet in packets)
                    PacketSendUtility.SendPacket(player, packet);
            }
        });
    }

    public override List<Aion.GameServer.Model.GameObjects.Players.Player> GetOnlineMembers()
    {
        return FilterMembers(Predicates.Players.ONLINE);
    }

    public override LootGroupRules GetLootGroupRules()
    {
        return lootGroupRules;
    }

    public void SetLootGroupRules(LootGroupRules lootGroupRules)
    {
        this.lootGroupRules = lootGroupRules;
        if (lootGroupRules != null && lootGroupRules.GetLootRule() == LootRuleType.FREEFORALL)
            SendPacket(Predicates.Players.WITH_LOOT_PET, Aion.GameServer.Network.Aion.ServerPackets.SmSystemMessage.STR_MSG_LOOTING_PET_MESSAGE03());
    }
}
