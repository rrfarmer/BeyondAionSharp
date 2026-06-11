using System.Collections.Generic;

namespace Aion.GameServer.Model.GameObjects.Players;

/// <summary>Java parity: model/gameobjects/player/RecipeList.</summary>
public class RecipeList
{
    private ISet<int> recipeList = new HashSet<int>();

    public RecipeList(HashSet<int> recipeList)
    {
        this.recipeList = recipeList;
    }

    public RecipeList()
    {
    }

    public ISet<int> GetRecipeList()
    {
        return recipeList;
    }

    public bool AddRecipe(Player player, int recipeId)
    {
        if (!IsRecipePresent(recipeId) && Aion.GameServer.Dao.PlayerRecipesDAO.AddRecipe(player.GetObjectId(), recipeId))
        {
            recipeList.Add(recipeId);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmLearnRecipe(recipeId));
            return true;
        }
        return false;
    }

    public bool DeleteRecipe(Player player, int recipeId)
    {
        if (recipeList.Contains(recipeId) && Aion.GameServer.Dao.PlayerRecipesDAO.DelRecipe(player.GetObjectId(), recipeId))
        {
            recipeList.Remove(recipeId);
            Aion.GameServer.Utils.PacketSendUtility.SendPacket(player, new Aion.GameServer.Network.Aion.ServerPackets.SmRecipeDelete(recipeId));
            return true;
        }
        return false;
    }

    public bool IsRecipePresent(int recipeId)
    {
        return recipeList.Contains(recipeId);
    }

    public int Size()
    {
        return this.recipeList.Count;
    }
}
