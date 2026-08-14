using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/dragonLordsRefuge/CalindiFlamelordAI (Cheatkiller, Yeats, Estrayl).</summary>
[AIName("IDTiamat_2_calindi_flamelord")]
public class CalindiFlamelordAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    /// <summary>
    /// Retail thresholds, from pattern IDTiamat_Kalrindy: four identical steps at 80/60/40/25
    /// that each cast and spawn the ground surukana, then a different finisher at 15. We ran
    /// only three of the repeats, at an invented 75/50/25, and finished at 12.
    /// See docs/retail-ai-fidelity.md.
    /// </summary>
    private readonly HpPhases hpPhases = new HpPhases(80, 60, 40, 25, 15);

    public CalindiFlamelordAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        BlazeEngraving();
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        switch (phaseHpPercent)
        {
            case 80:
            case 60:
            case 40:
            case 25:
                StartHallucinatoryVictoryEvent();
                break;
            case 15:
                GetOwner().QueueSkill(20942, 1);
                break;
        }
    }

    protected virtual void StartHallucinatoryVictoryEvent()
    {
        if (GetPosition().GetWorldMapInstance().GetNpc(730695) == null && GetPosition().GetWorldMapInstance().GetNpc(730696) == null)
            GetOwner().QueueSkill(20911, 1);
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        switch (skillTemplate.GetSkillId())
        {
            case 20911:
                Spawn(730695, 482.21f, 458.06f, 427.42f, (sbyte)98);
                Spawn(730696, 482.21f, 571.16f, 427.42f, (sbyte)22);
                RndSpawn(283133);
                break;
            case 20913:
                Creature target = GetRandomTarget();
                if (target != null)
                    Spawn(283131, target.GetX(), target.GetY(), target.GetZ(), (sbyte)0);
                break;
        }
    }

    protected virtual void BlazeEngraving()
    {
        if (Rnd.Chance() < 2 && GetPosition().GetWorldMapInstance().GetNpc(283131) == null)
            GetOwner().QueueSkill(20913, 60);
    }

    protected void RndSpawn(int npcId)
    {
        for (int i = 0; i < 10; i++)
        {
            RndSpawnInRange(npcId, 5, 20);
        }
    }

    protected Creature GetRandomTarget()
    {
        return GetAggroList().GetTarget(AggroTarget.RANDOM, 50);
    }
}
