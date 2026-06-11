using Aion.GameServer.Controllers;
using Aion.GameServer.Controllers.Effects;
using Aion.GameServer.Model.Templates.Spawns;
using Aion.GameServer.World.Knownlist;

namespace Aion.GameServer.Model.GameObjects;

/// <summary>Java parity: model/gameobjects/GroupGate.</summary>
public class GroupGate : SummonedObject<Creature>
{
    public GroupGate(NpcController controller, SpawnTemplate spawnTemplate, Creature creator)
        : base(controller, spawnTemplate, (sbyte) 1, creator)
    {
        SetKnownlist(new PlayerAwareKnownList(this));
        SetEffectController(new EffectController(this));
    }

    public override NpcObjectType GetNpcObjectType()
    {
        return NpcObjectType.GROUPGATE;
    }
}
