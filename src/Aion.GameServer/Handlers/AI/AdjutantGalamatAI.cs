using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Ai;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theHexway/AdjutantGalamatAI (@author Sykra).</summary>
[AIName("adjutant_galamat")]
public class AdjutantGalamatAI : SummonerAI
{
    private readonly AtomicBoolean shieldPhase = new AtomicBoolean(false);
    private readonly AtomicInteger damageInShieldPhase = new AtomicInteger(0);
    private ScheduledTask? shieldPhaseEvaluationTask;
    private ScheduledTask? addSpawnTask;
    private float damageMultiplicator = 1.0f;

    public AdjutantGalamatAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleIndividualSpawnedSummons(Percentage percent)
    {
        switch (percent.GetPercent())
        {
            case 60:
            case 20:
                GetOwner().ClearQueuedSkills();
                GetOwner().QueueSkill(21799, 65, 25000);
                break;
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 21799)
        {
            shieldPhase.Set(true);
            addSpawnTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { RndSpawnInRange(219616, 12); return System.Threading.Tasks.ValueTask.CompletedTask; }, System.TimeSpan.FromMilliseconds(0), System.TimeSpan.FromMilliseconds(4000));
            shieldPhaseEvaluationTask = ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                shieldPhase.Set(false);
                if (addSpawnTask != null && !addSpawnTask.IsDone())
                    addSpawnTask.Cancel(true);
                List<Player> playersInRange = GetKnownList().StreamPlayers().Where(p => IsInRange(p, 30)).ToList();
                if (playersInRange.Count == 0)
                    return System.Threading.Tasks.ValueTask.CompletedTask;
                int dmgPerPlayer = damageInShieldPhase.GetAndSet(0) / playersInRange.Count;
                float multiplicatorToAdd = 0f;
                foreach (Player player in playersInRange)
                {
                    if (player.IsDead())
                    {
                        multiplicatorToAdd += 0.2f;
                        continue;
                    }
                    if (dmgPerPlayer > 0 && player.GetLifeStats().GetCurrentHp() <= dmgPerPlayer)
                        multiplicatorToAdd += 0.35f;
                }
                lock (this)
                {
                    if (damageMultiplicator == 1.0f)
                        damageMultiplicator = 1.2f;
                    damageMultiplicator += multiplicatorToAdd;
                    damageMultiplicator = System.Math.Min(damageMultiplicator, 2.7f);
                }
                return System.Threading.Tasks.ValueTask.CompletedTask;
            }, 25000L);
        }
        base.OnEndUseSkill(skillTemplate, skillLevel);
    }

    protected override void HandleBackHome()
    {
        ResetVariablesAndCancelTasks();
        base.HandleBackHome();
    }

    protected override void HandleDespawned()
    {
        ResetVariablesAndCancelTasks();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        ResetVariablesAndCancelTasks();
        base.HandleDied();
    }

    private void ResetVariablesAndCancelTasks()
    {
        shieldPhase.Set(false);
        damageInShieldPhase.Set(0);
        if (shieldPhaseEvaluationTask != null && !shieldPhaseEvaluationTask.IsDone())
        {
            shieldPhaseEvaluationTask.Cancel(true);
            shieldPhaseEvaluationTask = null;
        }
        if (addSpawnTask != null && !addSpawnTask.IsDone())
        {
            addSpawnTask.Cancel(true);
            addSpawnTask = null;
        }
    }

    public override float ModifyOwnerDamage(float damage, Creature effected, Effect effect)
    {
        return damage * damageMultiplicator;
    }

    public override float ModifyDamage(Creature attacker, float damage, Effect effect)
    {
        if (shieldPhase.Get())
        {
            if (GetEffectController().FindBySkillId(21799) == null)
            {
                GetOwner().ClearQueuedSkills();
                ResetVariablesAndCancelTasks();
            }
            else
            {
                damageInShieldPhase.AddAndGet((int)damage);
            }
        }
        return damage;
    }
}
