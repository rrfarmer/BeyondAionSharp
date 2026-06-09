using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Model.Templates.Rewards;

/// <summary>Java parity: model/templates/rewards/MedicineItem.</summary>
public class MedicineItem : IdLevelReward
{
    public override long GetCount()
    {
        return Rnd.Get(1, 3);
    }
}
