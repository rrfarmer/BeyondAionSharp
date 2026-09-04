using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>Java parity: skillengine/effect/TransformEffect (Sweetkr, kecimis) abstract : EffectTemplate. @XmlAttribute fields→[XmlAttribute]; instanceof TransformEffect+cast→is TransformEffect te. EffectTemplate/Effect/Creature/TransformType/transformModel red-tolerated.</summary>
[XmlType("TransformEffect")]
public abstract class TransformEffect : EffectTemplate
{
    [XmlAttribute]
    public int model;

    [XmlAttribute]
    public TransformType type = TransformType.NONE;

    [XmlAttribute]
    public int panelid;

    [XmlAttribute]
    public bool cantUseSkills;
    [XmlAttribute]
    public bool cantMove;
    [XmlAttribute]
    public bool cantRecall;
    [XmlAttribute]
    public bool cantJump;
    [XmlAttribute]
    public bool cantAttack;
    [XmlAttribute]
    public bool cantUseItems;
    [XmlAttribute]
    public bool cantFly;

    public override void ApplyEffect(Effect effect)
    {
        // TODO need more info fix for cases like use itemId: 160010206(Dignified Wyvern Form Candy) after that use cannon skill(ex. 20365) -> candy should be removed
        if (type == TransformType.FORM1 && panelid > 0)
        {
            if (effect.GetEffected().GetTransformModel().IsActive())
            {
                effect.GetEffected().GetEffectController().RemoveTransformEffects();
            }
        }

        effect.AddToEffectedController();
    }

    public override void EndEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();

        TransformEffect temp = null;
        foreach (Effect tmp in effected.GetEffectController().GetAbnormalEffects())
        {
            foreach (EffectTemplate template in tmp.GetEffectTemplates())
            {
                if (template is TransformEffect te && te.GetTransformId() != model)
                {
                    temp = te;
                    break;
                }
            }
        }
        if (temp != null)
            effected.GetTransformModel().Apply(temp.GetTransformId(), temp.GetTransformType(), temp.GetPanelId(), temp.CantUseSkills(),
                temp.CantMove(), temp.CantRecall(), temp.CantJump(), temp.CantAttack(), temp.CantUseItems(), temp.CantFly());
        else
            effected.EndTransformation();
    }

    public override void StartEffect(Effect effect)
    {
        effect.GetEffected().GetTransformModel().Apply(GetTransformId(), GetTransformType(), GetPanelId(), CantUseSkills(), CantMove(), CantRecall(),
            CantJump(), CantAttack(), CantUseItems(), CantFly());
    }

    public TransformType GetTransformType()
    {
        return type;
    }

    public int GetTransformId()
    {
        return model;
    }

    public int GetPanelId()
    {
        return panelid;
    }

    public bool CantUseSkills()
    {
        return cantUseSkills;
    }

    public bool CantMove()
    {
        return cantMove;
    }

    public bool CantRecall()
    {
        return cantRecall;
    }

    public bool CantJump()
    {
        return cantJump;
    }

    public bool CantAttack()
    {
        return cantAttack;
    }

    public bool CantUseItems()
    {
        return cantUseItems;
    }

    public bool CantFly()
    {
        return cantFly;
    }
}
