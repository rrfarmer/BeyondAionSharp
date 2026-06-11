using System;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Stats;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/HostileUpEffect (ATracer, Yeats) : EffectTemplate. @XmlAttribute(name="temp_duration"/"temp_value"/"temp_delta"); applyEffect: Npc→addHate(tauntHate [+effectHate if sole successEffect] + tempHate); tempHate>0→schedule(tempDuration) remove tempHate + detach observer, DeathObserver cancels task. AtomicReference&lt;DeathObserver&gt; forward-ref→plain C# locals (closures capture by ref; single-threaded apply). calculate: base 3-arg false→return; setTauntHate(base), tempHate=tempValue+tempDelta*lvl, >0→StatFunctions.calculateHate. ScheduledFuture→ScheduledTask. DeathObserver/StatFunctions red-tolerated.</summary>
[XmlType("HostileUpEffect")]
public class HostileUpEffect : EffectTemplate
{
    [XmlAttribute("temp_duration")]
    protected int tempDuration = 0;
    [XmlAttribute("temp_value")]
    protected int tempValue = 0;
    [XmlAttribute("temp_delta")]
    protected int tempDelta = 0;

    private int tempHate = 0;

    public override void ApplyEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        if (effected is Npc)
        {
            int totalHate = effect.GetTauntHate();
            // FIXME some skills never broadcast regular hate. that's why the following check exists as a workaround, which should be removed once fixed
            // hate broadcasts in Effect.startEffect (if added to EffectController) and applyEffect (if there are no successEffects), so some never do
            if (effect.GetSuccessEffects().Count == 1) // only this effect template is present, therefore we know regular hate will never broadcast
                totalHate += effect.GetEffectHate();
            effected.GetAggroList().AddHate(effect.GetEffector(), totalHate + tempHate);
            if (tempHate > 0)
            {
                ScheduledTask task = null;
                DeathObserver observer = null;
                task = ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    effected.GetAggroList().AddHate(effect.GetEffector(), -tempHate);
                    effect.GetEffector().GetObserveController().RemoveObserver(observer);
                    return ValueTask.CompletedTask;
                }, TimeSpan.FromMilliseconds(tempDuration));
                observer = new DeathObserver(_ => task.Cancel(false));
                effect.GetEffector().GetObserveController().Attach(observer);
            }
        }
    }

    public override void Calculate(Effect effect)
    {
        if (!base.Calculate(effect, null, null))
            return;
        effect.SetTauntHate(CalculateBaseValue(effect));
        tempHate = tempValue + tempDelta * effect.GetSkillLevel();
        if (tempHate > 0)
            tempHate = StatFunctions.CalculateHate(effect.GetEffected(), tempHate);
    }
}
