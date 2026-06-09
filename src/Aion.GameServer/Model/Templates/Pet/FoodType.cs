using System;
using System.Xml.Serialization;

namespace Aion.GameServer.Model.Templates.Pet;

/// <summary>Java parity: model/templates/pet/FoodType (Rolandas).</summary>
[XmlType("FoodType")]
public enum FoodType
{
    AETHER_CHERRY,
    AETHER_CRYSTAL_BISCUIT,
    AETHER_GEM_BISCUIT,
    AETHER_POWDER_BISCUIT,
    ARMOR,
    BALAUR_SCALES,
    BONES,
    EXCLUDES, // Excluded items
    FLUIDS,
    HEALTHY_FOOD_ALL,
    HEALTHY_FOOD_SPICY,
    MISCELLANEOUS,
    POPPY_SNACK,
    POPPY_SNACK_TASTY,
    POPPY_SNACK_NUTRITIOUS,
    SOULS,
    SHUGO_EVENT_COIN,
    STINKY, // Other excuded items
    THORNS
}

public static class FoodTypeExtensions
{
    public static string Value(this FoodType v) => v.ToString();

    public static FoodType FromValue(string value) => (FoodType) Enum.Parse(typeof(FoodType), value);
}
