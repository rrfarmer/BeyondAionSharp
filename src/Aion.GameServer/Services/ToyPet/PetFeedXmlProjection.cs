using System.Xml.Linq;

namespace Aion.GameServer.Services.ToyPet;

public sealed record PetFeedFlavourProjection(
	int Id,
	int FullCount,
	int LovedFoodLimit,
	int CooldownSeconds,
	IReadOnlyList<PetFeedRewardGroup> RewardGroups);

public static class PetFeedXmlProjection
{
	public static IReadOnlyDictionary<int, PetFeedFlavourProjection> Parse(string xml)
	{
		ArgumentNullException.ThrowIfNull(xml);

		var document = XDocument.Parse(xml);
		var flavours = new Dictionary<int, PetFeedFlavourProjection>();
		foreach (var flavourElement in document.Root?.Elements("flavour") ?? [])
		{
			var flavour = ParseFlavour(flavourElement);
			// Java parity: dataholders/PetFeedData.afterUnmarshal uses Map.put, so later duplicate ids replace earlier ones.
			flavours[flavour.Id] = flavour;
		}

		return flavours;
	}

	public static IReadOnlyList<int> GetSortedFullCounts(IReadOnlyDictionary<int, PetFeedFlavourProjection> flavours)
	{
		ArgumentNullException.ThrowIfNull(flavours);

		// Java parity: PetFeedCalculator static initialization uses TreeSet<Short> and ignores non-positive full counts.
		return flavours.Values
			.Select(flavour => flavour.FullCount)
			.Where(fullCount => fullCount > 0)
			.Distinct()
			.Order()
			.ToArray();
	}

	private static PetFeedFlavourProjection ParseFlavour(XElement flavourElement)
	{
		var rewardGroups = flavourElement
			.Elements("food")
			.Select(ParseRewardGroup)
			.ToArray();

		return new PetFeedFlavourProjection(
			Id: ReadRequiredInt(flavourElement, "id"),
			FullCount: ReadOptionalInt(flavourElement, "full_count", defaultValue: 1),
			LovedFoodLimit: ReadOptionalInt(flavourElement, "loved_limit", defaultValue: 0),
			CooldownSeconds: ReadRequiredInt(flavourElement, "cd"),
			RewardGroups: rewardGroups);
	}

	private static PetFeedRewardGroup ParseRewardGroup(XElement foodElement)
	{
		return new PetFeedRewardGroup(
			Type: ParseFoodType(ReadRequiredString(foodElement, "group")),
			IsLoved: ReadOptionalBool(foodElement, "loved", defaultValue: false),
			Results: foodElement
				.Elements("result")
				.Select(resultElement => new PetFeedReward(ReadRequiredInt(resultElement, "item"), ItemLevel: 0))
				.ToArray());
	}

	private static PetFoodType ParseFoodType(string value)
	{
		return value switch
		{
			"AETHER_CHERRY" => PetFoodType.AetherCherry,
			"AETHER_CRYSTAL_BISCUIT" => PetFoodType.AetherCrystalBiscuit,
			"AETHER_GEM_BISCUIT" => PetFoodType.AetherGemBiscuit,
			"AETHER_POWDER_BISCUIT" => PetFoodType.AetherPowderBiscuit,
			"ARMOR" => PetFoodType.Armor,
			"BALAUR_SCALES" => PetFoodType.BalaurScales,
			"BONES" => PetFoodType.Bones,
			"EXCLUDES" => PetFoodType.Excludes,
			"FLUIDS" => PetFoodType.Fluids,
			"HEALTHY_FOOD_ALL" => PetFoodType.HealthyFoodAll,
			"HEALTHY_FOOD_SPICY" => PetFoodType.HealthyFoodSpicy,
			"MISCELLANEOUS" => PetFoodType.Miscellaneous,
			"POPPY_SNACK" => PetFoodType.PoppySnack,
			"POPPY_SNACK_TASTY" => PetFoodType.PoppySnackTasty,
			"POPPY_SNACK_NUTRITIOUS" => PetFoodType.PoppySnackNutritious,
			"SOULS" => PetFoodType.Souls,
			"SHUGO_EVENT_COIN" => PetFoodType.ShugoEventCoin,
			"STINKY" => PetFoodType.Stinky,
			"THORNS" => PetFoodType.Thorns,
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown Java pet FoodType value."),
		};
	}

	private static string ReadRequiredString(XElement element, string attributeName)
	{
		return (string?)element.Attribute(attributeName)
			?? throw new FormatException($"Missing required Java pet feed attribute '{attributeName}'.");
	}

	private static int ReadRequiredInt(XElement element, string attributeName)
	{
		return int.Parse(ReadRequiredString(element, attributeName), System.Globalization.CultureInfo.InvariantCulture);
	}

	private static int ReadOptionalInt(XElement element, string attributeName, int defaultValue)
	{
		var value = (string?)element.Attribute(attributeName);
		return value is null ? defaultValue : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
	}

	private static bool ReadOptionalBool(XElement element, string attributeName, bool defaultValue)
	{
		var value = (string?)element.Attribute(attributeName);
		return value is null ? defaultValue : bool.Parse(value);
	}
}
