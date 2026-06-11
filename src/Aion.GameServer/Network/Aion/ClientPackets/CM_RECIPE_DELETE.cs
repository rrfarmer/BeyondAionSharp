using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_RECIPE_DELETE (Rolandas). Deletes a learned recipe. RecipeList red-tolerated.</summary>
public class CM_RECIPE_DELETE : AionClientPacket
{
    int recipeId;

    public CM_RECIPE_DELETE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        recipeId = ReadD();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        player.GetRecipeList().DeleteRecipe(player, recipeId);
    }
}
