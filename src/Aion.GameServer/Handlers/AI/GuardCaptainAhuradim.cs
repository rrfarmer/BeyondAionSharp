using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Npcskill;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/sauroBase/GuardCaptainAhuradim.</summary>
[AIName("guard_captain_ahuradim")]
public class GuardCaptainAhuradim : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(100, 75, 50, 25);
    private readonly AtomicBoolean started = new AtomicBoolean();
    private readonly int[] generators = new int[] { 284437, 284445, 284446 };
    private ScheduledTask scheduledTask;

    public GuardCaptainAhuradim(Npc owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Retail's <c>IDVritra_Base_Boss2</c> raises the base alarm at fifty metres as it engages, and the
    /// supply base's guards answer by taking whoever he is fighting. Added to this class rather than
    /// translated into one, because everything else here is a Java port. See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly CombatAlarm alarm = new CombatAlarm(ShebanBladesmanAI.BaseAlarm, 50f);

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        alarm.Raise(GetOwner());
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        if (phaseHpPercent == 100)
        {
            StartGeneratorTask();
        }
        else
        {
            GetOwner().QueueSkill(21194, 1, 0, NpcSkillTargetAttribute.ME);
            if (Rnd.NextBoolean())
            {
                GetOwner().QueueSkill(21429, 1, 5000);
            }
            else
            {
                GetOwner().QueueSkill(21190, 1, 5000, NpcSkillTargetAttribute.ME);
            }
        }
    }

    private void StartGeneratorTask()
    {
        scheduledTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (hpPhases.GetCurrentPhase() > 0 && !GetOwner().GetEffectController().HasAbnormalEffect(21191))
            {
                foreach (Npc generator in GetPosition().GetWorldMapInstance().GetNpcs(284437))
                {
                    generator.SetTarget(GetOwner());
                    PacketSendUtility.BroadcastMessage(generator, 1501014);
                    scheduledTask = ThreadPoolManager.GetInstance().Schedule(_ =>
                    {
                        if (hpPhases.GetCurrentPhase() > 0 && !generator.IsDead())
                        {
                            PacketSendUtility.BroadcastMessage(generator, 1501015);
                            SkillEngine.SkillEngine.GetInstance().GetSkill(generator, 21200, 1, generator).UseWithoutPropSkill();
                        }

                        return ValueTask.CompletedTask;
                    }, System.TimeSpan.FromMilliseconds(15 * 1000));
                }
            }

            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(40 * 1000));
    }

    public override void OnStartUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20775:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500783); // Weaklings! I will destroy you myself!
                break;
            case 21190:
            case 21429:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500785); // I'm Achradim, the Steel Wall Protector!
                break;
            case 21194:
                PacketSendUtility.BroadcastMessage(GetOwner(), 1500786); // You're very persistent. Allow me to demonstrate my power!
                break;
        }
    }

    private void CancelTasks()
    {
        if (scheduledTask != null)
        {
            scheduledTask.Cancel(false);
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        CancelTasks();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTasks();
    }
}
