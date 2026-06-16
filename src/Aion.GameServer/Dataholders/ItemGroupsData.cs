using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Itemgroups;
using Aion.GameServer.Model.Templates.Pet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/ItemGroupsData (Rolandas). @XmlRootElement(item_groups); EnumMap→Dictionary; Arrays.asList/singletonList→new List; afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("item_groups")]
public class ItemGroupsData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("craft_materials")] public CraftItemGroup craftMaterials;
    [XmlElement("craft_shop")] public CraftItemGroup craftShop;
    [XmlElement("craft_bundles")] public CraftRecipeGroup craftBundles;
    [XmlElement("craft_recipes")] public CraftRecipeGroup craftRecipes;
    [XmlElement("manastones_common")] public ManastoneGroup manastonesCommon;
    [XmlElement("manastones_rare")] public ManastoneGroup manastonesRare;
    [XmlElement("medals")] public MedalGroup medals;
    [XmlElement("food")] public FoodGroup food;
    [XmlElement("medicine_common")] public MedicineGroup medicineCommon;
    [XmlElement("medicine_rare")] public MedicineGroup medicineRare;
    [XmlElement("medicine_legendary")] public MedicineGroup medicineLegendary;
    [XmlElement("ores_rare")] public OreGroup oresRare;
    [XmlElement("ores_legendary")] public OreGroup oresLegendary;
    [XmlElement("ores_unique")] public OreGroup oresUnique;
    [XmlElement("ores_epic")] public OreGroup oresEpic;
    [XmlElement("gather_rare")] public GatherGroup gatherRare;
    [XmlElement("enchants")] public EnchantGroup enchants;
    [XmlElement("events")] public EventGroup events;
    [XmlElement("boss_rare")] public BossGroup bossRare;
    [XmlElement("boss_legendary")] public BossGroup bossLegendary;
    [XmlElement("feed_fluid")] public FeedGroups.FeedFluidGroup feedFluids;
    [XmlElement("feed_armor")] public FeedGroups.FeedArmorGroup feedArmor;
    [XmlElement("feed_thorn")] public FeedGroups.FeedThornGroup feedThorns;
    [XmlElement("feed_bone")] public FeedGroups.FeedBoneGroup feedBones;
    [XmlElement("feed_balaur_material")] public FeedGroups.FeedBalaurGroup feedBalaurScales;
    [XmlElement("feed_soul")] public FeedGroups.FeedSoulGroup feedSouls;
    [XmlElement("feed_exclude")] public FeedGroups.FeedExcludeGroup feedExcludes;
    [XmlElement("stinking_junk")] public FeedGroups.StinkingJunkGroup stinkingJunk;
    [XmlElement("feed_healthy_all")] public FeedGroups.HealthyFoodAllGroup healthyFoodAll;
    [XmlElement("feed_healthy_spicy")] public FeedGroups.HealthyFoodSpicyGroup healthyFoodSpicy;
    [XmlElement("feed_powder_biscuit")] public FeedGroups.AetherPowderBiscuitGroup aetherPowderBiscuit;
    [XmlElement("feed_crystal_biscuit")] public FeedGroups.AetherCrystalBiscuitGroup aetherCrystalBiscuit;
    [XmlElement("feed_gem_biscuit")] public FeedGroups.AetherGemBiscuitGroup aetherGemBiscuit;
    [XmlElement("poppy_snack")] public FeedGroups.PoppySnackGroup poppySnack;
    [XmlElement("tasty_poppy_snack")] public FeedGroups.PoppySnackTastyGroup poppySnackTasty;
    [XmlElement("nutritious_poppy_snack")] public FeedGroups.PoppySnackNutritiousGroup poppySnackNutritious;
    [XmlElement("feed_shugo_event_coin")] public FeedGroups.ShugoEventCoinGroup shugoCoins;
    [XmlElement("feed_aether_cherry")] public FeedGroups.AetherCherryGroup aetherCherries;

    private List<BonusItemGroup> craftGroups;
    private List<BonusItemGroup> manastoneGroups;
    private List<BonusItemGroup> medalGroups;
    private List<BonusItemGroup> foodGroups;
    private List<BonusItemGroup> medicineGroups;
    private List<BonusItemGroup> gatherGroups;
    private List<BonusItemGroup> enchantGroups;
    private List<BonusItemGroup> eventGroups;
    private List<BonusItemGroup> bossGroups;

    private readonly Dictionary<FoodType, HashSet<int>> petFood = new();

    private int petFoodCount = 0;

    public void AfterUnmarshal(object parent)
    {
        craftGroups = new List<BonusItemGroup> { craftMaterials, craftShop, craftBundles, craftRecipes };
        manastoneGroups = new List<BonusItemGroup> { manastonesCommon, manastonesRare };
        medalGroups = new List<BonusItemGroup> { medals };
        foodGroups = new List<BonusItemGroup> { food };
        medicineGroups = new List<BonusItemGroup> { medicineCommon, medicineRare, medicineLegendary };
        gatherGroups = new List<BonusItemGroup> { gatherRare, oresRare, oresLegendary, oresUnique, oresEpic };
        enchantGroups = new List<BonusItemGroup> { enchants };
        eventGroups = new List<BonusItemGroup> { events };
        bossGroups = new List<BonusItemGroup> { bossRare, bossLegendary };

        foreach (FoodType foodType in System.Enum.GetValues<FoodType>())
        {
            List<ItemRaceEntry> food = GetPetFood(foodType);
            if (food == null)
                continue;
            HashSet<int> itemIds = new HashSet<int>(food.Select(e => e.GetId()));
            petFood[foodType] = itemIds;
            if (foodType != FoodType.EXCLUDES && foodType != FoodType.STINKY)
                petFoodCount += itemIds.Count;
            food.Clear();
        }
    }

    public List<BonusItemGroup> GetCraftGroups()
    {
        return craftGroups;
    }

    public List<BonusItemGroup> GetManastoneGroups()
    {
        return manastoneGroups;
    }

    public List<BonusItemGroup> GetMedalGroups()
    {
        return medalGroups;
    }

    public List<BonusItemGroup> GetFoodGroups()
    {
        return foodGroups;
    }

    public List<BonusItemGroup> GetMedicineGroups()
    {
        return medicineGroups;
    }

    public List<BonusItemGroup> GetGatherGroups()
    {
        return gatherGroups;
    }

    public List<BonusItemGroup> GetEnchantGroups()
    {
        return enchantGroups;
    }

    public List<BonusItemGroup> GetEventGroups()
    {
        return eventGroups;
    }

    public List<BonusItemGroup> GetBossGroups()
    {
        return bossGroups;
    }

    public bool IsFood(int itemId, FoodType foodType)
    {
        HashSet<int> food = petFood[FoodType.EXCLUDES];
        if (food.Contains(itemId))
            return false;
        food = petFood[FoodType.STINKY];
        if (food.Contains(itemId))
            return false;
        if (foodType != FoodType.MISCELLANEOUS)
        {
            food = petFood[foodType];
            return food.Contains(itemId);
        }
        else
        {
            food = petFood[FoodType.ARMOR];
            if (food.Contains(itemId))
                return true;
            food = petFood[FoodType.BALAUR_SCALES];
            if (food.Contains(itemId))
                return true;
            food = petFood[FoodType.BONES];
            if (food.Contains(itemId))
                return true;
            food = petFood[FoodType.FLUIDS];
            if (food.Contains(itemId))
                return true;
            food = petFood[FoodType.SOULS];
            if (food.Contains(itemId))
                return true;
            food = petFood[FoodType.THORNS];
            if (food.Contains(itemId))
                return true;
        }
        return false;
    }

    private List<ItemRaceEntry> GetPetFood(FoodType foodType)
    {
        switch (foodType)
        {
            // Biscuits bought from shop
            case FoodType.AETHER_CRYSTAL_BISCUIT:
                return aetherCrystalBiscuit.GetItems();
            case FoodType.AETHER_GEM_BISCUIT:
                return aetherGemBiscuit.GetItems();
            case FoodType.AETHER_POWDER_BISCUIT:
                return aetherPowderBiscuit.GetItems();
            case FoodType.AETHER_CHERRY:
                return aetherCherries.GetItems();

            // Specific Junk
            case FoodType.ARMOR:
                return feedArmor.GetItems();
            case FoodType.BALAUR_SCALES:
                return feedBalaurScales.GetItems();
            case FoodType.BONES:
                return feedBones.GetItems();
            case FoodType.FLUIDS:
                return feedFluids.GetItems();
            case FoodType.SOULS:
                return feedSouls.GetItems();
            case FoodType.THORNS:
                return feedThorns.GetItems();

            // Healthy Pet Food bought from vendors
            case FoodType.HEALTHY_FOOD_ALL:
                return healthyFoodAll.GetItems();
            case FoodType.HEALTHY_FOOD_SPICY:
                return healthyFoodSpicy.GetItems();

            // Runaway Poppy's Food
            case FoodType.POPPY_SNACK:
                return poppySnack.GetItems();
            case FoodType.POPPY_SNACK_TASTY:
                return poppySnackTasty.GetItems();
            case FoodType.POPPY_SNACK_NUTRITIOUS:
                return poppySnackNutritious.GetItems();

            // Shugo Tomb Event pet food
            case FoodType.SHUGO_EVENT_COIN:
                return shugoCoins.GetItems();

            // Exclusions
            case FoodType.STINKY:
                return stinkingJunk.GetItems();
            case FoodType.EXCLUDES:
                return feedExcludes.GetItems();
            case FoodType.MISCELLANEOUS:
                break;
            default:
                log.LogWarning("Unhandled food type " + foodType);
                break;
        }
        return null;
    }

    public int BonusSize()
    {
        return craftMaterials.GetItems().Count + craftShop.GetItems().Count + craftBundles.GetItems().Count + craftRecipes.GetItems().Count
            + manastonesCommon.GetItems().Count + manastonesRare.GetItems().Count + food.GetItems().Count + medicineCommon.GetItems().Count
            + medicineRare.GetItems().Count + medicineLegendary.GetItems().Count + oresRare.GetItems().Count + oresLegendary.GetItems().Count
            + oresUnique.GetItems().Count + oresEpic.GetItems().Count + gatherRare.GetItems().Count + enchants.GetItems().Count
            + events.GetItems().Count + bossRare.GetItems().Count + bossLegendary.GetItems().Count;
    }

    public int PetFoodSize()
    {
        return petFoodCount;
    }
}
