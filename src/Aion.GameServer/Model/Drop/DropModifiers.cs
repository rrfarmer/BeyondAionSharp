using Aion.GameServer.Model;

namespace Aion.GameServer.Model.Drop;

/// <summary>Java parity: model/drop/DropModifiers. Nullable Race (value-type enum) → Race?; Float/Integer → float?/int?.</summary>
public class DropModifiers
{
    private bool isDropNpcChest;
    private Race? dropRace;
    private float boostDropRate;
    private float? reductionDropRate;
    private int? maxDropsPerGroup;

    public bool IsDropNpcChest()
    {
        return isDropNpcChest;
    }

    public void SetIsDropNpcChest(bool dropNpcChest)
    {
        isDropNpcChest = dropNpcChest;
    }

    public Race? GetDropRace()
    {
        return dropRace;
    }

    public void SetDropRace(Race? dropRace)
    {
        this.dropRace = dropRace;
    }

    public float GetBoostDropRate()
    {
        return boostDropRate;
    }

    public void SetBoostDropRate(float boostDropRate)
    {
        this.boostDropRate = boostDropRate;
    }

    public float? GetReductionDropRate()
    {
        return reductionDropRate;
    }

    public void SetReductionDropRate(float? reductionDropRate)
    {
        this.reductionDropRate = reductionDropRate;
    }

    public int? GetMaxDropsPerGroup()
    {
        return maxDropsPerGroup;
    }

    public void SetMaxDropsPerGroup(int? maxDropsPerGroup)
    {
        this.maxDropsPerGroup = maxDropsPerGroup;
    }

    public float CalculateDropChance(float chance, bool allowReductionDropRate)
    {
        if (allowReductionDropRate && reductionDropRate != null)
            chance *= reductionDropRate.Value;
        return chance * boostDropRate;
    }
}
