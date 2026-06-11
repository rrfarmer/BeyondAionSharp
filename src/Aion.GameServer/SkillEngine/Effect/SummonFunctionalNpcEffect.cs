using System;
using System.Threading.Tasks;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Spawnengine;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SummonFunctionalNpcEffect (ginho1) : SummonEffect. @XmlAttribute(name="owner")→[XmlAttribute("owner")]; VisibleObjectSpawner.spawnFunctionalNpc; anonymous Runnable→async delegate; schedule(...,300000)→Schedule(async,TimeSpan.FromMilliseconds(300000)). SummonOwner/Npc red-tolerated.</summary>
[XmlType("SummonFunctionalNpcEffect")]
public class SummonFunctionalNpcEffect : SummonEffect
{
    [XmlAttribute("owner")]
    private SummonOwner owner;

    public override void ApplyEffect(Effect effect)
    {
        Player effected = (Player)effect.GetEffected();
        Npc functionalNpc = VisibleObjectSpawner.SpawnFunctionalNpc(effected, npcId, owner);

        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            if (functionalNpc != null && functionalNpc.IsSpawned())
                functionalNpc.GetController().Delete();
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(300000));
    }
}
