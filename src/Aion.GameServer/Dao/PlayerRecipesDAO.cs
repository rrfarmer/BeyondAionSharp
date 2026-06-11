using System.Collections.Generic;
using MySqlConnector;
using Aion.Commons.Database;
using Aion.GameServer.Model.GameObjects.Player;

namespace Aion.GameServer.Dao;

/// <summary>
/// Java parity: dao/PlayerRecipesDAO (@author lord_rex). JDBC DAO over player_recipes via the commons DB callback helper.
/// Anonymous ParamReadStH/IUStH -> private nested classes capturing locals via ctor. setInt->Parameters.Add; execute()->ExecuteNonQuery;
/// rs.next()/getInt->Read()/GetInt32(GetOrdinal). RecipeList(HashSet&lt;int&gt;). SQL verbatim.
/// </summary>
public class PlayerRecipesDAO
{
    private const string SELECT_QUERY = "SELECT `recipe_id` FROM player_recipes WHERE `player_id`=?";
    private const string ADD_QUERY = "INSERT INTO player_recipes (`player_id`, `recipe_id`) VALUES (?, ?)";
    private const string DELETE_QUERY = "DELETE FROM player_recipes WHERE `player_id`=? AND `recipe_id`=?";

    public static RecipeList Load(int playerId)
    {
        HashSet<int> recipeList = new HashSet<int>();
        DB.Select(SELECT_QUERY, new LoadHandler(playerId, recipeList));
        return new RecipeList(recipeList);
    }

    private sealed class LoadHandler : ParamReadStH
    {
        private readonly int playerId;
        private readonly HashSet<int> recipeList;

        internal LoadHandler(int playerId, HashSet<int> recipeList)
        {
            this.playerId = playerId;
            this.recipeList = recipeList;
        }

        public void SetParams(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = playerId });
        }

        public void HandleRead(MySqlDataReader rs)
        {
            while (rs.Read())
            {
                recipeList.Add(rs.GetInt32(rs.GetOrdinal("recipe_id")));
            }
        }
    }

    public static bool AddRecipe(int playerId, int recipeId)
    {
        return DB.InsertUpdate(ADD_QUERY, new AddRecipeHandler(playerId, recipeId));
    }

    private sealed class AddRecipeHandler : IUStH
    {
        private readonly int playerId;
        private readonly int recipeId;

        internal AddRecipeHandler(int playerId, int recipeId)
        {
            this.playerId = playerId;
            this.recipeId = recipeId;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = playerId });
            ps.Parameters.Add(new MySqlParameter { Value = recipeId });
            ps.ExecuteNonQuery();
        }
    }

    public static bool DelRecipe(int playerId, int recipeId)
    {
        return DB.InsertUpdate(DELETE_QUERY, new DelRecipeHandler(playerId, recipeId));
    }

    private sealed class DelRecipeHandler : IUStH
    {
        private readonly int playerId;
        private readonly int recipeId;

        internal DelRecipeHandler(int playerId, int recipeId)
        {
            this.playerId = playerId;
            this.recipeId = recipeId;
        }

        public void HandleInsertUpdate(MySqlCommand ps)
        {
            ps.Parameters.Add(new MySqlParameter { Value = playerId });
            ps.Parameters.Add(new MySqlParameter { Value = recipeId });
            ps.ExecuteNonQuery();
        }
    }
}
