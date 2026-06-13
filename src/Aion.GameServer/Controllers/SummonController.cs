using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using TYPE = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.TYPE;
using LOG = Aion.GameServer.Network.Aion.ServerPackets.SmAttackStatus.LOG;

namespace Aion.GameServer.Controllers;

/// <summary>Java parity: controllers/SummonController (ATracer, RotO [Attack-speed hack protection], Sippolo).</summary>
public class SummonController : CreatureController<Summon>
{
    private long lastAttackMillis = 0;

    public override void NotKnow(VisibleObject @object)
    {
        base.NotKnow(@object);
        if (GetOwner().GetMaster().Equals(@object))
            Aion.GameServer.Services.Summons.SummonsService.Release(GetOwner(), UnsummonType.DISTANCE);
    }

    /// <summary>Release summon.</summary>
    public virtual void Release(UnsummonType unsummonType)
    {
        Aion.GameServer.Services.Summons.SummonsService.Release(GetOwner(), unsummonType);
    }

    /// <summary>Change to rest mode.</summary>
    public virtual void RestMode()
    {
        Aion.GameServer.Services.Summons.SummonsService.RestMode(GetOwner());
    }

    public virtual void SetUnkMode()
    {
        Aion.GameServer.Services.Summons.SummonsService.SetUnkMode(GetOwner());
    }

    /// <summary>Change to guard mode.</summary>
    public virtual void GuardMode()
    {
        Aion.GameServer.Services.Summons.SummonsService.GuardMode(GetOwner());
    }

    /// <summary>Change to attackMode.</summary>
    public virtual void AttackMode(int targetObjId)
    {
        VisibleObject obj = GetOwner().GetKnownList().GetObject(targetObjId);
        if (obj is Creature)
        {
            Aion.GameServer.Services.Summons.SummonsService.AttackMode(GetOwner());
        }
    }

    public override void AttackTarget(Creature target, int time, bool skipChecks)
    {
        if (target.IsDead() || target.GetLifeStats().IsAboutToDie() || !GetOwner().IsEnemy(target))
        {
            PacketSendUtility.SendPacket(GetMaster(), SmSystemMessage.STR_INVALID_TARGET());
            return;
        }

        int attackSpeed = GetOwner().GetGameStats().GetAttackSpeed().GetCurrent();
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long msSinceLastAttack = now - lastAttackMillis;
        if (msSinceLastAttack < attackSpeed && attackSpeed - msSinceLastAttack > 50) // 50ms tolerance
        {
            Aion.GameServer.Utils.Audit.AuditLogger.Log(GetMaster(), "possibly used hack to speed up summon auto-attack (" + msSinceLastAttack + "ms instead of " + attackSpeed + ")");
            return;
        }
        lastAttackMillis = now;
        base.AttackTarget(target, time, false);
    }

    public override void OnAttack(Creature creature, Effect effect, TYPE type, int damage, bool notifyAttack, LOG log, AttackStatus attackStatus,
        HopType? hopType)
    {
        if (GetOwner().IsDead())
            return;

        // temp
        if (GetOwner().GetMode() == SummonMode.RELEASE)
            return;

        base.OnAttack(creature, effect, type, damage, notifyAttack, log, attackStatus, hopType);
        PacketSendUtility.SendPacket(GetOwner().GetMaster(), new SM_SUMMON_UPDATE(GetOwner()));
    }

    public override void OnTargetChanged(VisibleObject oldTarget, VisibleObject newTarget)
    {
        base.OnTargetChanged(oldTarget, newTarget);
        GetOwner().ClearSkillOrders();
    }

    public override void OnDespawn()
    {
        if (GetOwner().GetMode() == SummonMode.RELEASE)
            GetOwner().GetEffectController().RemoveAllEffects();
        base.OnDespawn();
    }

    public override void OnDie(Creature lastAttacker)
    {
        base.OnDie(lastAttacker);
        Aion.GameServer.Services.Summons.SummonsService.Release(GetOwner(), UnsummonType.UNSPECIFIED);
    }

    public void UseSkill(SkillOrder order)
    {
        Creature creature = GetOwner();
        if (!DataManager.PET_SKILL_DATA.PetHasSkill(GetOwner().GetObjectTemplate().GetTemplateId(), order.GetSkillId()))
        {
            // hackers!)
            return;
        }
        Skill skill = Aion.GameServer.SkillEngine.SkillEngine.GetInstance().GetSkill(creature, order.GetSkillId(), 1, order.GetTarget());
        skill.SetHate(order.GetHate());
        if (skill.UseSkill() && order.IsRelease())
        {
            Aion.GameServer.Services.Summons.SummonsService.Release(GetOwner(), UnsummonType.UNSPECIFIED);
        }
    }

    public override void OnStartMove()
    {
        base.OnStartMove();
        Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().AddPlayer(GetOwner());
        UpdateZone();
    }

    public override void OnStopMove()
    {
        base.OnStopMove();
        Aion.GameServer.Taskmanager.Tasks.PlayerMoveTaskManager.GetInstance().RemovePlayer(GetOwner());
    }

    protected Aion.GameServer.Model.GameObjects.Players.Player GetMaster()
    {
        return GetOwner().GetMaster();
    }
}
