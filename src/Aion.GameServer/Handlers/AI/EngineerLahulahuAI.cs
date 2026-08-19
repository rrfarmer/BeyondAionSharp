using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
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
/// <b>What is genuinely missing is his summon wave, and it is blocked on waypoints.</b> Below
/// twenty-five per cent retail runs a ladder on <c>BTIMERI_INDEX_9</c> that spawns one of nine
/// <c>BIDShulack_EngineerSum*</c> npcs — 281103-281107, 281293-281295 and 281351, all of which exist in
/// our data — chosen by a probability cascade (20, 40, 60, 80, then unguarded) and walked in on
/// <c>BIDShulack_EngineerSum_NPCPath</c>. There are three such ladders, one per health band.
/// </para>
/// <para>
/// <b>It cannot be written without inventing a trigger.</b> <c>INDEX_9</c> is armed at <b>3500</b> from
/// four <c>on_arrived_at_waypoint</c> rungs and at <b>1000</b> from an <c>on_message</c> — the wave is
/// started by Lahulahu reaching points on his own route, and neither his route nor the summons' path is
/// in our walker data. Only the ladder's first rung re-arms the timer, so in retail the wave continues
/// only while the twenty-per-cent roll keeps winning and otherwise delivers one more summon and stops;
/// that shape is consistent across all three bands and is worth preserving whenever the trigger exists.
/// </para>
/// <para>
/// <b>Also not translated:</b> the <c>is_aerial_spawn</c> guard those rungs carry, which this port has
/// no equivalent primitive for.
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

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        hpPhases.TryEnterNextPhase(this);
    }

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
