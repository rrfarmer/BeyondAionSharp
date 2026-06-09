using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/FeedGroups (Rolandas) — holder of pet-feed group subtypes, each extends FeedItemGroup.</summary>
public static class FeedGroups
{
    [XmlType("FeedFluidGroup")]
    public class FeedFluidGroup : FeedItemGroup
    {
    }

    [XmlType("FeedArmorGroup")]
    public class FeedArmorGroup : FeedItemGroup
    {
    }

    [XmlType("FeedThornGroup")]
    public class FeedThornGroup : FeedItemGroup
    {
    }

    [XmlType("FeedBalaurGroup")]
    public class FeedBalaurGroup : FeedItemGroup
    {
    }

    [XmlType("FeedBoneGroup")]
    public class FeedBoneGroup : FeedItemGroup
    {
    }

    [XmlType("FeedSoulGroup")]
    public class FeedSoulGroup : FeedItemGroup
    {
    }

    [XmlType("FeedExcludeGroup")]
    public class FeedExcludeGroup : FeedItemGroup
    {
    }

    [XmlType("StinkingJunkGroup")]
    public class StinkingJunkGroup : FeedItemGroup
    {
    }

    [XmlType("HealthyFoodAllGroup")]
    public class HealthyFoodAllGroup : FeedItemGroup
    {
    }

    [XmlType("HealthyFoodSpicyGroup")]
    public class HealthyFoodSpicyGroup : FeedItemGroup
    {
    }

    [XmlType("AetherPowderBiscuitGroup")]
    public class AetherPowderBiscuitGroup : FeedItemGroup
    {
    }

    [XmlType("AetherCrystalBiscuitGroup")]
    public class AetherCrystalBiscuitGroup : FeedItemGroup
    {
    }

    [XmlType("AetherGemBiscuitGroup")]
    public class AetherGemBiscuitGroup : FeedItemGroup
    {
    }

    [XmlType("PoppySnackGroup")]
    public class PoppySnackGroup : FeedItemGroup
    {
    }

    [XmlType("PoppySnackTastyGroup")]
    public class PoppySnackTastyGroup : FeedItemGroup
    {
    }

    [XmlType("PoppySnackNutritiousGroup")]
    public class PoppySnackNutritiousGroup : FeedItemGroup
    {
    }

    [XmlType("ShugoEventCoinGroup")]
    public class ShugoEventCoinGroup : FeedItemGroup
    {
    }

    [XmlType("AetherCherryGroup")]
    public class AetherCherryGroup : FeedItemGroup
    {
    }
}
