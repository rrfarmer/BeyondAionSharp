using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.AI;

/// <summary>
/// @author Estrayl
/// </summary>
[AIName("wounded_scar")]
public class WoundedScarAI : GeneralNpcAI
{
    private AtomicBoolean isDestinationReached = new AtomicBoolean(false);

    public WoundedScarAI(Npc owner) : base(owner)
    {
    }

    public override bool CanThink()
    {
        return false;
    }

    protected override void HandleDialogStart(Player player)
    {
        Npc owner = GetOwner();
        owner.GetSpawn().SetWalkerId("9C671D72B623B88FDFAFF9E2AF491976B84AE720");
        owner.OverrideNpcType(CreatureType.INVULNERABLE);
        WalkManager.StartWalking((NpcAI)owner.GetAi());
    }

    protected override void HandleMoveArrived()
    {
        base.HandleMoveArrived();
        if (GetOwner().GetMoveController().IsStop() && isDestinationReached.CompareAndSet(false, true))
        {
            WorldPosition p = GetPosition();
            Spawn(281116, p.GetX(), p.GetY(), p.GetZ(), (sbyte)p.GetHeading());
            AIActions.DeleteOwner(this);
            return;
        }
        switch (GetMoveController().GetCurrentStep().GetStepIndex())
        {
            case 8:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18532, GetOwner(), GetOwner());
                Spawn(281148, 648.8706f, 1167.4102f, 143.6956f, (sbyte)2);
                Spawn(281148, 649.7064f, 1164.4081f, 143.4077f, (sbyte)6);
                Spawn(281148, 651.4119f, 1161.6421f, 143.6618f, (sbyte)11);
                GetOwner().SetState(CreatureState.ACTIVE, true);
                PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.RUN));
                break;
            case 15:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18532, GetOwner(), GetOwner());
                Spawn(281149, 530.7089f, 1179.5717f, 138.0460f, (sbyte)7);
                break;
            case 25:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18532, GetOwner(), GetOwner());
                Spawn(281150, 508.9217f, 1050.1077f, 123.8376f, (sbyte)87);
                break;
            case 26:
                SkillEngine.SkillEngine.GetInstance().ApplyEffectDirectly(18532, GetOwner(), GetOwner());
                Spawn(281151, 504.4358f, 1064.9199f, 126.04446f, (sbyte)53);
                Spawn(281151, 506.9358f, 1066.3199f, 126.04446f, (sbyte)53);
                break;
        }
    }
}
