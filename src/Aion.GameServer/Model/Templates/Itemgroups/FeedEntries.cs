using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Itemgroups;

/// <summary>Java parity: model/templates/itemgroups/FeedEntries (Rolandas). final container of 16 nested XML marker subclasses of ItemRaceEntry. @XmlType(name)→[XmlType(TypeName)]; @XmlAccessorType(FIELD)→no C# equivalent (XmlSerializer default).</summary>
public static class FeedEntries
{
    [XmlType("FeedFluid")]
    public class FeedFluid : ItemRaceEntry { }

    [XmlType("FeedArmor")]
    public class FeedArmor : ItemRaceEntry { }

    [XmlType("FeedThorn")]
    public class FeedThorn : ItemRaceEntry { }

    [XmlType("FeedBalaur")]
    public class FeedBalaur : ItemRaceEntry { }

    [XmlType("FeedBone")]
    public class FeedBone : ItemRaceEntry { }

    [XmlType("FeedSoul")]
    public class FeedSoul : ItemRaceEntry { }

    [XmlType("FeedExclude")]
    public class FeedExclude : ItemRaceEntry { }

    [XmlType("StinkingJunk")]
    public class StinkingJunk : ItemRaceEntry { }

    [XmlType("HealthyFoodAll")]
    public class HealthyFoodAll : ItemRaceEntry { }

    [XmlType("HealthyFoodSpicy")]
    public class HealthyFoodSpicy : ItemRaceEntry { }

    [XmlType("AetherPowderBiscuit")]
    public class AetherPowderBiscuit : ItemRaceEntry { }

    [XmlType("AetherCrystalBiscuit")]
    public class AetherCrystalBiscuit : ItemRaceEntry { }

    [XmlType("AetherGemBiscuit")]
    public class AetherGemBiscuit : ItemRaceEntry { }

    [XmlType("PoppySnack")]
    public class PoppySnack : ItemRaceEntry { }

    [XmlType("PoppySnackTasty")]
    public class PoppySnackTasty : ItemRaceEntry { }

    [XmlType("PoppySnackNutritious")]
    public class PoppySnackNutritious : ItemRaceEntry { }
}
