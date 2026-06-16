using System.Collections.Generic;
using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.Effects;

/// <summary>
/// Java parity: skillengine/effect/DispelEffect (ATracer).
/// </summary>
public class DispelEffect : EffectTemplate
{
    [XmlElement("effectids")]
    public List<int>? effectids;

    [XmlElement("effecttype")]
    public List<EffectType>? effecttype;

    [XmlElement("slottype")]
    public List<DispelSlotType>? slottype;

    // Java parity: @XmlAttribute("dispeltype") DispelType (nullable). XmlSerializer cannot bind Nullable<T>
    // as an attribute, so round-trip via a string proxy (null when absent / unparseable, mirroring JAXB).
    [XmlIgnore]
    public DispelType? dispeltype;

    [XmlAttribute("dispeltype")]
    public string? DispeltypeRaw
    {
        get => dispeltype?.ToString();
        set => dispeltype = value == null ? (DispelType?)null : (System.Enum.TryParse<DispelType>(value, out var v) ? v : (DispelType?)null);
    }

    [XmlAttribute("count")]
    public int count;

    [XmlAttribute("dpower")]
    public int dpower;

    [XmlAttribute("power")]
    public int power = 100;

    [XmlAttribute("dispel_level")]
    public int dispelLevel = 100;

    public override void ApplyEffect(Aion.GameServer.SkillEngine.Model.Effect effect)
    {
        if (effect.GetEffected() == null || effect.GetEffected().GetEffectController() == null)
            return;

        if (dispeltype == null)
            return;

        if ((dispeltype == DispelType.EFFECTID || dispeltype == DispelType.EFFECTIDRANGE) && effectids == null)
            return;

        if (dispeltype == DispelType.EFFECTTYPE && effecttype == null)
            return;

        if (dispeltype == DispelType.SLOTTYPE && slottype == null)
            return;

        int finalPower = power + dpower * effect.GetSkillLevel();

        switch (dispeltype)
        {
            case DispelType.EFFECTID:
                int removedEffects = 0;
                foreach (int effectId in effectids!)
                {
                    if (removedEffects == count)
                        break;
                    if (effect.GetEffected().GetEffectController().RemoveByEffectId(effectId, dispelLevel, finalPower))
                        removedEffects++;
                }
                break;
            case DispelType.EFFECTIDRANGE:
                int removedEffectCount = 0;
                for (int effectId = effectids![0]; effectId <= effectids[1]; effectId++)
                {
                    if (removedEffectCount == count)
                        break;
                    if (effect.GetEffected().GetEffectController().RemoveByEffectId(effectId, dispelLevel, finalPower))
                        removedEffectCount++;
                }
                break;
            case DispelType.EFFECTTYPE:
                foreach (EffectType type in effecttype!)
                {
                    effect.GetEffected().GetEffectController().RemoveByDispelEffect(type, null, count, dispelLevel, finalPower);
                }
                break;
            case DispelType.SLOTTYPE:
                foreach (DispelSlotType type in slottype!)
                {
                    effect.GetEffected().GetEffectController().RemoveByDispelEffect(null, type, count, dispelLevel, finalPower);
                }
                break;
        }
    }
}
