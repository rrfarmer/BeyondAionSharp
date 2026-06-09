using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/FoodItem.</summary>
public class FoodItem : IdLevelReward
{
    public override long GetCount()
    {
        return Rnd.NextBoolean() ? 5 : 10;
    }
}
