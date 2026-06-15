using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/theShugoEmperorsVault/WatchmanHokuruki (Yeats).</summary>
[AIName("watchman_hokuruki")]
public class WatchmanHokuruki : IDSweep_Bosses, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(100, 75, 50, 25, 15);
    private readonly List<int> randomPositions = new List<int> { 1, 2, 3 };
    private readonly object randomPositionsLock = new object();

    public WatchmanHokuruki(Npc owner)
        : base(owner)
    {
        Shuffle(randomPositions);
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
            case 100:
            case 15:
                RndSpawn(235632, 4);
                break;
            case 75:
                SpawnAdds(GetRandomPosition(0));
                break;
            case 50:
                SpawnAdds(GetRandomPosition(1));
                break;
            case 25:
                SpawnAdds(GetRandomPosition(2));
                break;
        }
    }

    private int GetRandomPosition(int index)
    {
        lock (randomPositionsLock)
        {
            return randomPositions[index];
        }
    }

    private void SpawnAdds(int position)
    {
        switch (position)
        {
            case 1:
                Spawn(236083, 472.8545f, 644.94f, 395.50546f, (sbyte)113);
                Spawn(235649, 473.7f, 648.9f, 395.55615f, (sbyte)116);
                Spawn(235649, 471.75f, 641f, 395.27322f, (sbyte)116);
                break;
            case 2:
                Spawn(236083, 490f, 655.1424f, 394.69073f, (sbyte)84);
                Spawn(235649, 485.812f, 656.14f, 395.375f, (sbyte)90);
                Spawn(235649, 493.59f, 652.62f, 395.41345f, (sbyte)77);
                break;
            case 3:
                Spawn(236083, 493f, 629.34f, 394.89963f, (sbyte)41);
                Spawn(235649, 496.56f, 631.78f, 395.20126f, (sbyte)49);
                Spawn(235649, 488.86f, 627.54f, 395.27853f, (sbyte)38);
                break;
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
        Shuffle(randomPositions);
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
    }

    private void RndSpawn(int npcId, int count)
    {
        Creature mostHated = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
        for (int i = 0; i < count; i++)
        {
            Npc npc = (Npc)RndSpawnInRange(npcId, 3, 5);
            npc.GetAggroList().AddHate(mostHated, 1);
        }
    }

    // Java parity: Collections.shuffle(list) — Fisher-Yates from end, swap with Rnd.nextInt(i+1).
    private void Shuffle(List<int> list)
    {
        lock (randomPositionsLock)
        {
            for (int i = list.Count - 1; i >= 1; i--)
            {
                int j = Rnd.NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
