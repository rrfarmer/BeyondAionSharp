using Aion.GameServer.Model.Templates;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/StaticObject extends VisibleObject.</summary>
public class StaticObject : VisibleObject
{
    public StaticObject(Aion.GameServer.Controllers.StaticObjectController controller, Aion.GameServer.Model.Templates.Spawns.SpawnTemplate spawnTemplate, VisibleObjectTemplate objectTemplate)
        : base(Aion.GameServer.Utils.IdFactory.IDFactory.GetInstance().NextId(), controller, spawnTemplate, objectTemplate, new WorldPosition(spawnTemplate.GetWorldId()), true)
    {
        controller.SetOwner(this);
    }
}
