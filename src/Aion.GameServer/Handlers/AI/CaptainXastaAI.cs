using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Captain Xasta, Rentus Base. Retail pattern IDYun_Nmd3 (217309); his second form (217310) runs its
/// own pattern and is untouched here.
/// </summary>
/// <remarks>
/// Retail-sourced; see docs/retail-ai-fidelity.md. His first form ran a 28s cycle that stopped him
/// attacking, walked him down a path, summoned two Inhibitor Sikars and ended in a sanctuary event.
/// None of that is in the pattern. Retail runs two battle timers instead:
/// <list type="bullet">
/// <item>every 9s, self-cast Dragon Breath and drop three Magic Flames on the current target;</item>
/// <item>every 6s, check HP and send one siege artilleryman the first time it passes 85/65/45/20.</item>
/// </list>
/// Both sets of adds share one spawn id, so leaving the fight clears them together.
/// <para>
/// The pattern addresses only skill index 0 of his two, and its branch is named Blaze: skill 19657
/// is Dragon Breath (stack <c>IDYUN_RASTA_BLAZE</c>) and the branch spawns <c>IDYun_3Nmd_Blaze</c>,
/// so the index resolves unambiguously. Index 1, Interception Soldier Shout, is the sanctuary shield
/// the old cycle applied; no branch casts it, so it stays listed but silent.
/// </para>
/// </remarks>
[AIName("captain_xasta")]
public class CaptainXastaAI : AggressiveNpcAI
{
    private const int FirstFormNpcId = 217309;
    private const int SecondFormNpcId = 217310;

    /// <summary>The 9s beat, and the only skill index his pattern addresses.</summary>
    private const int BeatSkill = 19657;

    private const int SiegeArtilleryman = 282606;

    /// <summary>The flames the beat drops on whoever he is facing.</summary>
    private const int MagicFlame = 282390;
    private const int FlamesPerBeat = 3;
    private const float FlameSpread = 4f;
    private const long FlameLifeMillis = 15000L;

    /// <summary>One-shot summon steps, each sending a single artilleryman.</summary>
    private static readonly int[] SummonSteps = { 85, 65, 45, 20 };

    private readonly object stepLock = new object();
    private int stepsTaken;

    private ScheduledTask? phaseTask;
    private ScheduledTask? beatTask;
    private readonly AtomicBoolean isHome = new AtomicBoolean(true);

    public CaptainXastaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isHome.CompareAndSet(true, false))
        {
            if (GetNpcId() == FirstFormNpcId)
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500388);
                StartRetailTimers();
            }
            else
            {
                StartPhase2Task();
            }
        }
    }

    private void CancelPhaseTask()
    {
        Cancel(ref phaseTask);
        Cancel(ref beatTask);
        lock (stepLock)
        {
            stepsTaken = 0;
        }
    }

    private static void Cancel(ref ScheduledTask? task)
    {
        if (task != null && !task.IsDone())
            task.Cancel(true);
        task = null;
    }

    /// <summary>Retail's two battle timers: the 9s beat, and the 6s summon check.</summary>
    private void StartRetailTimers()
    {
        beatTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnBeatTick(); return ValueTask.CompletedTask; },
            TimeSpan.FromMilliseconds(6000), TimeSpan.FromMilliseconds(9000));

        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(
            _ => { OnSummonTick(); return ValueTask.CompletedTask; },
            TimeSpan.FromMilliseconds(6000), TimeSpan.FromMilliseconds(6000));
    }

    private bool Fighting() => !IsDead() && IsInState(AIState.FIGHT);

    private void OnBeatTick()
    {
        if (!Fighting())
            return;

        // The pattern casts this at OBJI_SELF, which is what makes it a breath rather than a nuke:
        // the flames it leaves behind are what actually hurts.
        NpcSkillCasting.QueueAtDataLevel(GetOwner(), BeatSkill, NpcSkillTargetAttribute.ME);
        if (GetOwner().GetTarget() is Creature target)
            DropFlames(target);
    }

    /// <summary>Scatters the beat's flames around <paramref name="target"/>, each burning out on its own.</summary>
    private void DropFlames(Creature target)
    {
        WorldPosition at = target.GetPosition();
        for (int i = 0; i < FlamesPerBeat; i++)
        {
            double angle = Rnd.NextFloat(360f) * Math.PI / 180.0;
            float distance = Rnd.NextFloat(FlameSpread);
            float x = at.GetX() + (float)(Math.Cos(angle) * distance);
            float y = at.GetY() + (float)(Math.Sin(angle) * distance);
            if (Spawn(MagicFlame, x, y, at.GetZ(), (sbyte)at.GetHeading()) is not Npc flame)
                continue;

            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                flame.GetController().DeleteIfAliveOrCancelRespawn();
                return ValueTask.CompletedTask;
            }, FlameLifeMillis);
        }
    }

    /// <summary>The summon timer acts only on the tick that first crosses one of its four steps.</summary>
    private void OnSummonTick()
    {
        if (!Fighting())
            return;

        int hp = GetLifeStats().GetHpPercentage();
        lock (stepLock)
        {
            if (stepsTaken >= SummonSteps.Length || hp >= SummonSteps[stepsTaken])
                return;
            stepsTaken++;
        }
        PacketSendUtility.BroadcastMessage(GetOwner(), 1500389);
        RndSpawnInRange(SiegeArtilleryman, 5);
    }

    private void StartPhase2Task()
    {
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19729, 60, GetOwner()).UseNoAnimationSkill();
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500392);
            }
            return ValueTask.CompletedTask;
        }, TimeSpan.FromMilliseconds(30000), TimeSpan.FromMilliseconds(30000));
    }

    /// <summary>Retail despawns his summons when he drops out of the fight or resets.</summary>
    private void DeleteHelpers()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        if (instance != null)
        {
            DeleteNpcs(instance.GetNpcs(SiegeArtilleryman));
            DeleteNpcs(instance.GetNpcs(MagicFlame));
        }
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
        CancelPhaseTask();
        if (GetNpcId() == FirstFormNpcId)
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500390);
            Spawn(SecondFormNpcId, 238.160f, 598.624f, 178.480f, (sbyte)0);
            DeleteHelpers();
            AIActions.DeleteOwner(this);
        }
        else
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500391);
            WorldMapInstance instance = GetPosition().GetWorldMapInstance();
            if (instance != null)
            {
                Npc ariana = instance.GetNpc(799668);
                if (ariana != null)
                {
                    ariana.GetEffectController().RemoveEffect(19921);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        ariana.GetSpawn().SetWalkerId("30028000016");
                        WalkManager.StartWalking((NpcAI)ariana.GetAi());
                        return ValueTask.CompletedTask;
                    }, 1000L);
                    PacketSendUtility.BroadcastMessage(ariana, 1500415, 4000);
                    PacketSendUtility.BroadcastMessage(ariana, 1500416, 13000);
                    ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(ariana, 19358, 60, ariana).UseNoAnimationSkill();
                        instance.SetDoorState(145, true);
                        DeleteNpcs(instance.GetNpcs(701156));
                        ThreadPoolManager.GetInstance().Schedule(_ => { ariana.GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, 13000L);
                        return ValueTask.CompletedTask;
                    }, 13000L);
                }
            }
        }
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        CancelPhaseTask();
        base.HandleDespawned();
    }

    protected override void HandleBackHome()
    {
        CancelPhaseTask();
        DeleteHelpers();
        isHome.Set(true);
        base.HandleBackHome();
    }
}
