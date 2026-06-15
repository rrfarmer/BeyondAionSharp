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
