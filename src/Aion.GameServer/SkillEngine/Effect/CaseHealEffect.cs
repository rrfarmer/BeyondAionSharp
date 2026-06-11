using System.Xml.Serialization;
using Aion.GameServer.Controllers.Observer;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effects;

/// <summary>Java parity: skillengine/effect/CaseHealEffect (kecimis) : AbstractHealEffect. @XmlAttribute(name="cond_value")→[XmlAttribute("cond_value")]; switch-expr on HealType→C# switch expr; anonymous ActionObserver(HP_CHANGED) overriding hpChanged→nested CaseHealObserver capturing effect; heals when current ≤ cond% of max. AbstractHealEffect/ActionObserver/Effect/SM_ATTACK_STATUS red-tolerated.</summary>
public class CaseHealEffect : AbstractHealEffect
{
    [XmlAttribute("cond_value")]
    protected int condValue;
    [XmlAttribute]
    protected HealType type;

    public override int GetCurrentStatValue(Effect effect)
    {
        return type switch
        {
            HealType.HP => effect.GetEffected().GetLifeStats().GetCurrentHp(),
            HealType.MP => effect.GetEffected().GetLifeStats().GetCurrentMp(),
            _ => 0,
        };
    }

    public override int GetMaxStatValue(Effect effect)
    {
        return type switch
        {
            HealType.HP => effect.GetEffected().GetGameStats().GetMaxHp().GetCurrent(),
            HealType.MP => effect.GetEffected().GetGameStats().GetMaxMp().GetCurrent(),
            _ => 0,
        };
    }

    public override void ApplyEffect(Effect effect)
    {
        effect.AddToEffectedController();
    }

    public override void StartEffect(Effect effect)
    {
        if (TryHeal(effect))
            return;
        effect.AddObserver(effect.GetEffected(), new CaseHealObserver(this, effect));
    }

    private bool TryHeal(Effect effect)
    {
        int currentValue = GetCurrentStatValue(effect);
        int maxCurValue = GetMaxStatValue(effect);
        // only heal if the current value is at or below the given percentage
        if (currentValue <= (maxCurValue * condValue / 100f))
        {
            if (type == HealType.HP)
                effect.GetEffected().GetLifeStats().IncreaseHp(SM_ATTACK_STATUS.TYPE.HP, CalculateHealValue(effect, type), effect, SM_ATTACK_STATUS.LOG.CASEHEAL);
            else if (type == HealType.MP)
                effect.GetEffected().GetLifeStats().IncreaseMp(SM_ATTACK_STATUS.TYPE.MP, CalculateHealValue(effect, type), effect.GetSkillId(), SM_ATTACK_STATUS.LOG.CASEHEAL);
            effect.EndEffect();
            return true;
        }
        return false;
    }

    public override bool AllowHpHealBoost(Effect effect)
    {
        return false;
    }

    public override bool AllowHpHealSkillDeboost(Effect effect)
    {
        return false;
    }

    // Java parity: anonymous ActionObserver(ObserverType.HP_CHANGED) in startEffect (hpChanged override).
    private sealed class CaseHealObserver : ActionObserver
    {
        private readonly CaseHealEffect outer;
        private readonly Effect effect;

        public CaseHealObserver(CaseHealEffect outer, Effect effect) : base(ObserverType.HP_CHANGED)
        {
            this.outer = outer;
            this.effect = effect;
        }

        public override void HpChanged(int value)
        {
            outer.TryHeal(effect);
        }
    }
}
