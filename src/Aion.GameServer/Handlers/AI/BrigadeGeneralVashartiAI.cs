using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.SkillEngine.Effects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/rentusBase/BrigadeGeneralVashartiAI (@author xTz, Yeats, Estrayl).</summary>
[AIName("brigade_general_vasharti")]
public class BrigadeGeneralVashartiAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail thresholds, from pattern IDYun_Nmd6: three steps at 86/56/26, where we had four at an
    /// invented 75/50/25/10. Every step does the same thing here, so there is no per-percent branch to
    /// follow the renumbering.
    /// </summary>
    /// <remarks>
    /// Retail also spawns a Glove Controller at each step. Those NPCs (283002/283004/283006) exist but
    /// are plain aggressive clones of Vasharti himself with no controller AI, so spawning them would put
    /// three extra full-strength bosses in the room rather than retail's controllers — harder than
    /// retail, not closer to it. They wait for their own AI. See docs/retail-ai-fidelity.md.
    /// </remarks>
    private readonly HpPhases hpPhases = new HpPhases(86, 56, 26);

    /// <summary>
    /// The two flames he lights when the fight starts, and where retail puts them.
    /// </summary>
    /// <remarks>
    /// Retail-sourced; see docs/retail-ai-fidelity.md. His reflect alternates red and blue on a 40s
    /// timer and players have to stand in the matching flame, so without these the mechanic has no
    /// board to play on. <c>DancingFlameAI</c> was already written for them; nothing had ever spawned
    /// one. Only 217313 is live on this pattern, so the fixed coordinates are unambiguous.
    /// </remarks>
    private const int DancingRedFlame = 282996;
    private const int DancingBlueFlame = 282997;
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);
    private readonly AtomicBoolean isInFlameShowerEvent = new AtomicBoolean();
    private ScheduledTask? enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask;

    public BrigadeGeneralVashartiAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            GetPosition().GetWorldMapInstance().SetDoorState(70, false);
            LightTheFlames();
            enrageSchedule = ThreadPoolManager.GetInstance().Schedule(_ => { HandleEnrageEvent(); return ValueTask.CompletedTask; }, (long)System.TimeSpan.FromMinutes(10).TotalMilliseconds);
            ScheduleFlameShieldBuffEvent(5000);
        }
        if (!isInFlameShowerEvent.Get())
            hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        CancelTasks(flameShieldBuffSchedule);
        GetOwner().ClearQueuedSkills();
        GetOwner().QueueSkill(20532, 1, 10000); // off (skill name)
    }

    private void ScheduleFlameShieldBuffEvent(int delay)
    {
        flameShieldBuffSchedule = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(20530 + Rnd.Get(0, 1), 60); return ValueTask.CompletedTask; }, (long)delay);
    }

    private void HandleEnrageEvent()
    {
        GetOwner().ClearQueuedSkills();
        GetOwner().QueueSkill(19962, 1, 15000); // Purple Flame Weapon
        GetOwner().QueueSkill(19907, 1, 0); // Chastise
    }

    private void HandleSeaOfFireEvent()
    {
        int percent = GetLifeStats().GetHpPercentage();
        int npcId = percent <= 70 ? percent <= 40 ? 283012 : 283011 : 283010;

        Spawn(npcId, 188.33f, 414.61f, 260.61f, unchecked((sbyte)244)); // FX
        Spawn(283007, 188.33f, 414.61f, 260.61f, (sbyte)0); // de-buff

        seaOfFireSpawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            int smashCount = (npcId - 283007) * 5 + 1; // 15, 20, 25
            for (int i = 2; i < smashCount; i++)
            {
                RndSpawnInRange(i % 2 == 0 ? 283008 : 283009, 0, 29);
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(750), System.TimeSpan.FromMilliseconds(7100));
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20534:
                HandleSeaOfFireEvent();
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 19907: // repeat until reset
                GetOwner().QueueSkill(19907, 1, 0); // Chastise
                break;
            case 20530:
            case 20531:
                WorldMapInstance instance = GetPosition().GetWorldMapInstance();
                if (instance != null)
                {
                    if (instance.GetNpc(283000) == null)
                        Spawn(283000, 171.330f, 417.57f, 261f, (sbyte)116);
                    if (instance.GetNpc(283001) == null)
                        Spawn(283001, 205.280f, 410.53f, 261f, (sbyte)56);
                }
                ScheduleFlameShieldBuffEvent(33000);
                break;
            case 20532:
                EmoteManager.EmoteStopAttacking(GetOwner());
                GetOwner().ClearQueuedSkills();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    WalkManager.StartForcedWalking(this, 188.17f, 414.06f, 260.75488f);
                    GetOwner().SetState(CreatureState.ACTIVE, true);
                    PacketSendUtility.BroadcastPacket(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
                    return ValueTask.CompletedTask;
                }, 800L);
                break;
            case 20533:
                SetStateIfNot(AIState.FIGHT);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20534, 1, GetOwner()).UseSkill(); // Sea of Fire
                break;
        }
    }

    public override void OnEffectEnd(Effect effect)
    {
        if (effect != null && effect.GetSkillId() == 20534 && isInFlameShowerEvent.CompareAndSet(true, false))
        {
            CancelTasks(seaOfFireSpawnTask);
            GetKnownList().ForEachNpc(n =>
            {
                switch (GetNpcId())
                {
                    case 283010:
                    case 283011:
                    case 283012:
                        n.GetController().Delete();
                        break;
                }
            });
            ScheduleFlameShieldBuffEvent(10000);
            GetOwner().GetAggroList().AddHate((Creature)GetTarget(), 1000);
        }
    }

    public override bool IsDestinationReached()
    {
        if (GetState() == AIState.FORCED_WALKING && PositionUtil.GetDistance(GetOwner().GetX(), GetOwner().GetY(), 188.17f, 414.06f) <= 1f
            && isInFlameShowerEvent.CompareAndSet(false, true))
        {
            SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 20533, 1, GetOwner()).UseSkill(); // off (skill name)
        }
        return base.IsDestinationReached();
    }

    /// <summary>Places the red and blue flames at the two points retail lights them.</summary>
    private void LightTheFlames()
    {
        Spawn(DancingRedFlame, 167.6f, 418.22f, 262.54f, (sbyte)0);
        Spawn(DancingBlueFlame, 208.58f, 410.71f, 262.54f, (sbyte)0);
    }

    private void ClearSpawns()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            DeleteNpcs(instance.GetNpcs(283002));
            DeleteNpcs(instance.GetNpcs(283003));
            DeleteNpcs(instance.GetNpcs(283004));
            DeleteNpcs(instance.GetNpcs(283005));
            DeleteNpcs(instance.GetNpcs(283006));
            DeleteNpcs(instance.GetNpcs(283007));
            DeleteNpcs(instance.GetNpcs(283010));
            DeleteNpcs(instance.GetNpcs(283011));
            DeleteNpcs(instance.GetNpcs(283012));
            DeleteNpcs(instance.GetNpcs(283000));
            DeleteNpcs(instance.GetNpcs(283001));
            DeleteNpcs(instance.GetNpcs(DancingRedFlame));
            DeleteNpcs(instance.GetNpcs(DancingBlueFlame));
        }
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        npcs.Where(npc => npc != null).ToList().ForEach(npc => npc.GetController().Delete());
    }

    private void CancelTasks(params ScheduledTask?[] tasks)
    {
        foreach (ScheduledTask? task in tasks)
            if (task != null && !task.IsCancelled)
                task.Cancel(true);
    }

    protected override void HandleDespawned()
    {
        CancelTasks(enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask);
        ClearSpawns();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        isHome.Set(true);
        GetPosition().GetWorldMapInstance().SetDoorState(70, true);
        CancelTasks(enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask);
        ClearSpawns();
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDied()
    {
        GetPosition().GetWorldMapInstance().SetDoorState(70, true);
        CancelTasks(enrageSchedule, flameShieldBuffSchedule, seaOfFireSpawnTask);
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500410);
        ClearSpawns();
        base.HandleDied();
    }
}
