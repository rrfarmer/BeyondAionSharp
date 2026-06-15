using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// Java parity: ai/instance/unstableSplinterpath/UnstableYamennesAI (@author Ritsu, Luzien, Cheatkiller).
/// </summary>
[AIName("unstableyamennes")]
public class UnstableYamennesAI : AggressiveNpcAI
{
    private ScheduledTask? portalTask;
    private ScheduledTask? enrageTask;
    private readonly AtomicBoolean isStart = new AtomicBoolean();

    public UnstableYamennesAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleAttack(Creature creature)
    {
        base.HandleAttack(creature);
        if (isStart.CompareAndSet(false, true))
        {
            PacketSendUtility.BroadcastMessage(GetOwner(), 1500013); // Those who threaten the artefact shall be returned to the flow of Aether!
            StartTasks();
        }
    }

    private void StartTasks()
    {
        enrageTask = ThreadPoolManager.GetInstance().Schedule(_ => { GetOwner().QueueSkill(19098, 55, 0); return ValueTask.CompletedTask; }, 600000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(false); return ValueTask.CompletedTask; }, 60000L);
    }

    private void OnHealingDebuff()
    {
        WorldMapInstance instance = GetPosition().GetWorldMapInstance();
        DeleteNpcs(instance.GetNpcs(219586));
        GetOwner().QueueSkill(19282, 55);
        Spawn(219586, GetOwner().GetX() + 10, GetOwner().GetY() - 10, GetOwner().GetZ(), (sbyte)0);
        Spawn(219586, GetOwner().GetX() - 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0);
        Spawn(219586, GetOwner().GetX() + 10, GetOwner().GetY() + 10, GetOwner().GetZ(), (sbyte)0);
        GetOwner().ClearAttackedCount();
        PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_ResetAggro());
    }

    private void SpawnPortals(bool isTopSpawn)
    {
        Npc portalA = GetPosition().GetWorldMapInstance().GetNpc(219567);
        Npc portalB = GetPosition().GetWorldMapInstance().GetNpc(219579);
        Npc portalC = GetPosition().GetWorldMapInstance().GetNpc(219580);
        if (portalA == null && portalB == null && portalC == null)
        {
            PacketSendUtility.BroadcastToMap(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_IDAbRe_Core_NmdD_SummonStart());
            if (isTopSpawn)
            {
                Spawn(219567, 288.10f, 741.95f, 216.81f, (sbyte)3);
                Spawn(219579, 375.05f, 750.67f, 216.82f, (sbyte)59);
                Spawn(219580, 341.33f, 699.38f, 216.86f, (sbyte)59);
            }
            else
            {
                Spawn(219567, 303.69f, 736.35f, 198.7f, (sbyte)0);
                Spawn(219579, 335.19f, 708.92f, 198.9f, (sbyte)35);
                Spawn(219580, 360.23f, 741.07f, 198.7f, (sbyte)0);
            }
        }
        ThreadPoolManager.GetInstance().Schedule(_ => { OnHealingDebuff(); return ValueTask.CompletedTask; }, 3000L);
        portalTask = ThreadPoolManager.GetInstance().Schedule(_ => { SpawnPortals(!isTopSpawn); return ValueTask.CompletedTask; }, 60000L);
    }

    private void DeleteNpcs(List<Npc> npcs)
    {
        npcs.Where(n => n != null).ToList().ForEach(n => n.GetController().Delete());
    }

    private void CancelTasks()
    {
        if (portalTask != null && !portalTask.IsDone())
            portalTask.Cancel(true);
        if (enrageTask != null && !enrageTask.IsDone())
            enrageTask.Cancel(true);
    }

    protected override void HandleBackHome()
    {
        CancelTasks();
        base.HandleBackHome();
        GetOwner().GetController().Delete();
    }

    protected override void HandleDespawned()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(219586));
        base.HandleDespawned();
    }

    protected override void HandleDied()
    {
        CancelTasks();
        DeleteNpcs(GetPosition().GetWorldMapInstance().GetNpcs(219586));
        base.HandleDied();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.REWARD_LOOT or AIQuestion.REWARD_AP => false,
            _ => base.Ask(question),
        };
    }
}
