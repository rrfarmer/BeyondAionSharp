using Aion.GameServer.Model.Stats.Calc;
using Aion.GameServer.Model.Stats.Calc.Functions;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.SkillEngine.condition;

/// <summary>
/// Java parity: skillengine/condition/Condition (ATracer). Base of the condition cone.
/// Namespace is lowercase 'condition' to avoid the FQN clash with this class name.
/// </summary>
public abstract class Condition : StatCondition
{
    /// <summary>Validate condition specified in template.</summary>
    public abstract bool Validate(Skill env);

    public virtual bool Validate(Stat2 stat, IStatFunction statFunction)
    {
        return true;
    }

    public virtual bool Validate(Effect effect)
    {
        return true;
    }
}
