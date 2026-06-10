using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RECIPE_LIST (lord_rex). Sends the player's known recipe ids. Converges PlayerEnterWorldService. Set->ISet. AionServerPacket red-tolerated.</summary>
public class SM_RECIPE_LIST : AionServerPacket
{
    private readonly ISet<int> recipeIds;

    public SM_RECIPE_LIST(ISet<int> recipeIds)
    {
        this.recipeIds = recipeIds;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(recipeIds.Count);
        foreach (int id in recipeIds)
        {
            WriteD(id);
            WriteC(0);
        }
    }
}
