using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/rakes/EngineerLahulahuAI (@author xTz).
/// </summary>
/// <summary>
/// Engineer Lahulahu (215080), Steel Rake. Retail pattern <c>IDSlk_Engineer</c>.
/// </summary>
/// <remarks>
/// Retail-sourced notes; see docs/retail-ai-fidelity.md. Found by <c>audit_hp_phases.py</c>, whose row
/// is <c>ours [95, 25]</c> against <c>retail [25]</c>.
/// <para>
/// <b>The 95 is this port's stand-in for entering combat and is not a defect.</b> Retail has no
/// ninety-five per cent threshold; its <c>on_enter_attack_state</c> arms two battle timers, adds ten
/// thousand hate to whoever pulled, and shouts. Firing our setup from a phase that trips almost
/// immediately reaches the same place, and the audit row is a false positive of the same family as the
/// openings measured from an HP phase.
/// </para>
/// <para>
/// <b>His summon wave is here now.</b> It was recorded as blocked on waypoints and on route data, and
/// neither was true. <c>on_arrived_at_waypoint</c> is reachable — <c>MoveArrived</c> fires at every route
/// step — and his route was already in our own data: spawn <c>walker_id</c>
/// <c>02692E8AA2C2793A7801E13C574871619504EEF9</c>, twenty-one steps, whose points are the client's
/// <c>IDShulackShip_1F_Engineer_MobPath</c> to two decimals and whose first point is his spawn to five
/// millimetres.
/// </para>
/// <para>
/// Retail arms <c>BTIMERI_INDEX_9</c> at <b>3500</b> from waypoints <b>2, 6, 12 and 16</b>. When it
/// fires, a probability cascade picks one of nine <c>BIDShulack_EngineerSum*</c> npcs by health band, and
/// <b>only the first rung of each band re-arms</b>, at 6500 — so the wave continues while that roll keeps
/// winning and otherwise delivers one more summon and stops. The top band does not re-arm at all.
/// </para>
/// <para>
/// <b>This is a patrol mechanic, not a fight mechanic</b>, which is what makes it safe to spawn npcs with
/// <c>live_time=0</c>. Every rung carries <c>despawn_at_attack_state=TRUE</c>: the nozzles accumulate
/// while he walks his round and go when he is pulled. That is modelled here by clearing them when he
/// enters combat, without which <c>live_time=0</c> would leave them standing for the life of the instance.
/// </para>
/// <para>
/// <b>Retail's health bands leave holes and they are reproduced.</b> They are
/// <c>is_hp_lower_than 25</c>, then <c>is_hp_in_boundary</c> 26-50, 51-75 and 75-100, all exclusive at
/// both ends. So nothing at all is summoned at exactly 25, 26, 50, 51 or 75 per cent. The timer fires and
/// no rung matches — which is what the data says, so it is what happens here.
/// </para>
/// <para>
/// <b>Not translated.</b> The shout on each waypoint rung
/// (<c>STR_CHAT_BIDShulack_Engineer_45_Ah_AIPattern_1</c>) and the <c>SKILLI_INDEX_9</c> cast beside it —
/// the first has no message id we can resolve and the second is the skill-index blocker. And the
/// <c>on_message 6647</c> path, which arms the same timer at 1000 and despawns <c>SPAWN_ID_1</c>: nothing
/// in this port sends 6647, so there is no sender to hang it on. The <c>is_aerial_spawn</c> flag noted
/// here previously as unmodelled is <c>FALSE</c> on every one of these rungs, so there was never anything
/// to model.
/// </para>
/// </remarks>
[AIName("engineerlahulahu")]
public class EngineerLahulahuAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(95, 25);
    private int skill = 18153;
    private Npc npc;
    private Npc npc1;
    private Npc npc2;
    private Npc npc3;
    private Npc npc4;
    private Npc npc5;
    private Npc npc6;
    private Npc npc7;
    private Npc npc8;
    private Npc npc9;
    private Npc npc10;
    private Npc npc11;

    public EngineerLahulahuAI(Npc owner)
        : base(owner)
    {
    }

    private void RegisterNpcs()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        npc = instance.GetNpc(281111);
        npc1 = instance.GetNpc(281325);
        npc2 = instance.GetNpc(281323);
        npc3 = instance.GetNpc(281322);
        npc4 = instance.GetNpc(281326);
        npc5 = instance.GetNpc(281113);
        npc6 = instance.GetNpc(281324);
        npc7 = instance.GetNpc(281109);
        npc8 = instance.GetNpc(281112);
        npc9 = instance.GetNpc(281114);
        npc10 = instance.GetNpc(281108);
        npc11 = instance.GetNpc(281110);
    }

    /// <summary>Retail's four summoning waypoints on his twenty-one-step route.</summary>
    public static readonly int[] SummonWaypoints = { 2, 6, 12, 16 };

    /// <summary>Retail's <c>BTIMERI_INDEX_9</c> delay from a waypoint rung.</summary>
    public const long SummonDelayMillis = 3500L;

    /// <summary>Retail's re-arm, carried by the first rung of a band and by no other.</summary>
    public const long SummonRepeatMillis = 6500L;

    /// <summary>
    /// <c>SPAWN_LOCATION_WAY_POINT_START</c> on <c>BIDShulack_EngineerSum_NPCPath</c>: the first point of
    /// that path, from <c>Map/Worlds/idshulackship</c>. The path has three points spanning barely a metre,
    /// so it places the summon rather than walking it anywhere.
    /// </summary>
    public const float SummonX = 688.590820f;
    public const float SummonY = 509.239136f;
    public const float SummonZ = 868.099976f;

    private readonly List<Npc> nozzles = new List<Npc>();

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        DespawnNozzles();
        hpPhases.TryEnterNextPhase(this);
    }

    /// <summary>Retail's <c>despawn_at_attack_state=TRUE</c>, carried by every summon rung.</summary>
    private void DespawnNozzles()
    {
        foreach (Npc nozzle in nozzles)
        {
            if (nozzle != null && nozzle.IsSpawned())
                nozzle.GetController().Delete();
        }
        nozzles.Clear();
    }

    /// <summary>Retail's <c>on_arrived_at_waypoint</c> rungs at indices 2, 6, 12 and 16.</summary>
    /// <remarks>
    /// The index is read before <c>base</c>: the base handler runs <c>WalkManager.ChooseNextRouteStep</c>,
    /// which advances the controller, so after it the index is the point he is leaving for.
    /// </remarks>
    protected override void HandleMoveArrived()
    {
        RouteStep arrived = GetMoveController().GetCurrentStep();
        base.HandleMoveArrived();
        if (arrived == null || System.Array.IndexOf(SummonWaypoints, arrived.GetStepIndex()) < 0)
            return;
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            SummonNozzle();
            return ValueTask.CompletedTask;
        }, SummonDelayMillis);
    }

    /// <summary>
    /// One turn of retail's <c>BTIMERI_INDEX_9</c> ladder: pick the band by health, run its probability
    /// cascade, place one nozzle, and re-arm only if the band's first rung was the one that won.
    /// </summary>
    private void SummonNozzle()
    {
        if (IsDead() || !GetOwner().IsSpawned())
            return;

        int hp = GetLifeStats().GetHpPercentage();
        int nozzleId;
        bool rearm = false;

        // The boundaries below are retail's, exclusive at both ends, so 25, 26, 50, 51 and 75 fall through
        // every band and summon nothing at all. That is the data, not an oversight here.
        if (hp < 25)
        {
            if (Rnd.Chance() < 20) { nozzleId = NozzleC; rearm = true; }
            else if (Rnd.Chance() < 40) nozzleId = NozzleF;
            else if (Rnd.Chance() < 60) nozzleId = NozzleG;
            else if (Rnd.Chance() < 80) nozzleId = NozzleH;
            else nozzleId = NozzleI;
        }
        else if (hp > 26 && hp < 50)
        {
            if (Rnd.Chance() < 20) { nozzleId = NozzleB; rearm = true; }
            else if (Rnd.Chance() < 40) nozzleId = NozzleF;
            else if (Rnd.Chance() < 60) nozzleId = NozzleG;
            else if (Rnd.Chance() < 80) nozzleId = NozzleH;
            else nozzleId = NozzleI;
        }
        else if (hp > 51 && hp < 75)
        {
            if (Rnd.Chance() < 25) { nozzleId = NozzleA; rearm = true; }
            else if (Rnd.Chance() < 50) nozzleId = NozzleD;
            else if (Rnd.Chance() < 75) nozzleId = NozzleE;
            else nozzleId = NozzleH;
        }
        else if (hp > 75 && hp < 100)
        {
            // This band does not re-arm at all -- retail hangs no timer on any of its three rungs.
            if (Rnd.Chance() < 33) nozzleId = NozzleD;
            else if (Rnd.Chance() < 66) nozzleId = NozzleE;
            else if (hp > 76) nozzleId = NozzleH;
            else return;
        }
        else
        {
            return;
        }

        if (Spawn(nozzleId, SummonX, SummonY, SummonZ, (sbyte)0) is Npc nozzle)
            nozzles.Add(nozzle);

        if (rearm)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                SummonNozzle();
                return ValueTask.CompletedTask;
            }, SummonRepeatMillis);
        }
    }

    // BIDShulack_EngineerSum{A..I}_45_An, resolved through ai_binding.tsv.
    private const int NozzleA = 281103;
    private const int NozzleB = 281104;
    private const int NozzleC = 281105;
    private const int NozzleD = 281106;
    private const int NozzleE = 281107;
    private const int NozzleF = 281293;
    private const int NozzleG = 281294;
    private const int NozzleH = 281295;
    private const int NozzleI = 281351;

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 95:
                RegisterNpcs();
                AIActions.UseSkill(this, 18131);
                UseSkills();
                break;
            case 25:
                GetEffectController().RemoveEffect(18131);
                AIActions.UseSkill(this, 18132);
                break;
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    private void DoSchedule()
    {
        ThreadPoolManager.GetInstance().Schedule(_ => { UseSkills(); return ValueTask.CompletedTask; }, 10000L);
    }

    private void UseSkills()
    {
        if (GetPosition().IsSpawned() && !IsDead() && hpPhases.GetCurrentPhase() > 0)
        {
            int rnd = Rnd.Get(1, 8);
            switch (rnd)
            {
                case 1:
                    if (npc != null)
                    {
                        npc.SetTarget(npc);
                        npc.GetController().UseSkill(skill);
                    }
                    if (npc1 != null)
                    {
                        npc1.SetTarget(npc1);
                        npc1.GetController().UseSkill(skill);
                    }
                    break;
                case 2:
                    if (npc2 != null)
                    {
                        npc2.SetTarget(npc2);
                        npc2.GetController().UseSkill(skill);
                    }
                    if (npc3 != null)
                    {
                        npc3.SetTarget(npc3);
                        npc3.GetController().UseSkill(skill);
                    }
                    break;
                case 3:
                    if (npc4 != null)
                    {
                        npc4.SetTarget(npc4);
                        npc4.GetController().UseSkill(skill);
                    }
                    if (npc5 != null)
                    {
                        npc5.SetTarget(npc5);
                        npc5.GetController().UseSkill(skill);
                    }
                    break;
                case 4:
                    if (npc6 != null)
                    {
                        npc6.SetTarget(npc6);
                        npc6.GetController().UseSkill(skill);
                    }
                    if (npc7 != null)
                    {
                        npc7.SetTarget(npc7);
                        npc7.GetController().UseSkill(skill);
                    }
                    break;
                case 5:
                    if (npc8 != null)
                    {
                        npc8.SetTarget(npc8);
                        npc8.GetController().UseSkill(skill);
                    }
                    break;
                case 6:
                    if (npc9 != null)
                    {
                        npc9.SetTarget(npc9);
                        npc9.GetController().UseSkill(skill);
                    }
                    break;
                case 7:
                    if (npc10 != null)
                    {
                        npc10.SetTarget(npc10);
                        npc10.GetController().UseSkill(skill);
                    }
                    break;
                case 8:
                    if (npc11 != null)
                    {
                        npc11.SetTarget(npc11);
                        npc11.GetController().UseSkill(skill);
                    }
                    break;
            }
            DoSchedule();
        }
    }
}
