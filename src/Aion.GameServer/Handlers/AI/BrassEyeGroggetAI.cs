using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Brass-Eye Grogget (215081), Steel Rake. Retail pattern <c>IDSlk_Captain</c>.
/// </summary>
/// <remarks>
/// Java parity: ai/instance/rakes/BrassEyeGroggetAI (@author xTz). Retail-sourced additions below; see
/// docs/retail-ai-fidelity.md.
/// <para>
/// <b>His whole patrol was missing, and the class asked for it.</b> The comment below still reads
/// "todo 4 towers in the room center and fix coordinates of monsters / need snif". The sniff was not
/// needed: he already walks retail's route -- spawn <c>walker_id</c>
/// <c>055B73AA897B0E07D287848D3AD6EBCABB7DD93D</c>, twelve steps, which is the client's
/// <c>IDShip_mobpath_ShulackCaptainNmd_46_Ah</c> with its first point <b>exactly</b> on his spawn.
/// </para>
/// <para>
/// <b>Waypoint 4 drops one stigma stone per lap</b>, at four absolute coordinates, guarded in retail by
/// <c>unset_flag_var</c> on <c>FLAGVARI_ZETA_4</c> down to <c>ZETA_1</c> -- test-and-unset, so each fires
/// once and the four arrive on four successive laps. Those are the four towers the comment was asking
/// about.
/// </para>
/// <para>
/// <b>Waypoint 10 brings one wave per lap</b>, at his own point, guarded by <c>set_flag_var</c> on
/// <c>DELTA_1</c> to <c>DELTA_3</c> with a fourth rung carrying no guard at all -- so the first three laps
/// each bring a different wave and every lap after that brings the fourth.
/// </para>
/// <para>
/// <b>All ten spawns carry <c>live_time=0</c> and <c>despawn_at_attack_state=TRUE</c></b>, so this is
/// patrol furniture: it accumulates while he walks his round and goes when he is pulled. The second half
/// is modelled here, without which a permanent spawn would leave the room full for the life of the
/// instance.
/// </para>
/// <para>
/// <b>Not translated.</b> The HP-percent helper spawning below is aionemu's invention -- its own comment
/// says so -- and retail's spawn actions never mention 281181-281184 or 281187. It is left alone rather
/// than removed: it fires in combat where all of the above fires on patrol, so the two do not collide,
/// and deleting a fight's adds is a bigger decision than adding the patrol they were standing in for.
/// <c>audit_summon_ids.py</c> keeps the row. Also untranslated: the shouts and <c>use_skill</c> beside
/// each rung, the broadcasts 6661-6667 (no listener in this port sends or answers them), and the later
/// index-4 rungs which re-arm battle timers once the four stones are used up.
/// </para>
/// </remarks>
[AIName("brasseyegrogget")]
public class BrassEyeGroggetAI : SummonerAI
{
    public BrassEyeGroggetAI(Npc owner)
        : base(owner)
    {
    }

    // todo 4 towers in the room center and fix coordinates of monsters
    // need snif

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
    }

    /// <summary>Retail's <c>on_arrived_at_waypoint</c> index for the stigma stones.</summary>
    public const int StoneWaypoint = 4;

    /// <summary>Retail's <c>on_arrived_at_waypoint</c> index for the wave.</summary>
    public const int WaveWaypoint = 10;

    /// <summary>
    /// The four stones and where retail puts them -- <c>SPAWN_LOCATION_ABSOLUTE</c>, one per lap, in the
    /// priority order the pattern lists them.
    /// </summary>
    private static readonly (int NpcId, float X, float Y, float Z)[] StigmaStones =
    {
        (281191, 397.43f, 504.22f, 1073.3f),
        (281192, 397.06f, 516.37f, 1073.3f),
        (281193, 409.63f, 504.49f, 1073.3f),
        (281194, 409.05f, 516.65f, 1073.3f),
    };

    /// <summary>
    /// The waves, at <c>SPAWN_LOCATION_MY_POINT</c>. The last rung carries no flag guard, so once the
    /// first three are spent every further lap brings the fourth again.
    /// </summary>
    private static readonly int[] Waves = { 281198, 281199, 281200, 281201 };

    private int stonesPlaced;
    private int wavesCalled;
    private readonly List<Npc> patrolSpawns = new List<Npc>();

    /// <summary>Retail's two waypoint ladders.</summary>
    /// <remarks>
    /// The index is read before <c>base</c>: the base handler runs <c>ChooseNextRouteStep</c>, which
    /// advances the controller, so afterwards the index is the point he is leaving for.
    /// </remarks>
    protected override void HandleMoveArrived()
    {
        RouteStep arrived = GetMoveController().GetCurrentStep();
        base.HandleMoveArrived();
        if (arrived == null)
            return;

        if (arrived.GetStepIndex() == StoneWaypoint && stonesPlaced < StigmaStones.Length)
        {
            (int npcId, float x, float y, float z) = StigmaStones[stonesPlaced++];
            Remember(Spawn(npcId, x, y, z, (sbyte)0));
        }
        else if (arrived.GetStepIndex() == WaveWaypoint)
        {
            int wave = Waves[System.Math.Min(wavesCalled, Waves.Length - 1)];
            wavesCalled++;
            Remember(Spawn(wave, GetOwner().GetX(), GetOwner().GetY(), GetOwner().GetZ(),
                (sbyte)GetOwner().GetHeading()));
        }
    }

    private void Remember(object spawned)
    {
        if (spawned is Npc npc)
            patrolSpawns.Add(npc);
    }

    /// <summary>Retail's <c>despawn_at_attack_state=TRUE</c>, carried by all ten of his spawn rungs.</summary>
    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        foreach (Npc spawned in patrolSpawns)
        {
            if (spawned != null && spawned.IsSpawned())
                spawned.GetController().Delete();
        }
        patrolSpawns.Clear();
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        Spawn(percent.GetPercent());
    }

    private void Spawn(int percent)
    {
        int i = 0;
        if (percent < 81 && percent > 60)
        {
            i = 1;
        }
        else if (percent < 61 && percent > 30)
        {
            i = 2;
        }
        else if (percent < 31)
        {
            i = 3;
        }
        int nrSpawn = i;

        // to do move boss to initial position and set pause move and atack
        // after 9 sec first spawn
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SpawnHelpers1(nrSpawn);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 9000L);
    }

    private void SpawnHelpers1(int nrSpawn)
    {
        switch (nrSpawn)
        {
            case 1:
                Spawn(281184, 381.3756f, 495.24835f, 1072.1212f, (sbyte)13);
                Spawn(281181, 379.4199f, 495.36453f, 1072.1212f, (sbyte)13);
                break;
            case 2:
                Spawn(281184, 383.76724f, 527.02856f, 1072.1212f, (sbyte)100);
                Spawn(281181, 381.26767f, 526.40845f, 1072.1212f, (sbyte)100);
                break;
            case 3:
                Spawn(281182, 416.2482f, 500.6516f, 1071.8457f, (sbyte)52);
                Spawn(281182, 415.66647f, 519.5354f, 1071.8457f, (sbyte)52);
                break;
        }

        // next spawn after 35 sec
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SpawnHelpers2(nrSpawn);
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 35000L);
    }

    private void SpawnHelpers2(int nrSpawn)
    {
        switch (nrSpawn)
        {
            case 1:
                Spawn(281183, 383.26193f, 528.38403f, 1072.1212f, (sbyte)100);
                Spawn(281184, 383.76724f, 527.02856f, 1072.1212f, (sbyte)100);
                Spawn(281181, 381.26767f, 526.40845f, 1072.1212f, (sbyte)100);
                break;
            case 2:
                Spawn(281182, 429.55338f, 525.7714f, 1075.3801f, (sbyte)62);
                Spawn(281182, 429.52865f, 492.56076f, 1075.3801f, (sbyte)62);
                Spawn(281181, 376.42566f, 502.19736f, 1072.1212f, (sbyte)1);
                break;
            case 3:
                Spawn(281187, 376.42566f, 502.19736f, 1072.1212f, (sbyte)1);
                Spawn(281181, 381.26767f, 526.40845f, 1072.1212f, (sbyte)100);
                break;
        }

        // remove effect after 21 sec
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (GetEffectController().HasAbnormalEffect(18191))
            {
                GetEffectController().RemoveEffect(18191);
            }
            // to do move boss in the room center and remove pause
            // to do some skill boss use
            return System.Threading.Tasks.ValueTask.CompletedTask;
        }, 21000L);
    }
}
