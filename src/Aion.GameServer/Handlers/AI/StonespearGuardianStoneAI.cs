using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Poll;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/stonespearReach/StonespearGuardianStoneAI (@author Yeats).</summary>
[AIName("stonespear_guardian_stone")]
public class StonespearGuardianStoneAI : NpcAI
{
    private ScheduledTask task;

    public StonespearGuardianStoneAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        StartTask();
    }

    private void StartTask()
    {
        task = ThreadPoolManager.GetInstance().Schedule(() =>
        {
            PacketSendUtility.BroadcastPacket(GetOwner(), SM_SYSTEM_MESSAGE.STR_MSG_OBJ_End());
            GetOwner().GetController().Delete();
        }, 55000L); // message says 2mins but its actually only ~1min.
    }

    protected override void HandleDied()
    {
        base.HandleDied();
        GetOwner().GetController().Delete();
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
    }

    protected override void HandleDespawned()
    {
        if (task != null && !task.IsCancelled)
        {
            task.Cancel(true);
        }
        base.HandleDespawned();
    }

    public override bool Ask(AIQuestion question)
    {
        return question switch
        {
            AIQuestion.ALLOW_RESPAWN or AIQuestion.REWARD_AP_XP_DP_LOOT or AIQuestion.REWARD_LOOT or AIQuestion.ALLOW_DECAY => false,
            _ => base.Ask(question),
        };
    }
}
