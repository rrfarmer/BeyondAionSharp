using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Event;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("drakenspire_beritra_minion")]
public class BeritraMinionAI : AggressiveNoLootNpcAI
{
    private readonly AtomicInteger followAttempts = new AtomicInteger();
    private ScheduledTask deathTask;

    public BeritraMinionAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            AggroPlayer();
            return ValueTask.CompletedTask;
        }, 500L);
        deathTask = ThreadPoolManager.GetInstance().Schedule(ct =>
        {
            int npcId = GetSkillNpcId();
            if (npcId != 0)
                Spawn(npcId, GetPosition().GetX(), GetPosition().GetY(), GetPosition().GetZ(), (sbyte)0);
            ThreadPoolManager.GetInstance().Schedule(ct2 =>
            {
                AIActions.Die(this);
                return ValueTask.CompletedTask;
            }, 500L);
            return ValueTask.CompletedTask;
        }, 20000L);
    }

    private void AggroPlayer()
    {
        Player p = GetKnownList().StreamPlayers().FirstOrDefault(pl => !pl.IsDead() && PositionUtil.IsInRange(pl, 152.38f, 518.68f, 1749.6f, 24));
        if (p != null)
            GetAggroList().AddHate(p, 10000);
    }

    private int GetSkillNpcId()
    {
        return GetNpcId() switch
        {
            855444 => 855447,
            855445 => 855448,
            855446 => 855449,
            _ => 0,
        };
    }

    private bool IsTargetInsideArena()
    {
        return PositionUtil.IsInRange(GetTarget(), 151.9f, 518.6f, 1749.6f, 26);
    }

    protected override void HandleTargetTooFar()
    {
        if (!IsTargetInsideArena())
        {
            if (followAttempts.IncrementAndGet() < 3)
            {
                ThreadPoolManager.GetInstance().Schedule(ct =>
                {
                    HandleTargetTooFar();
                    return ValueTask.CompletedTask;
                }, 2000L);
            }
            else
            {
                followAttempts.Set(0);
                GetAggroList().StopHating(GetTarget());
                Creature mostHated = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
                if (mostHated != null)
                    OnCreatureEvent(AiEventType.TARGET_CHANGED, mostHated);
                else
                    OnGeneralEvent(AiEventType.TARGET_GIVEUP);
            }
            return;
        }
        base.HandleTargetTooFar();
    }

    protected override void HandleDespawned()
    {
        if (deathTask != null && !deathTask.IsDone())
            deathTask.Cancel(true);
        base.HandleDespawned();
    }
}
