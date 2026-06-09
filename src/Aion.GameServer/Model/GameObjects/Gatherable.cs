using Aion.GameServer.Dataholders;
using Aion.GameServer.World;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/Gatherable extends VisibleObject.</summary>
public class Gatherable : VisibleObject
{
    public Gatherable(Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, Aion.GameServer.Controllers.GatherableController controller)
        : base(Aion.GameServer.Utils.Idfactory.IDFactory.GetInstance().NextId(), controller, spawnTemplate, DataManager.GATHERABLE_DATA.GetGatherableTemplate(spawnTemplate.GetNpcId()), new WorldPosition(spawnTemplate.GetWorldId()), true)
    {
        controller.SetOwner(this);
        SetKnownlist(new PlayerAwareKnownList(this));
    }

    public override Aion.GameServer.Model.Templates.Gather.GatherableTemplate GetObjectTemplate()
    {
        return (Aion.GameServer.Model.Templates.Gather.GatherableTemplate)base.GetObjectTemplate();
    }

    public override Aion.GameServer.Controllers.GatherableController GetController()
    {
        return (Aion.GameServer.Controllers.GatherableController)base.GetController();
    }
}
