using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RECIPE_DELETE (namedrisk). Removes a recipe from the client list (recipeId).</summary>
public class SM_RECIPE_DELETE : AionServerPacket
{
    private int recipeId;

    public SM_RECIPE_DELETE(int recipeId)
    {
        this.recipeId = recipeId;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(recipeId);
    }
}
