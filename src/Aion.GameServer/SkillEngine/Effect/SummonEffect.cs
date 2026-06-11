using System;
using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/SummonEffect (Simple) : EffectTemplate. @XmlAttribute(name="npc_id"/"time", required=true)→[XmlAttribute("npc_id"/"time")]; (Player) cast→(Player); Future&lt;?&gt;→ScheduledTask; schedule(lambda, time*1000)→Schedule(async delegate, TimeSpan.FromMilliseconds); release(UnsummonType.UNSPECIFIED); addTask(TaskId.DESPAWN, task); addSuccessEffect(this). SummonsService/Summon red-tolerated.</summary>
[XmlType("SummonEffect")]
public class SummonEffect : EffectTemplate
{
    [XmlAttribute("npc_id")]
    protected int npcId;
    [XmlAttribute("time")]
    protected int time; // in seconds

    public override void ApplyEffect(Effect effect)
    {
        Player effected = (Player)effect.GetEffected();
        Summon summon = SummonsService.CreateSummon(effected, npcId, effect.GetSkillId(), effect.GetSkillLevel(), time);
        if (summon != null && time > 0)
        {
            ScheduledTask task = ThreadPoolManager.GetInstance().Schedule(ct => { summon.GetController().Release(UnsummonType.UNSPECIFIED); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(time * 1000));
            summon.GetController().AddTask(TaskId.DESPAWN, task);
            effected.GetEffectController().RemovePetOrderUnSummonEffects();
        }
    }

    public override void Calculate(Effect effect)
    {
        effect.AddSuccessEffect(this);
    }
}
