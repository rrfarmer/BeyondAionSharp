using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Model.Templates.Item.Bonuses;
using Aion.GameServer.Model.Templates.Stats;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ItemRandomBonusData (Rolandas). @XmlRootElement(random_bonuses); EnumMap→Dictionary; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("random_bonuses")]
public class ItemRandomBonusData
{
    [XmlElement("random_bonus")] private List<RandomBonusSet> randomBonusSets;

    [XmlIgnore] private readonly Dictionary<StatBonusType, Dictionary<int, RandomBonusSet>> bonusData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (StatBonusType statBonusType in Enum.GetValues<StatBonusType>())
            bonusData[statBonusType] = new Dictionary<int, RandomBonusSet>();
        foreach (RandomBonusSet bonus in randomBonusSets)
        {
            bonusData[bonus.GetBonusType()][bonus.GetId()] = bonus;
        }
        randomBonusSets = null;
    }

    /// <summary>
    /// NCSoft implemented different stat bonus sets with identical content (one for base item, one for the purified version).
    /// To ensure purification doesn't trigger a re-roll of random bonus stats, we need this method.
    /// </summary>
    public bool AreBonusSetsEqual(StatBonusType statBonusType, int statBonusSetId1, int statBonusSetId2)
    {
        if (statBonusSetId1 == statBonusSetId2)
            return true;
        RandomBonusSet bonusSet1 = GetBonusSet(statBonusType, statBonusSetId1);
        RandomBonusSet bonusSet2 = GetBonusSet(statBonusType, statBonusSetId2);
        if (bonusSet1 == null || bonusSet2 == null)
            return bonusSet1 == bonusSet2;
        // if size comparison isn't sufficient we should override ModifiersTemplate.Equals() and check if both lists are equal
        return bonusSet1.GetModifiers().Count == bonusSet2.GetModifiers().Count;
    }

    private RandomBonusSet GetBonusSet(StatBonusType statBonusType, int statBonusSetId)
    {
        return bonusData[statBonusType].TryGetValue(statBonusSetId, out var v) ? v : null;
    }

    public int SelectRandomBonusNumber(StatBonusType statBonusType, int statBonusSetId)
    {
        RandomBonusSet bonus = GetBonusSet(statBonusType, statBonusSetId);
        if (bonus == null)
            return 0;

        List<ModifiersTemplate> modifiersGroup = bonus.GetModifiers();
        float chance = Rnd.NextFloat(CalculateSumOfChances(modifiersGroup));
        float chanceSum = 0;
        for (int i = 0; i < modifiersGroup.Count; i++)
        {
            chanceSum += modifiersGroup[i].GetChance();
            if (chanceSum >= chance)
                return i + 1;
        }
        return 0;
    }

    private float CalculateSumOfChances(List<ModifiersTemplate> modifiersGroup)
    {
        float sumOfAllChances = 0;
        foreach (ModifiersTemplate modifiersTemplate in modifiersGroup)
            sumOfAllChances += modifiersTemplate.GetChance();
        return sumOfAllChances;
    }

    public ModifiersTemplate GetTemplate(StatBonusType statBonusType, int statBonusSetId, int statBonusId)
    {
        RandomBonusSet bonus = GetBonusSet(statBonusType, statBonusSetId);
        return bonus == null ? null : bonus.GetModifiers()[statBonusId - 1];
    }

    public int Size()
    {
        return bonusData.Values.Sum(m => m.Count);
    }
}
