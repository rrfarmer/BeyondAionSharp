using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Brigade General Terath (219354). Retail pattern <c>IDTiamat_Sardha</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/tiamatStrongHold/BrigadeGeneralTerathAI (@author Cheatkiller).
/// Retail-sourced corrections below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The jump event placed two hostile drakan that retail never places.</b> Java spawns 283558 at the
/// two jump posts under a <c>TODO find Right ID</c>, and 283558 is <c>3rd vituperators assassin</c> — a
/// real aggressive monster. Retail's pattern names the npc outright: <c>IDTiamat_Sadha_JumpBoxFX</c>,
/// which is <b>283158</b>, an effect npc on <c>general</c>. One digit apart, and the difference is two
/// extra adds in a fight that has none.
/// </para>
/// <para>
/// <b>And the aetheric field stood seven hundred units away.</b> Java spawns it at
/// <c>(1030.08, 1030.08, 1030.08)</c> — the x repeated into y and z. Retail's
/// <c>IDTiamat_FOBJ_SardhaSheild</c> is at <c>(1030.08, 297.31, 407.04)</c>, which is inside the room.
/// </para>
/// <para>
/// <b>Not translated: the fight's whole cadence.</b> Retail drives Terath from four battle timers —
/// front attack every 12s, the jump at 35s then every 55s, the black hole every 15s with its closing
/// cast two seconds later, and a rage check every 10s below 14% — and re-arms each on its own rung.
/// This class drives the jump off <b>HP phases</b> (90/70/50/30/25) and the black hole off a fixed
/// 30-second task instead, so a party that burns Terath quickly sees a different fight from retail's.
/// Rewriting that is a larger change than this pass; the ids, posts and lifetimes are corrected here
/// and the cadence is recorded as outstanding.
/// </para>
/// </remarks>
[AIName("brigadegeneralterath")]
public class BrigadeGeneralTerathAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary><c>IDTiamat_FOBJ_SardhaSheild</c>, and retail's own coordinates for it.</summary>
    private const int AethericField = 730692;
    private const float FieldX = 1030.08f;
    private const float FieldY = 297.31f;
    private const float FieldZ = 407.04f;

    /// <summary><c>IDTiamat_Sadha_JumpBoxFX</c> — an effect npc, not the drakan Java spawned.</summary>
    private const int JumpBoxFx = 283158;

    /// <summary>Retail's <c>live_time</c> on the jump's three npcs.</summary>
    private const int JumpBoxLife = 29;
    private const int GravityLife = 24;

    private readonly HpPhases hpPhases = new HpPhases(90, 70, 50, 30, 25);
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private ScheduledTask? skillTask;
    private bool canThink = true;
    private Npc aethericField;
    private bool isGravityEvent;
    private bool isFinalBuff;

    public BrigadeGeneralTerathAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            if (aethericField == null)
            {
                aethericField = (Npc)Spawn(AethericField, FieldX, FieldY, FieldZ, (sbyte)0);
                GetPosition().GetWorldMapInstance().SetDoorState(706, false);
            }
            if (!isGravityEvent)
            {
                StartSkillTask();
            }
        }
        hpPhases.TryEnterNextPhase(this);
        if (!isFinalBuff && GetOwner().GetLifeStats().GetHpPercentage() <= 25)
        {
            isFinalBuff = true;
            AIActions.UseSkill(this, 20942);
        }
    }

    private void StartSkillTask()
    {
        skillTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (!IsDead())
                GravityDistortionEvent();
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(5000), System.TimeSpan.FromMilliseconds(30000));
    }

    private void CancelskillTask()
    {
        if (skillTask != null && !skillTask.IsCancelled)
        {
            skillTask.Cancel(true);
        }
    }

    private void GravityDistortionEvent()
    {
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20739, 55, GetOwner()).UseNoAnimationSkill();
        Spawn(283096, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        Spawn(283097, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        Spawn(283098, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(), (sbyte)0); // 4.0
        ThreadPoolManager.GetInstance().Schedule(_ => { SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20741, 55, GetOwner()).UseNoAnimationSkill(); return ValueTask.CompletedTask; }, 5000L);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        if (isGravityEvent)
            return;
        canThink = false;
        isGravityEvent = true;
        CancelskillTask();
        // Retail's two jump posts, and its own live_time. Java had 283558 here under a TODO: that is
        // "3rd vituperators assassin", a real monster, so the event was placing two adds.
        SpawnFor(JumpBoxFx, 1056.8f, 297.6f, 409.9f, (sbyte)0, JumpBoxLife);
        SpawnFor(JumpBoxFx, 1002.07f, 297.41f, 409.85f, (sbyte)0, JumpBoxLife);
        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20737, 55, GetOwner()).UseNoAnimationSkill();
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            EmoteManager.EmoteStopAttacking(GetOwner());
            SetStateIfNot(AIState.WALKING);
            GetOwner().GetMoveController().MoveToPoint(GetOwner().GetSpawn().GetX(), GetOwner().GetSpawn().GetY(), GetOwner().GetSpawn().GetZ());
            WalkManager.StartWalking(this);
            GetOwner().SetState(CreatureState.ACTIVE, true);
            PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
            return ValueTask.CompletedTask;
        }, 4000L);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            // Retail places both at the same point and gives both twenty-four seconds.
            SpawnFor(283109, 1029.93f, 297.31f, 409f, (sbyte)0, GravityLife);
            SpawnFor(283110, 1029.93f, 297.31f, 409f, (sbyte)0, GravityLife);
            return ValueTask.CompletedTask;
        }, 10000L);
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead())
                return ValueTask.CompletedTask;
            Despawn();
            GetEffectController().RemoveEffect(20737);
            canThink = true;
            isGravityEvent = false;
            StartSkillTask();
            Creature creature = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
            if (creature == null)
            {
                SetStateIfNot(AIState.FIGHT);
                Think();
            }
            else
            {
                GetMoveController().AbortMove();
                GetOwner().SetTarget(creature);
                GetOwner().GetGameStats().RenewLastAttackTime();
                GetOwner().GetGameStats().RenewLastAttackedTime();
                GetOwner().GetGameStats().RenewLastSkillTime();
                SetStateIfNot(AIState.WALKING);
                GetOwner().SetState(CreatureState.ACTIVE, true);
                GetOwner().GetMoveController().MoveToTargetObject();
                PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetOwner().GetObjectId()));
            }
            return ValueTask.CompletedTask;
        }, 30000L);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        foreach (Npc npc in npcs)
        {
            if (npc != null)
            {
                npc.GetController().Delete();
            }
        }
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelskillTask();
        aethericField.GetController().Delete();
        GetPosition().GetWorldMapInstance().SetDoorState(706, true);
        Despawn();
    }

    private void Despawn()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(JumpBoxFx));
        DeleteNpcs(instance.GetNpcs(283109)); // 4.0
        DeleteNpcs(instance.GetNpcs(283110)); // 4.0
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        isFinalBuff = false;
        CancelskillTask();
        isGravityEvent = false;
        canThink = true;
        isHome.Set(true);
        aethericField.GetController().Delete();
        Despawn();
        GetPosition().GetWorldMapInstance().SetDoorState(706, true);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelskillTask();
    }

    public override bool CanThink()
    {
        return canThink;
    }
}
