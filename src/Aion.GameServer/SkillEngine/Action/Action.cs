using Aion.GameServer.SkillEngine.Effects.Modifier;

namespace Aion.GameServer.SkillEngine.Action;

/// <summary>
/// Java parity: skillengine/action/Action. Abstract base for skill-cost actions.
/// Namespace is lowercase 'action' to avoid the FQN clash with this class name.
/// </summary>
public abstract class Action
{
    protected ActionModifiers modifiers;

    /// <summary>Perform action specified in template.</summary>
    public abstract bool Act(Aion.GameServer.SkillEngine.Model.Skill skill);

    /// <summary>
    /// Checks whether Act(Skill) could be performed, without performing it.
    /// </summary>
    /// <returns>True, if the action can be performed</returns>
    public virtual bool CanAct(Aion.GameServer.SkillEngine.Model.Skill skill)
    {
        return true;
    }
}
