using System.Xml.Serialization;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Skillengine.Model;

namespace Aion.GameServer.Skillengine.Effect;

/// <summary>Java parity: skillengine/effect/TransformEffect (Sweetkr, kecimis) abstract : EffectTemplate. @XmlAttribute fields→[XmlAttribute]; instanceof TransformEffect+cast→is TransformEffect te. EffectTemplate/Effect/Creature/TransformType/transformModel red-tolerated.</summary>
[XmlType("TransformEffect")]
public abstract class TransformEffect : EffectTemplate
{
    [XmlAttribute]
    protected int model;

    [XmlAttribute]
    protected TransformType type = TransformType.NONE;

    [XmlAttribute]
    protected int panelid;

    [XmlAttribute]
    protected int banUseSkills;
    [XmlAttribute]
    protected int banMovement;
    [XmlAttribute]
    protected int res1;
    [XmlAttribute]
    protected int res2;
    [XmlAttribute]
    protected int res3;
    [XmlAttribute]
    protected int res5;
    [XmlAttribute]
    protected int res6;

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
            effected.GetTransformModel().Apply(temp.GetTransformId(), temp.GetTransformType(), temp.GetPanelId(), temp.GetBanUseSkills(),
                temp.GetBanMovement(), temp.GetRes1(), temp.GetRes2(), temp.GetRes3(), temp.GetRes5(), temp.GetRes6());
        else
            effected.EndTransformation();
    }

    public override void StartEffect(Effect effect)
    {
        Creature effected = effect.GetEffected();
        effected.GetTransformModel().Apply(this.GetTransformId(), this.GetTransformType(), this.GetPanelId(), this.GetBanUseSkills(),
            this.GetBanMovement(), this.GetRes1(), this.GetRes2(), this.GetRes3(), this.GetRes5(), this.GetRes6());
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

    /// <summary>the banUseSkills</summary>
    public int GetBanUseSkills()
    {
        return banUseSkills;
    }

    /// <summary>the banMovement</summary>
    public int GetBanMovement()
    {
        return banMovement;
    }

    /// <summary>the res1</summary>
    public int GetRes1()
    {
        return res1;
    }

    /// <summary>the res2</summary>
    public int GetRes2()
    {
        return res2;
    }

    /// <summary>the res3</summary>
    public int GetRes3()
    {
        return res3;
    }

    /// <summary>the res5</summary>
    public int GetRes5()
    {
        return res5;
    }

    /// <summary>the res6</summary>
    public int GetRes6()
    {
        return res6;
    }
}
