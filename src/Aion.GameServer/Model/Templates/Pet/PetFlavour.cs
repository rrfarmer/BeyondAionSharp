using System.Collections.Generic;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Services.ToyPet;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/PetFlavour (Rolandas).</summary>
[XmlType("PetFlavour")]
public class PetFlavour
{
    [XmlElement("food")] protected List<PetRewards> food;

    [XmlAttribute("id")] protected int id;
    [XmlAttribute("full_count")] protected int fullCount = 1;
    [XmlAttribute("loved_limit")] protected int lovedFoodLimit = 0;
    [XmlAttribute("cd")] protected int cooldown = 0;

    public List<PetRewards> GetFood()
    {
        if (food == null)
        {
            food = new List<PetRewards>();
        }
        return this.food;
    }

    /// <summary>Returns a food group for the itemId. Null if doesn't match.</summary>
    public FoodType? GetFoodType(int itemId)
    {
        foreach (PetRewards rewards in GetFood())
        {
            if (DataManager.ITEM_GROUPS_DATA.IsFood(itemId, rewards.GetType_()))
                return rewards.GetType_();
        }
        return null;
    }

    /// <summary>Returns reward details if earned, otherwise null. Updates progress automatically.</summary>
    public PetFeedResult ProcessFeedResult(PetFeedProgress progress, FoodType foodType, int itemLevel, int playerLevel)
    {
        PetRewards rewardGroup = null;
        foreach (PetRewards rewards in GetFood())
        {
            if (rewards.GetType_() == foodType)
            {
                rewardGroup = rewards;
                break;
            }
        }
        if (rewardGroup == null)
            return null;

        int maxFeedCount = 1;
        if (rewardGroup.IsLoved())
        {
            progress.SetIsLovedFeeded();
        }
        else
        {
            maxFeedCount = fullCount;
        }

        PetFeedCalculator.UpdatePetFeedProgress(progress, itemLevel, maxFeedCount);
        if (progress.GetHungryLevel() != PetHungryLevel.FULL)
            return null;

        return PetFeedCalculator.GetReward(maxFeedCount, rewardGroup, progress, playerLevel);
    }

    public bool IsLovedFood(FoodType foodType, int itemId)
    {
        PetRewards rewardGroup = null;
        foreach (PetRewards rewards in GetFood())
        {
            if (rewards.GetType_() == foodType)
            {
                rewardGroup = rewards;
                break;
            }
        }
        if (rewardGroup == null)
            return false;
        return rewardGroup.IsLoved();
    }

    public int GetId()
    {
        return id;
    }

    public int GetFullCount()
    {
        return fullCount;
    }

    public int GetLovedFoodLimit()
    {
        return lovedFoodLimit;
    }

    public int GetCooldDown()
    {
        return cooldown;
    }
}
