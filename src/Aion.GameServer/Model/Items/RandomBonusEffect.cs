using System.Collections.Generic;
using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Model.Items;

/// <summary>Java parity: model/items/RandomBonusEffect implements StatOwner.</summary>
public class RandomBonusEffect : Aion.GameServer.Model.Stats.Calc.IStatOwner
{
    private readonly int statBonusId;
    private readonly List<Aion.GameServer.Model.Stats.Calc.Functions.StatFunction> stats;

    public RandomBonusEffect(Aion.GameServer.Model.Templates.Item.Bonuses.StatBonusType type, int statBonusSetId, int statBonusId)
    {
        this.statBonusId = statBonusId;
        this.stats = DataManager.ITEM_RANDOM_BONUSES.GetTemplate(type, statBonusSetId, statBonusId).GetModifiers();
    }

    public int GetStatBonusId()
    {
        return statBonusId;
    }

    public void ApplyEffect(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        player.GetGameStats().AddEffect(this, stats);
    }

    public void EndEffect(Aion.GameServer.Model.GameObjects.Player.Player player)
    {
        player.GetGameStats().EndEffect(this);
    }
}
