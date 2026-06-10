using System.Threading.Tasks;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/AbstractOverTimeEffect (kecimis) abstract : EffectTemplate. @XmlAttribute(required=true)→[XmlAttribute] (required dropped); Future&lt;?&gt;→ScheduledTask; scheduleAtFixedRate(lambda)→ScheduleAtFixedRateTask(ct=>{OnPeriodicAction;...}); AbnormalState param nullable. Inherited value/duration2/position/OnPeriodicAction + EffectTemplate red-tolerated.</summary>
[XmlType("AbstractOverTimeEffect")]
public abstract class AbstractOverTimeEffect : EffectTemplate
{
    [XmlAttribute]
    protected int checktime;
    [XmlAttribute]
    protected bool percent;
    [XmlAttribute]
    protected bool shared;

    public override int GetValue()
    {
        return value;
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        this.StartEffect(effect, null);
    }

    public void StartEffect(Effect effect, AbnormalState? abnormal)
    {
        Creature effected = effect.GetEffected();
        if (abnormal != null)
        {
            effect.SetAbnormal(abnormal.Value);
            effected.GetEffectController().SetAbnormal(abnormal.Value);
        }
        // TODO figure out what to do with such cases
        if (checktime == 0)
            return;
        // Some skills have an effective duration of 2000 (see getDuration2) and a checktime of 1000 (e.g. Ripple of Purification).
        // On retail, these skills are applied once instead of twice, so we slightly increase the initialDelay to prevent this from happening.
        long initialDelay = 300 + checktime;
        ScheduledTask task = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { OnPeriodicAction(effect); return ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(initialDelay), System.TimeSpan.FromMilliseconds(checktime));
        effect.SetPeriodicTask(task, position);
    }

    public void EndEffect(Effect effect, AbnormalState? abnormal)
    {
        if (abnormal != null)
            effect.GetEffected().GetEffectController().UnsetAbnormal(abnormal.Value);
    }

    public override int GetDuration2()
    {
        return duration2 + 1000; // on retail these effects last one sec more than their template value of duration2
    }
}
