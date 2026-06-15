using System;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/drakenspire/TwinDoorDestroyerAI (@author Estrayl).</summary>
[AIName("twin_door_destroyer")]
public class TwinDoorDestroyerAI : GeneralNpcAI
{
    private readonly AtomicBoolean isGateReached = new AtomicBoolean();

    public TwinDoorDestroyerAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        RemoveTrap();
        PacketSendUtility.BroadcastMessage(GetOwner(), 1501309, 2500);
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().IsStop())
        {
            if (isGateReached.CompareAndSet(false, true))
            {
                PacketSendUtility.BroadcastMessage(GetOwner(), 1501310);
                ScheduleGateAttack();
            }
        }
    }

    private void RemoveTrap()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(207128, 207129))
                npc.GetController().Delete();
            return ValueTask.CompletedTask;
        }, 1500L);
    }

    private void ScheduleGateAttack()
    {
        ThreadPoolManager.GetInstance().Schedule(_ =>
        {
            foreach (Npc npc in GetOwner().GetPosition().GetWorldMapInstance().GetNpcs(731580))
            {
                if (IsInRange(npc, 10))
                    SkillEngine.SkillEngine.GetInstance().GetSkill(npc, 20840, 1, npc).UseWithoutPropSkill();
            }

            PacketSendUtility.BroadcastMessage(GetOwner(), 1501311);
            return ValueTask.CompletedTask;
        }, 3500L);
    }
}
