using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Trap extends SummonedObject&lt;Creature&gt;.</summary>
public class Trap : SummonedObject<Creature>
{
    public Trap(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, Creature creator)
        : base(controller, spawnTemplate, (sbyte)DataManager.NPC_DATA.GetNpcTemplate(spawnTemplate.GetNpcId()).GetLevel(), creator)
    {
        SetMasterName("");
        SetKnownlist(new Aion.GameServer.World.Knownlist.NpcKnownList(this));
        SetEffectController(new EffectController(this));
    }

    protected override void SetupStatContainers()
    {
        SetGameStats(new TrapGameStats(this));
        SetLifeStats(new NpcLifeStats(this));
    }

    public override sbyte GetLevel()
    {
        return GetCreator() == null ? (sbyte)1 : GetCreator().GetLevel();
    }

    public override NpcObjectType GetNpcObjectType()
    {
        return NpcObjectType.TRAP;
    }
}
