using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author xTz
/// </summary>
[AIName("raksha")]
public class RakshaAI : AggressiveNpcAI, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(75);
    private AtomicBoolean isAggred = new AtomicBoolean(false);
    private ScheduledTask phaseTask;

    public RakshaAI(Npc owner) : base(owner)
    {
    }

    protected override void HandleDespawned()
    {
        CancelPhaseTask();
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelPhaseTask();
        Spawn(730445, 1062.281f, 889.900f, 138.744f, (sbyte)29);
        base.HandleDied();
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isAggred.CompareAndSet(false, true))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1401152);
        }
        hpPhases.TryEnterNextPhase(this);
    }

    public void HandleHpPhase(int phaseHpPercent)
    {
        PacketSendUtility.BroadcastMessage(GetOwner(), 1401154);
        StartPhaseTask();
    }

    private void StartPhaseTask()
    {
        phaseTask = ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ =>
        {
            if (IsDead())
            {
                CancelPhaseTask();
            }
            else
            {
                SkillEngine.SkillEngine.GetInstance().GetSkill(GetOwner(), 19938, 46, GetOwner()).UseNoAnimationSkill();
                List<Player> players = GetLifedPlayers();
                if (players.Count != 0)
                {
                    int size = players.Count;
                    if (players.Count < 4)
                    {
                        foreach (Player p in players)
                        {
                            SpawnRubble(p);
                        }
                    }
                    else
                    {
                        int count = Rnd.Get(3, size);
                        for (int i = 0; i < count; i++)
                        {
                            SpawnRubble(Rnd.Get(players)!);
                        }
                    }
                }
            }
            return ValueTask.CompletedTask;
        }, System.TimeSpan.FromMilliseconds(3000), System.TimeSpan.FromMilliseconds(15000));
    }

    private void SpawnRubble(Player player)
    {
        float x = player.GetX();
        float y = player.GetY();
        float z = player.GetZ();
        if (x > 0 && y > 0 && z > 0)
        {
            ThreadPoolManager.GetInstance().Schedule(_ =>
            {
                if (!IsDead())
                {
                    Spawn(282325, x, y, z, (sbyte)0);
                }
                return ValueTask.CompletedTask;
            }, 3000L);
        }
    }

    private List<Player> GetLifedPlayers()
    {
        List<Player> players = new List<Player>();
        GetKnownList().ForEachPlayer(player =>
        {
            if (!player.IsDead())
            {
                players.Add(player);
            }
        });
        return players;
    }

    private void CancelPhaseTask()
    {
        if (phaseTask != null && !phaseTask.IsDone())
        {
            phaseTask.Cancel(true);
        }
    }

    protected override void HandleBackHome()
    {
        CancelPhaseTask();
        isAggred.Set(false);
        base.HandleBackHome();
        hpPhases.Reset();
    }
}
