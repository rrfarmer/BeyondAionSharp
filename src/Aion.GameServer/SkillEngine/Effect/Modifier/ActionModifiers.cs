using System.Collections.Generic;
using System.Xml.Serialization;

namespace Aion.GameServer.SkillEngine.Effect.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/ActionModifiers (ATracer). Polymorphic XML list of
/// action modifiers (Java @XmlElements → C# multiple [XmlElement(name, typeof(Sub))]).
/// Subclass types are forward-refs until the modifier cone is ported (WIP).
/// </summary>
public class ActionModifiers
{
    [XmlElement("frontdamage", typeof(FrontDamageModifier))]
    [XmlElement("backdamage", typeof(BackDamageModifier))]
    [XmlElement("abnormaldamage", typeof(AbnormalDamageModifier))]
    [XmlElement("targetrace", typeof(TargetRaceDamageModifier))]
    [XmlElement("targetclass", typeof(TargetClassDamageModifier))]
    protected List<ActionModifier>? actionModifiers;

    /// <summary>Gets the action modifiers (live list; lazily created). Java parity: getActionModifiers().</summary>
    public List<ActionModifier> GetActionModifiers()
    {
        if (actionModifiers == null)
        {
            actionModifiers = new List<ActionModifier>();
        }
        return actionModifiers;
    }
}
