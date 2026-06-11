using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LEARN_RECIPE (lord_rex). Confirms a learned recipe (recipeId).</summary>
public class SM_LEARN_RECIPE : AionServerPacket
{
    private int recipeId;

    public SM_LEARN_RECIPE(int recipeId)
    {
        this.recipeId = recipeId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(recipeId);
        WriteC(0); // 4.0
    }
}
