using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Controllers.Attack;

/// <summary>
/// Result of one attack hit: damage, status, hit type, and shield/reflect/protect details.
/// Java parity: controllers/attack/AttackResult.
/// </summary>
public class AttackResult
{
    private float _damage;
    private readonly AttackStatus _attackStatus;
    private HitType _hitType = HitType.EVERYHIT;

    // shield effects related
    private int _shieldType;
    private int _reflectedDamage;
    private int _reflectedSkillId;
    private int _protectedSkillId;
    private int _protectedDamage;
    private int _protectorId;
    private int _mpAbsorbed;
    private int _mpShieldSkillId;

    private bool _launchSubEffect = true;

    public AttackResult(float damage, AttackStatus attackStatus)
    {
        _damage = damage;
        _attackStatus = attackStatus;
    }

    public AttackResult(float damage, AttackStatus attackStatus, HitType type) : this(damage, attackStatus)
    {
        _hitType = type;
    }

    // Java parity: getDamage()
    public int GetDamage() => (int)_damage;

    // Java parity: getExactDamage()
    public float GetExactDamage() => _damage;

    // Java parity: setDamage(float)
    public void SetDamage(float damage) => _damage = damage;

    // Java parity: getAttackStatus()
    public AttackStatus GetAttackStatus() => _attackStatus;

    // Java parity: getHitType()
    public HitType GetHitType() => _hitType;

    // Java parity: setHitType(HitType)
    public void SetHitType(HitType type) => _hitType = type;

    // Java parity: getShieldType()
    public int GetShieldType() => _shieldType;

    // Java parity: setShieldType(int) — OR-accumulates
    public void SetShieldType(int shieldType) => _shieldType |= shieldType;

    public int GetReflectedDamage() => _reflectedDamage;
    public void SetReflectedDamage(int reflectedDamage) => _reflectedDamage = reflectedDamage;

    public int GetReflectedSkillId() => _reflectedSkillId;
    public void SetReflectedSkillId(int skillId) => _reflectedSkillId = skillId;

    public int GetProtectedSkillId() => _protectedSkillId;
    public void SetProtectedSkillId(int skillId) => _protectedSkillId = skillId;

    public int GetProtectedDamage() => _protectedDamage;
    public void SetProtectedDamage(int protectedDamage) => _protectedDamage = protectedDamage;

    public int GetProtectorId() => _protectorId;
    public void SetProtectorId(int protectorId) => _protectorId = protectorId;

    public bool IsLaunchSubEffect() => _launchSubEffect;
    public void SetLaunchSubEffect(bool launchSubEffect) => _launchSubEffect = launchSubEffect;

    public int GetMpAbsorbed() => _mpAbsorbed;
    public void SetMpAbsorbed(int mpAbsorbed) => _mpAbsorbed = mpAbsorbed;

    public int GetMpShieldSkillId() => _mpShieldSkillId;
    public void SetMpShieldSkillId(int mpShieldSkillId) => _mpShieldSkillId = mpShieldSkillId;
}
