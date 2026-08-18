using System.Threading.Tasks;
using Aion.GameServer.Model;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.SpawnEngine;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Should also be able to request support once by dropping below 35% HP.
/// Java parity: ai/worlds/panesterra/ahserionsflight/AhserionConstructDestroyerAI (@author Estrayl).
/// </summary>
[AIName("ahserion_construct_destroyer")]
public class AhserionConstructDestroyerAI : AhserionAggressiveNpcAI
{
    private readonly AtomicBoolean isActivated = new AtomicBoolean();

    public AhserionConstructDestroyerAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetSpawnTemplate().GetHandlerType() == SpawnHandlerType.ATTACKER)
        {
            GetOwner().GetController().AddTask(TaskId.DESPAWN,
                ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().GetController().DeleteIfAliveOrCancelRespawn(); return ValueTask.CompletedTask; }, (long)System.TimeSpan.FromMinutes(9).TotalMilliseconds));
        }
    }

    protected override void HandleCreatureAggro(Creature creature)
    {
        base.HandleCreatureAggro(creature);
        if (isActivated.CompareAndSet(false, true))
        {
            WorldPosition p = GetPosition();
            SpawnFor(297191, p.GetX() + 5, p.GetY() - 5, p.GetZ() + 0.5f, (sbyte)0, TrooperLife); // Ahserion Troopers Assassin
            SpawnFor(297191, p.GetX() - 5, p.GetY() + 5, p.GetZ() + 0.5f, (sbyte)0, TrooperLife); // Ahserion Troopers Assassin
        }
    }

    public override void OnEndUseSkill(SkillTemplate skillTemplate, int skillLevel)
    {
        if (skillTemplate.GetSkillId() == 17446 && skillLevel == 57)
            AddHateToRndTarget();
    }

    /// <summary>
    /// Retail gives these troopers three minutes. <b>The nine-minute schedule in this class deletes the
    /// destroyer itself, not them</b> — a bound on the summoner is not a bound on the summoned, which is
    /// the same distinction that hid four other rows behind a self-timed verdict.
    /// </summary>
    private const int TrooperLife = 180;

    private void DespawnAssassins()
    {
        GetKnownList().ForEachNpc(npc =>
        {
            if (npc.GetNpcId() == 297191)
                npc.GetController().DeleteIfAliveOrCancelRespawn();
        });
    }

    protected override void HandleBackHome()
    {
        DespawnAssassins();
        isActivated.Set(false);
        base.HandleBackHome();
    }

    protected override void HandleDied()
    {
        DespawnAssassins();
        base.HandleDied();
    }

    protected override void HandleDespawned()
    {
        DespawnAssassins();
        base.HandleDespawned();
    }
}
