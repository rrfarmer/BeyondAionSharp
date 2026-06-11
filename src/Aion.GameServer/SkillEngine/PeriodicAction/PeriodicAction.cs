namespace Aion.GameServer.SkillEngine.PeriodicAction;

/// <summary>
/// Java parity: skillengine/periodicaction/PeriodicAction. Abstract base for periodic effect actions.
/// Namespace is lowercase 'periodicaction' to avoid the FQN clash with this class name.
/// </summary>
public abstract class PeriodicAction
{
    public abstract void Act(Aion.GameServer.SkillEngine.Model.Effect effect);
}
