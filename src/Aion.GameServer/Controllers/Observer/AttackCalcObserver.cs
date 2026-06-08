using System.Collections.Generic;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Observer;

/// <summary>
/// Java parity: controllers/observer/AttackCalcObserver (ATracer).
/// Default no-op attack-calculation observer; subclasses override to modify combat results.
/// </summary>
public class AttackCalcObserver
{
    public virtual bool CheckStatus(AttackStatus status)
    {
        return false;
    }

    public virtual void CheckShield(List<AttackResult> attackList, Effect effect, Creature attacker)
    {
    }

    public virtual bool CheckAttackerStatus(AttackStatus status)
    {
        return false;
    }

    public virtual AttackerCriticalStatus CheckAttackerCriticalStatus(AttackStatus status, bool isSkill)
    {
        return new AttackerCriticalStatus(false);
    }

    /// <returns>physical damage multiplier</returns>
    public virtual float GetBasePhysicalDamageMultiplier(bool isSkill)
    {
        return 1f;
    }

    /// <returns>magic damage multiplier</returns>
    public virtual float GetBaseMagicalDamageMultiplier()
    {
        return 1f;
    }
}
