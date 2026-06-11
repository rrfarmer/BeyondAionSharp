using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.Change;
using Aion.GameServer.SkillEngine.Condition;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/BufEffect (ATracer) abstract : EffectTemplate. @XmlAttribute→[XmlAttribute]; **CreatureGameStats&lt;? extends Creature&gt;→non-generic CreatureGameStats**; inline LoggerFactory.getLogger().warn→inline ILogger; switch(Func) ADD/PERCENT/REPLACE. Inherited `change` + EffectTemplate/StatXFunction red-tolerated.</summary>
[XmlType("BufEffect")]
public abstract class BufEffect : EffectTemplate
{
    [XmlAttribute]
    protected bool maxstat;

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    /// <summary>Will be called from effect controller when effect starts</summary>
    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        CreatureGameStats cgs = effected.GetGameStats();

        List<IStatFunction> modifiers = GetModifiers(effect);

        if (modifiers.Count > 0)
            cgs.AddEffect(effect, modifiers);

        if (maxstat)
            effected.GetLifeStats().SynchronizeWithMaxStats();
    }

    protected virtual List<IStatFunction> GetModifiers(Effect effect)
    {
        int skillId = effect.GetSkillId();
        int skillLvl = effect.GetSkillLevel();

        List<IStatFunction> modifiers = new List<IStatFunction>();

        if (change == null)
            return modifiers;

        foreach (Change changeItem in change)
        {
            if (changeItem.GetStat() == null)
            {
                NullLoggerFactory.Instance.CreateLogger(nameof(BufEffect)).LogWarning("Skill stat has wrong name for skillid: " + skillId);
                continue;
            }

            int valueWithDelta = changeItem.GetValue() + changeItem.GetDelta() * skillLvl;

            Conditions conditions = changeItem.GetConditions();
            switch (changeItem.GetFunc())
            {
                case Func.ADD:
                    modifiers.Add(new StatAddFunction(changeItem.GetStat(), valueWithDelta, true).WithConditions(conditions));
                    break;
                case Func.PERCENT:
                    modifiers.Add(new StatRateFunction(changeItem.GetStat(), valueWithDelta, true).WithConditions(conditions));
                    break;
                case Func.REPLACE:
                    modifiers.Add(new StatSetFunction(changeItem.GetStat(), valueWithDelta).WithConditions(conditions));
                    break;
            }
        }
        return modifiers;
    }
}
