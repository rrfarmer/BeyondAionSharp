using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Berserk Anoha (855263), the Kaldor fortress boss. Retail pattern <c>LDF5_Fortress_Anoha</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/worlds/kaldor/BerserkAnohaAI (@author Ritsu, Estrayl). Retail-sourced corrections
/// below; see docs/retail-ai-fidelity.md.
/// <para>
/// <b>The commander he leaves belongs to whoever killed him, not to whoever holds the fortress.</b>
/// Retail's rung is <c>on_killed_by_user</c> split on <c>is_race from=OBJI_KILLER</c>: a
/// <c>pc_dark</c> killer gets <c>LDF5_Fortress_7011_Sonntag_E</c> and a <c>pc_light</c> killer gets
/// <c>LDF5_Fortress_7011_Solis_E</c>. This class picked from <see cref="occupier"/>, the fortress race
/// read when he spawned — so a raid that took him from the holding faction was handed <b>the holding
/// faction's</b> commander, which is the reward going to the side that lost the kill.
/// </para>
/// <para>
/// <b>And the commander stands thirty minutes, not sixty.</b> Retail's <c>live_time</c> is 1800.
/// </para>
/// <para>
/// <b>Not translated, and left alone deliberately:</b> retail's <c>on_wake_up</c> sets an idle timer of
/// <b>1200000</b> — twenty minutes — where this class removes him after an hour. The
/// <c>on_idle_timer</c> rung it drives only broadcasts and re-arms at 6000, so it is a warning beat
/// rather than the despawn, and nothing in the pattern removes him at all. His spawn and removal are
/// the siege schedule's business here, so the hour is left standing rather than guessed at.
/// </para>
/// <para>
/// <b>Also not translated:</b> the whole fight. Seven skill indices across five battle timers, with
/// target switches on <c>ATTACKERI_RANDOM_ONE_EXCEPT_CURRENT_TARGET</c> and a health ladder at 10, 40
/// and 100 — none of it reachable without the skill index. And the pair of
/// <c>set_condition_spawn_variable</c> calls on the kill (<c>7011_rewardcon_l_set</c>,
/// <c>7011_rewardcon_d_set</c>), which this port has no mechanism for.
/// </para>
/// </remarks>
[AIName("berserk_anoha")]
public class BerserkAnohaAI : AggressiveNpcAI
{
    private SiegeRace occupier;

    public BerserkAnohaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_ANOHA_SPAWN());
        ScheduleDespawn();
        occupier = SiegeService.GetInstance().GetFortress(7011).GetRace();
    }

    private void ScheduleDespawn()
    {
        GetOwner().GetController().AddTask(TaskId.DESPAWN,
            ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, TimeSpan.FromHours(1)));
    }

    protected override void HandleDespawned()
    {
        if (!IsDead())
            PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_ANOHA_DESPAWN());
        DespawnFlag();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        GetOwner().GetController().CancelTask(TaskId.DESPAWN);
        PacketSendUtility.BroadcastToWorld(SM_SYSTEM_MESSAGE.STR_MSG_ANOHA_DIE());
        DespawnFlag();
        CheckForFactionReward();
        base.HandleDied();
    }

    private void DespawnFlag()
    {
        Npc flag = GetOwner().GetPosition().GetWorldMapInstance().GetNpc(702618); // see AnohasSword AI
        if (flag != null)
            flag.GetController().Delete();
    }

    /// <summary>Commander Anoha, one per race. Retail picks by the killer's race.</summary>
    public const int AsmodianCommander = 804594;
    public const int ElyosCommander = 804595;

    /// <summary>Retail's <c>live_time</c> on the spawn command.</summary>
    public const int CommanderLifeSeconds = 1800;

    /// <summary>
    /// Retail's <c>on_killed_by_user</c>: <c>is_race from=OBJI_KILLER race_type=pc_dark</c> spawns one
    /// commander, <c>pc_light</c> the other.
    /// </summary>
    /// <remarks>
    /// Our runtime raises one death event rather than retail's killed-by-user, so the killer is taken
    /// as the player who did the most damage. That is the closest thing we have to
    /// <c>OBJI_KILLER</c>; it differs only when the killing blow and the bulk of the damage come from
    /// opposite factions, which on a fortress boss is the rare case rather than the normal one.
    /// </remarks>
    internal static int CommanderFor(Race killerRace) =>
        killerRace == Race.ELYOS ? ElyosCommander : AsmodianCommander;

    private void CheckForFactionReward()
    {
        if (GetAggroList().GetMostPlayerDamage() is not Player killer)
            return;

        Npc ca = (Npc)Spawn(CommanderFor(killer.GetRace()), 785.4833f, 458.4128f, 143.7177f, (sbyte)30);
        ca.GetController().AddTask(TaskId.DESPAWN, ThreadPoolManager.GetInstance().Schedule(
            _ => { ca.GetController().Delete(); return ValueTask.CompletedTask; },
            TimeSpan.FromSeconds(CommanderLifeSeconds)));
    }

    protected override void HandleCreatureSee(Creature creature)
    {
        base.HandleCreatureSee(creature);
        if (creature is Player player)
        {
            if (occupier == SiegeRace.ASMODIANS)
            {
                StartQuest(player, creature.GetRace() == Race.ELYOS ? 13818 : 23817);
            }
            else if (occupier == SiegeRace.ELYOS)
            {
                StartQuest(player, creature.GetRace() == Race.ELYOS ? 13817 : 23818);
            }
        }
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_DECAY or AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_LOOT => false,
            _ => base.Ask(question),
        };
    }

    private void StartQuest(Player player, int questId)
    {
        QuestState qs = player.GetQuestStateList().GetQuestState(questId);
        QuestEnv env = new QuestEnv(null, player, questId);
        if (qs == null || qs.IsStartable())
            QuestService.StartQuest(env);
    }
}
