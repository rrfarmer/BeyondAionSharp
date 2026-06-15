using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/nightmareCircus/HarlequinLordReshkaAI (@author Ritsu).</summary>
[AIName("harlequinlordreshka")]
public class HarlequinLordReshkaAI : AggressiveNpcAI
{
    private readonly ScheduledTask[] openBoxesTasks = new ScheduledTask[2];
    private ScheduledTask spawnBoxesTask;

    public HarlequinLordReshkaAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        SpawnTerrorsBox();
        SpawnNightmaresBox();
        OpenBoxes();
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        CancelTasks();
        DespawnNpcs(831348, 831349);
    }

    protected override void HandleDespawned()
    {
        base.HandleDespawned();
        CancelTasks();
    }

    private void CancelTasks()
    {
        foreach (ScheduledTask openBoxesTask in openBoxesTasks)
        {
            if (openBoxesTask != null && !openBoxesTask.IsDone())
            {
                openBoxesTask.Cancel(true);
            }
        }
        if (spawnBoxesTask != null && !spawnBoxesTask.IsDone())
        {
            spawnBoxesTask.Cancel(true);
        }
    }

    private void SpawnTerrorsBox()
    {
        Spawn(831348, 507.42712f, 570.53394f, 199.50775f, (sbyte)30);
        Spawn(831348, 507.6568f, 560.18787f, 199.50775f, (sbyte)30);
        Spawn(831348, 512.9201f, 552.13983f, 199.50775f, (sbyte)30);
        Spawn(831348, 513.1767f, 577.7641f, 199.50775f, (sbyte)30);
        Spawn(831348, 521.76257f, 548.68469f, 199.50775f, (sbyte)30);
        Spawn(831348, 521.97913f, 583.61383f, 199.50775f, (sbyte)30);
        Spawn(831348, 529.46533f, 582.23792f, 199.50775f, (sbyte)30);
        Spawn(831348, 530.72772f, 549.67969f, 199.50775f, (sbyte)30);
        Spawn(831348, 536.98706f, 578.31433f, 199.50775f, (sbyte)30);
        Spawn(831348, 539.05304f, 552.27924f, 199.50775f, (sbyte)30);
        Spawn(831348, 542.64111f, 560.79907f, 200.3221f, (sbyte)30);
        Spawn(831348, 543.323f, 572.56665f, 199.50775f, (sbyte)30);
    }

    private void SpawnNightmaresBox()
    {
        spawnBoxesTask = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                Spawn(831349, 507.42712f, 570.53394f, 199.50775f, (sbyte)30);
                Spawn(831349, 507.6568f, 560.18787f, 199.50775f, (sbyte)30);
                Spawn(831349, 512.9201f, 552.13983f, 199.50775f, (sbyte)30);
                Spawn(831349, 513.1767f, 577.7641f, 199.50775f, (sbyte)30);
                Spawn(831349, 521.76257f, 548.68469f, 199.50775f, (sbyte)30);
                Spawn(831349, 521.97913f, 583.61383f, 199.50775f, (sbyte)30);
                Spawn(831349, 529.46533f, 582.23792f, 199.50775f, (sbyte)30);
                Spawn(831349, 530.72772f, 549.67969f, 199.50775f, (sbyte)30);
                Spawn(831349, 536.98706f, 578.31433f, 199.50775f, (sbyte)30);
                Spawn(831349, 539.05304f, 552.27924f, 199.50775f, (sbyte)30);
                Spawn(831349, 542.64111f, 560.79907f, 200.3221f, (sbyte)30);
                Spawn(831349, 543.323f, 572.56665f, 199.50775f, (sbyte)30);
            }
            return ValueTask.CompletedTask;
        }, 36000L);
    }

    private void OpenBoxes()
    {
        openBoxesTasks[0] = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                AIActions.UseSkill(this, 21477);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501146);
            }
            return ValueTask.CompletedTask;
        }, 30000L);
        openBoxesTasks[1] = ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            if (!IsDead())
            {
                AIActions.UseSkill(this, 21477);
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501147);
                PacketSendUtility.BroadcastMessage(GetOwner(), Rnd.NextBoolean() ? 1501145 : 1501137, 2000);
            }
            return ValueTask.CompletedTask;
        }, 60000L);
    }

    private void DespawnNpcs(params int[] npcIds)
    {
        foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(npcIds))
        {
            npc.GetController().Delete();
        }
    }
}
