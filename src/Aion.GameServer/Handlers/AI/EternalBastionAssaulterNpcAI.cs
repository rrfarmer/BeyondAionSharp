using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/eternalBastion/EternalBastionAssaulterNpcAI (Cheatkiller, Estrayl).</summary>
[AIName("eternal_bastion_assaulter")]
public class EternalBastionAssaulterNpcAI : EternalBastionAggressiveNpcAI
{
    public EternalBastionAssaulterNpcAI(Npc owner)
        : base(owner)
    {
    }

    private int GetAggroRange()
    {
        int range = GetOwner().GetAggroRange();
        if (range < 11)
            range = 11;
        return range;
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        ThreadPoolManager.GetInstance().Schedule(_ => { StartWalking(); return ValueTask.CompletedTask; }, 3000L);
    }

    private void StartWalking()
    {
        WalkManager.StartWalking(this);
        GetOwner().UnsetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SmEmotion(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
    }

    public override bool IsDestinationReached()
    {
        GetOwner().UnsetState(CreatureState.WALK_MODE);
        PacketSendUtility.BroadcastPacket(GetOwner(), new SmEmotion(GetOwner(), EmotionType.CHANGE_SPEED, 0, GetObjectId()));
        HateCommander();
        return base.IsDestinationReached();
    }

    private void HateCommander()
    {
        Npc commander = GetPosition().GetWorldMapInstance().GetNpc(209516);
        if (commander == null)
            commander = GetPosition().GetWorldMapInstance().GetNpc(209517);
        if (commander != null && !GetOwner().GetAggroList().IsHating(commander) && PositionUtil.IsInRange(GetOwner(), commander, 20))
            GetOwner().GetAggroList().AddHate(commander, 100000);
    }

    protected override void HandleBackHome()
    {
        base.HandleBackHome();
        HateCommander();
    }
}
