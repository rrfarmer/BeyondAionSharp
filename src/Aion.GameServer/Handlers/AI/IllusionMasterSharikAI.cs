using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.SkillEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Illusionmaster Sharik (217425). Retail pattern <c>Raksha_MirrorMage_Nmd</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/raksang/IllusionMasterSharikAI (@author xTz). Retail-sourced corrections
/// below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>He calls two dispel statues to himself on every turn of his timer, and this port placed none.</b>
/// Retail's rung fires every thirty-seven seconds above half health and every thirty below it, opens by
/// despawning the pair before them, and spawns two more within two metres. They are bound here to
/// <c>servant</c>, which heals its master — so their absence made the fight materially easier: nothing
/// had to be killed to stop him healing.
/// </para>
/// <para>
/// The cadence was a flat forty seconds opening at three; retail has no forty anywhere in this pattern.
/// </para>
/// <para>
/// <b>Not translated.</b> The 35% roll retail puts on the below-half rung, the two broadcasts that
/// accompany it (12006 at fifteen metres, 1001 at fifty), and the teleport pair on messages 12100/12101
/// that moves him between the two mirror posts — this port picks the post from its own
/// <c>position</c> field instead, and nothing sends those messages.
/// </para>
/// </remarks>
[AIName("illusion_maseter_sharik")]
public class IllusionMasterSharikAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(80);
    private readonly AtomicBoolean startedEvent = new AtomicBoolean(false);
    private int position = 1;
    private int percent = 100;
    private ScheduledTask? phaseTask;

    public IllusionMasterSharikAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        WorldPosition p = GetPosition();
        if (p.GetX() == 738.065f && p.GetY() == 311.606f)
        {
            position = 1;
        }
        else
        {
            position = 2;
        }
    }

    protected override void HandleCreatureMoved(Creature creature)
    {
        if (creature is Player player && PositionUtil.IsInRange(GetOwner(), player, 30))
        {
            if (startedEvent.CompareAndSet(false, true))
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1401112);
            }
        }
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        PacketSendUtility.BroadcastToMap(GetOwner(), 1401136);
        if (position == 1)
        {
            Spawn(730446, 738.766f, 317.482f, 911.897f, (sbyte)0, 5);
        }
        else
        {
            Spawn(730447, 735.909f, 265.696f, 911.897f, (sbyte)0, 278);
        }
        StartPhaseTask();
    }

    private void StartPhaseTask()
    {
        // A chain rather than a fixed rate: retail re-arms whichever rung matches his health at the
        // moment the turn ends, and a fixed rate would freeze the delay chosen when the fight started --
        // so a Sharik who dropped below half kept the slower rung for the rest of the fight.
        phaseTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1401114);
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19981, 46, GetOwner()).UseNoAnimationSkill();
                CallStatues();
                ThreadPoolManager.GetInstance().Schedule(_ =>
                {
                    if (!IsDead())
                    {
                        SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19901, 44, GetOwner()).UseNoAnimationSkill();
                        if (percent <= 50)
                        {
                            ThreadPoolManager.GetInstance().Schedule(_ =>
                            {
                                if (!IsDead())
                                {
                                    SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19903, 44, GetOwner()).UseNoAnimationSkill();
                                }
                                return ValueTask.CompletedTask;
                            }, 13000L);
                        }
                    }
                    return ValueTask.CompletedTask;
                }, 3000L);
            }
            if (!IsDead())
                StartPhaseTask();
            return ValueTask.CompletedTask;
        }, (long)RungDelay().TotalMilliseconds);
    }

    /// <summary><c>BIDRaksha_StatueDispel</c>, the pair he calls to himself.</summary>
    private const int StatueDispel = 282576;
    private const int StatueCount = 2;

    /// <summary>Retail's <c>spawn_range</c> on that pair.</summary>
    private const float StatueSpread = 2f;

    /// <summary>
    /// Retail's two rungs for this: <c>BTIMERI_INDEX_3</c> every thirty-seven seconds above half health,
    /// and <c>BTIMERI_INDEX_0</c> every thirty below it.
    /// </summary>
    private static readonly System.TimeSpan AboveHalf = System.TimeSpan.FromSeconds(37);
    private static readonly System.TimeSpan BelowHalf = System.TimeSpan.FromSeconds(30);

    private System.TimeSpan RungDelay() =>
        GetLifeStats().GetHpPercentage() <= 50 ? BelowHalf : AboveHalf;

    /// <summary>
    /// Calls two dispel statues to himself, replacing the pair before them.
    /// </summary>
    /// <remarks>
    /// <b>Nothing in this port had ever placed these.</b> Retail's rung spawns two
    /// <c>BIDRaksha_StatueDispel</c> on himself within two metres, and opens by despawning the previous
    /// pair — so there are two at a time and they arrive with every turn of the timer. They are bound
    /// here to <c>servant</c>, which heals its master, so their absence made the fight materially
    /// easier: nothing had to be killed to stop him healing.
    /// </remarks>
    private void CallStatues()
    {
        foreach (Npc old in GetPosition().GetWorldMapInstance().GetNpcs(StatueDispel))
            old?.GetController().Delete();

        for (int i = 0; i < StatueCount; i++)
            RndSpawnInRange(StatueDispel, StatueSpread);
    }

    private void CancelPhaseTask()
    {
        if (phaseTask != null && !phaseTask.IsDone())
        {
            phaseTask.Cancel(true);
        }
    }

    protected override void HandleBackHome()
    {
        DespawnMirrors();
        CancelPhaseTask();
        base.HandleBackHome();
        PacketSendUtility.BroadcastToMap(GetOwner(), 1401137);
        if (position == 1)
        {
            Spawn(217425, 736.21704f, 270.8546f, 910.678f, (sbyte)53);
        }
        else
        {
            Spawn(217425, 738.065f, 311.606f, 910.678f, (sbyte)53);
        }
        AIActions.DeleteOwner(this);
    }

    private void DespawnMirrors()
    {
        WorldPosition p = GetPosition();
        if (p != null)
        {
            WorldMapInstance instance = p.GetWorldMapInstance();
            if (instance != null)
            {
                DeleteNpcs(instance.GetNpcs(730446));
                DeleteNpcs(instance.GetNpcs(730447));
            }
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

    protected override void HandleDespawned()
    {
        CancelPhaseTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        DespawnMirrors();
        CancelPhaseTask();
        GetPosition().GetWorldMapInstance().SetDoorState(294, true);
        GetPosition().GetWorldMapInstance().SetDoorState(295, true);
        base.HandleDied();
    }
}
