using Aion.GameServer.Controllers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Curingzones;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.Curingzone;

/// <summary>Java parity: model/curingzone/CuringObject (xTz).</summary>
public class CuringObject : VisibleObject
{
    private CuringTemplate template;
    private float range;

    public CuringObject(CuringTemplate template, int instanceId)
        : base(IDFactory.GetInstance().NextId(), new CuringObjectController(), null, null,
            Aion.GameServer.World.World.GetInstance().CreatePosition(template.GetMapId(), template.GetX(), template.GetY(), template.GetZ(), (byte) 0, instanceId), true)
    {
        this.template = template;
        this.range = template.GetRange();
        SetKnownlist(new NpcKnownList(this));
    }

    public CuringTemplate GetTemplate()
    {
        return template;
    }

    // Java parity: getName()
    public override string Name => "";

    public float GetRange()
    {
        return range;
    }

    public void Spawn()
    {
        Aion.GameServer.World.World w = Aion.GameServer.World.World.GetInstance();
        w.StoreObject(this);
        w.Spawn(this);
    }

    // Java parity: anonymous empty `new VisibleObjectController<CuringObject>(){}` passed to super().
    private sealed class CuringObjectController : VisibleObjectController<CuringObject>
    {
    }
}
