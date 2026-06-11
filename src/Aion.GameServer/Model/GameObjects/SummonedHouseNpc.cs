using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.House;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/SummonedHouseNpc (Rolandas) : SummonedObject&lt;House&gt;. getType(Creature)→GetTypeValue(Creature); always-friend non-enemy. NpcController/EffectController/House/PlayerAwareKnownList red-tolerated.</summary>
public class SummonedHouseNpc : SummonedObject<House.House>
{
    public SummonedHouseNpc(NpcController controller, SpawnTemplate spawnTemplate, House.House house)
        : base(controller, spawnTemplate, DataManager.NPC_DATA.GetNpcTemplate(spawnTemplate.GetNpcId()).GetLevel(), house)
    {
        string masterName = house.GetOwnerName();
        SetMasterName(masterName == null ? "" : masterName);
        SetKnownlist(new PlayerAwareKnownList(this));
        SetEffectController(new EffectController(this));
    }

    public override int GetCreatorId()
    {
        return GetCreator().GetAddress().GetId();
    }

    public override bool IsEnemy(Creature creature)
    {
        return false;
    }

    public override bool IsEnemyFrom(Npc npc)
    {
        return false;
    }

    public override bool IsEnemyFrom(Player player)
    {
        return false;
    }

    public override CreatureType GetTypeValue(Creature creature)
    {
        return CreatureType.FRIEND;
    }
}
