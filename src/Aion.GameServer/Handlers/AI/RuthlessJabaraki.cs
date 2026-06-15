using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Controllers.Attack;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Yeats
/// </summary>
[AIName("ruthless_jabaraki")]
public class RuthlessJabaraki : IDSweep_Bosses, HpPhases.PhaseHandler
{
    private readonly HpPhases hpPhases = new HpPhases(96, 80, 60, 45, 40, 35);
    private List<Npc> spawnedAdds = new List<Npc>();

    public RuthlessJabaraki(Npc owner) : base(owner)
    {
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
                SpawnAdds(1);
                break;
            case 80:
                SpawnAdds(1);
                break;
            case 65:
                SpawnAdds(2);
                break;
            case 50:
                SpawnAdds(3);
                break;
            case 35:
                SpawnAdds(4);
                break;
            case 20:
                SpawnAdds(5);
                break;
        }
    }

    private void SpawnAdds(int stage)
    {
        switch (stage)
        {
            case 1:
                spawnedAdds.Add((Npc)Spawn(235631, 541.625f, 407.0712f, 395.46875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235631, 538.86127f, 389.05258f, 395.4342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235631, 558.6854f, 382.02362f, 395.16785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235631, 564.9021f, 400.29718f, 395.46262f, (sbyte)69));
                break;
            case 2:
                spawnedAdds.Add((Npc)Spawn(235630, 541.625f, 407.0712f, 395.46875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235630, 538.86127f, 389.05258f, 395.4342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235630, 558.6854f, 382.02362f, 395.16785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235630, 564.9021f, 400.29718f, 395.46262f, (sbyte)69));

                spawnedAdds.Add((Npc)Spawn(235630, 540.625f, 406.0712f, 395.76875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235630, 539.86127f, 390.05258f, 395.7342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235630, 559.6854f, 383.02362f, 395.46785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235630, 565.9021f, 401.29718f, 395.76262f, (sbyte)69));
                break;
            case 3:
                spawnedAdds.Add((Npc)Spawn(235629, 541.625f, 407.0712f, 395.46875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235629, 538.86127f, 389.05258f, 395.4342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235629, 558.6854f, 382.02362f, 395.16785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235629, 564.9021f, 400.29718f, 395.46262f, (sbyte)69));

                spawnedAdds.Add((Npc)Spawn(235629, 540.625f, 406.0712f, 395.76875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235629, 539.86127f, 390.05258f, 395.7342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235629, 559.6854f, 383.02362f, 395.46785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235629, 565.9021f, 401.29718f, 395.76262f, (sbyte)69));
                break;
            case 4:
                spawnedAdds.Add((Npc)Spawn(235630, 541.625f, 407.0712f, 395.46875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235630, 538.86127f, 389.05258f, 395.4342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235630, 558.6854f, 382.02362f, 395.16785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235630, 564.9021f, 400.29718f, 395.46262f, (sbyte)69));

                spawnedAdds.Add((Npc)Spawn(235630, 540.625f, 406.0712f, 395.76875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235630, 539.86127f, 390.05258f, 395.7342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235630, 559.6854f, 383.02362f, 395.46785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235630, 565.9021f, 401.29718f, 395.76262f, (sbyte)69));

                spawnedAdds.Add((Npc)Spawn(235630, 541.625f, 407.0712f, 396.06875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235630, 540.86127f, 391.05258f, 396.0342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235630, 560.6854f, 384.02362f, 395.86785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235630, 566.9021f, 402.29718f, 395.86262f, (sbyte)69));
                break;
            case 5:
                spawnedAdds.Add((Npc)Spawn(235631, 541.625f, 407.0712f, 395.46875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235631, 538.86127f, 389.05258f, 395.4342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235631, 558.6854f, 382.02362f, 395.16785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235631, 564.9021f, 400.29718f, 395.46262f, (sbyte)69));

                spawnedAdds.Add((Npc)Spawn(235631, 540.625f, 406.0712f, 395.76875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235631, 539.86127f, 390.05258f, 395.7342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235631, 559.6854f, 383.02362f, 395.46785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235631, 565.9021f, 401.29718f, 395.76262f, (sbyte)69));

                spawnedAdds.Add((Npc)Spawn(235631, 541.625f, 407.0712f, 396.06875f, (sbyte)110));
                spawnedAdds.Add((Npc)Spawn(235631, 540.86127f, 391.05258f, 396.0342f, (sbyte)8));
                spawnedAdds.Add((Npc)Spawn(235631, 560.6854f, 384.02362f, 395.86785f, (sbyte)43));
                spawnedAdds.Add((Npc)Spawn(235631, 566.9021f, 402.29718f, 395.86262f, (sbyte)69));
                break;
        }
        StartHate();
    }

    private void StartHate()
    {
        Creature mostHated = GetAggroList().GetTarget(AggroTarget.MOST_HATED);
        foreach (Npc npc in spawnedAdds)
        {
            if (npc != null && !npc.IsDead())
            {
                npc.GetAggroList().AddHate(mostHated, 1);
            }
        }
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        hpPhases.Reset();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
    }
}
