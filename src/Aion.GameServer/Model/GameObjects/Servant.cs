using Aion.GameServer.Controllers.Effect;
using Aion.GameServer.Model.Stats.Container;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Servant extends SummonedObject&lt;Creature&gt;.</summary>
public class Servant : SummonedObject<Creature>
{
    private NpcObjectType objectType;

    public Servant(Aion.GameServer.Controllers.NpcController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, sbyte level, Creature creator)
        : base(controller, spawnTemplate, level, creator)
    {
        SetMasterName("");
        SetKnownlist(new Aion.GameServer.World.Knownlist.NpcKnownList(this));
        SetEffectController(new EffectController(this));
    }

    protected override void SetupStatContainers()
    {
        SetGameStats(new ServantGameStats(this));
        SetLifeStats(new NpcLifeStats(this));
    }

    public override NpcObjectType GetNpcObjectType()
    {
        return objectType;
    }

    public void SetNpcObjectType(NpcObjectType objectType)
    {
        this.objectType = objectType;
    }

    public void SetUpStats()
    {
        ((ServantGameStats)GetGameStats()).SetUpStats();
    }
}
