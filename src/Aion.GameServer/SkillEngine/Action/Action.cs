using Aion.GameServer.SkillEngine.Effects.Modifier;

namespace Aion.GameServer.SkillEngine.action;

/// <summary>
/// Java parity: skillengine/action/Action. Abstract base for skill-cost actions.
/// Namespace is lowercase 'action' to avoid the FQN clash with this class name.
/// </summary>
public abstract class Action
{
    protected ActionModifiers modifiers;

    /// <summary>Perform action specified in template.</summary>
    public abstract bool Act(SkillEngine.Model.Skill skill);
}
