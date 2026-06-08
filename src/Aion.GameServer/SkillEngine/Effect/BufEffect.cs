using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.Model.Stats.Container;
using Aion.GameServer.SkillEngine.change;
using Aion.GameServer.SkillEngine.condition;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.SkillEngine.Effect;

/// <summary>
/// Java parity: skillengine/effect/BufEffect (ATracer). Base for stat-modifying buff effects.
/// </summary>
public abstract class BufEffect : EffectTemplate
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlAttribute("maxstat")]
    public bool maxstat;

    public override void ApplyEffect(SkillEngine.Model.Effect effect)
    {
        effect.AddToEffectedController();
    }

    /// <summary>Will be called from effect controller when effect starts.</summary>
    public override void StartEffect(SkillEngine.Model.Effect effect)
    {
        Creature effected = effect.GetEffected();
        CreatureGameStats cgs = effected.GetGameStats();

        List<IStatFunction> modifiers = GetModifiers(effect);

        if (modifiers.Count > 0)
            cgs.AddEffect(effect, modifiers);

        if (maxstat)
            effected.GetLifeStats().SynchronizeWithMaxStats();
    }

    protected virtual List<IStatFunction> GetModifiers(SkillEngine.Model.Effect effect)
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
                log.LogWarning("Skill stat has wrong name for skillid: " + skillId);
                continue;
            }

            int valueWithDelta = changeItem.GetValue() + changeItem.GetDelta() * skillLvl;

            Conditions? conditions = changeItem.GetConditions();
            switch (changeItem.GetFunc())
            {
                case Func.ADD:
                    modifiers.Add(new StatAddFunction(changeItem.GetStat()!.Value, valueWithDelta, true).WithConditions(conditions));
                    break;
                case Func.PERCENT:
                    modifiers.Add(new StatRateFunction(changeItem.GetStat()!.Value, valueWithDelta, true).WithConditions(conditions));
                    break;
                case Func.REPLACE:
                    modifiers.Add(new StatSetFunction(changeItem.GetStat()!.Value, valueWithDelta).WithConditions(conditions));
                    break;
            }
        }
        return modifiers;
    }
}
