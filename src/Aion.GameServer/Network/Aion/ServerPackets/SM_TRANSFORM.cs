using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TRANSFORM (Sweetkr, xTz, kecimis). Sends a creature's transform model + state + typed restriction flags (skills/fly/items/attack/jump/recall/move, panel id) from the live transform model.</summary>
public class SM_TRANSFORM : AionServerPacket
{
    private readonly Creature creature;

    public SM_TRANSFORM(Creature creature)
    {
        this.creature = creature;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(creature.GetObjectId());
        WriteD(creature.GetTransformModel().GetModelId());
        WriteH(creature.GetState());
        WriteF(0.25f);
        WriteF(2.0f);
        WriteC(creature.GetTransformModel().CantUseSkills() ? 1 : 0);
        WriteD(creature.GetTransformModel().GetType_().GetId());
        WriteC(creature.GetTransformModel().CantFly() ? 1 : 0);
        WriteC(creature.GetTransformModel().CantUseItems() ? 1 : 0);
        WriteC(creature.GetTransformModel().CantAttack() ? 1 : 0);
        WriteC(creature.GetTransformModel().CantJump() ? 1 : 0);
        WriteC(creature.GetTransformModel().CantRecall() ? 1 : 0);
        WriteC(creature.GetTransformModel().CantMove() ? 1 : 0);
        WriteD(creature.GetTransformModel().GetPanelId()); // display panel
    }
}
