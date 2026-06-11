using System.Xml.Serialization;
using Aion.GameServer.SkillEngine.change;

namespace Aion.GameServer.SkillEngine.Effects.Modifier;

/// <summary>
/// Java parity: skillengine/effect/modifier/ActionModifier (ATracer). Base of the action-modifier cone.
/// <c>Effect</c> is fully qualified to avoid the SkillEngine.Effect namespace/type ambiguity;
/// <c>Func</c> is the skill-engine change enum (not System.Func).
/// </summary>
public abstract class ActionModifier
{
    [XmlAttribute("delta")]
    public int Delta;

    [XmlAttribute("value")]
    public int Value;

    [XmlAttribute("mode")]
    public Func Mode = Func.ADD;

    /// <summary>Applies modifier to original value.</summary>
    public abstract int Analyze(SkillEngine.Model.Effect effect);

    /// <summary>Performs check of condition.</summary>
    public abstract bool Check(SkillEngine.Model.Effect effect);

    public Func GetFunc()
    {
        return Mode;
    }
}
