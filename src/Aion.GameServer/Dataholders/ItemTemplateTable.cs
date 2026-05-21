using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class ItemTemplateTable
{
	private readonly IReadOnlyDictionary<int, ItemTemplateSummary> _templatesById;

	public ItemTemplateTable(IReadOnlyList<ItemTemplateSummary> templates)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, ItemTemplateSummary>(
			templates.ToDictionary(template => template.TemplateId));
	}

	public IReadOnlyList<ItemTemplateSummary> Templates { get; }

	public int Count => Templates.Count;

	public ItemTemplateSummary? GetItemTemplate(int itemId)
	{
		return _templatesById.GetValueOrDefault(itemId);
	}
}

public sealed record ItemTemplateSummary(
	int TemplateId,
	string Name,
	int DescriptionId,
	int Mask,
	int Level,
	string ItemGroup,
	string ItemType,
	string Quality,
	string Race,
	int MaxStackCount,
	long Price,
	long ValidEquipmentSlots,
	int DispositionItemId = 0,
	int DispositionItemCount = 0,
	IReadOnlySet<string>? ClassRestrictions = null,
	int CraftLearnRecipeId = 0,
	int ActivationCount = 0,
	int ExpireTimeMinutes = 0,
	int EnchantType = 0,
	bool CanTune = false,
	int ConditioningMaxLevel = 0,
	string AttackType = "",
	ItemWeaponStats? WeaponStats = null,
	IReadOnlyList<ItemStatModifier>? Modifiers = null,
	int StatBonusSetId = 0,
	string EnchantName = "",
	string TemperingName = "",
	int PolishSetId = 0,
	ItemGodstoneInfo? GodstoneInfo = null,
	ItemImprovement? Improvement = null,
	int RecommendRank = 0)
{
	private const int CanPolishMask = 1 << 17;

	private static readonly HashSet<string> WeaponGroups = new(StringComparer.Ordinal)
	{
		"NOWEAPON", "SWORD", "GREATSWORD", "DAGGER", "MACE", "ORB", "SPELLBOOK", "POLEARM", "STAFF", "BOW",
		"HARP", "GUN", "CANNON", "KEYBLADE", "NPC_MACE", "TOOLRODS", "TOOLHOES", "TOOLPICKS",
	};

	private static readonly HashSet<string> TwoHandWeaponGroups = new(StringComparer.Ordinal)
	{
		"NOWEAPON", "GREATSWORD", "ORB", "SPELLBOOK", "POLEARM", "STAFF", "BOW", "HARP", "CANNON", "KEYBLADE",
		"TOOLRODS", "TOOLPICKS",
	};

	private static readonly HashSet<string> AccessoryGroups = new(StringComparer.Ordinal)
	{
		"EARRING", "RING", "NECKLACE", "BELT", "HEAD", "CL_SHIELD", "POWER_SHARDS",
	};

	private static readonly IReadOnlyDictionary<string, string> StartingClasses = new Dictionary<string, string>(StringComparer.Ordinal)
	{
		["GLADIATOR"] = "WARRIOR",
		["TEMPLAR"] = "WARRIOR",
		["ASSASSIN"] = "SCOUT",
		["RANGER"] = "SCOUT",
		["SORCERER"] = "MAGE",
		["SPIRIT_MASTER"] = "MAGE",
		["CLERIC"] = "PRIEST",
		["CHANTER"] = "PRIEST",
		["RIDER"] = "ENGINEER",
		["GUNNER"] = "ENGINEER",
		["BARD"] = "ARTIST",
	};

	public bool IsEquipment => ValidEquipmentSlots != 0;

	public bool IsWeapon => WeaponGroups.Contains(ItemGroup);

	public bool IsArmor => IsEquipment && !IsWeapon && !IsStigma && !IsPlume;

	public bool IsAccessory => AccessoryGroups.Contains(ItemGroup);

	public bool IsShield => string.Equals(ItemGroup, "SHIELD", StringComparison.Ordinal);

	public bool IsWing => string.Equals(ItemGroup, "WING", StringComparison.Ordinal);

	public bool IsPlume => string.Equals(ItemGroup, "PLUME", StringComparison.Ordinal);

	public bool IsStigma => string.Equals(ItemGroup, "STIGMA", StringComparison.Ordinal);

	public bool IsStigmaShard => string.Equals(ItemGroup, "STIGMA_SHARD", StringComparison.Ordinal);

	public bool IsTwoHandWeapon => TwoHandWeaponGroups.Contains(ItemGroup);

	public bool IsTradeable => (Mask & (1 << 1)) == (1 << 1);

	public bool CanPolish => (Mask & CanPolishMask) == CanPolishMask;

	public bool IsCloth => IsArmor && ((!IsAccessory && !string.Equals(ItemGroup, "BELT", StringComparison.Ordinal)) || string.Equals(ItemGroup, "HEAD", StringComparison.Ordinal));

	public bool IsMagicalAttackWeapon => string.Equals(AttackType, "MAGICAL", StringComparison.Ordinal);

	public IReadOnlyList<ItemStatModifier> StatModifiers => Modifiers ?? Array.Empty<ItemStatModifier>();

	public bool IsClassSpecific(string playerClass)
	{
		// Java parity: model/templates/item/ItemTemplate.isClassSpecific.
		if (ClassRestrictions == null || ClassRestrictions.Count == 0)
			return false;

		var normalizedClass = playerClass.ToUpperInvariant();
		if (ClassRestrictions.Contains(normalizedClass))
			return true;

		return StartingClasses.TryGetValue(normalizedClass, out var startingClass)
			&& ClassRestrictions.Contains(startingClass);
	}

	public string? GetClientName()
	{
		// Java parity: model/templates/L10n.getL10n -> utils/ChatUtil.l10n.
		if (DescriptionId == 0)
			return null;

		var l10nId = (DescriptionId << 1) | 1;
		return string.Concat("$", (char)(l10nId & 0xffff), (char)((l10nId >>> 16) & 0xffff));
	}
}

public sealed record ItemWeaponStats(
	int MinDamage,
	int MaxDamage,
	int AttackSpeed,
	int PhysicalCritical,
	int PhysicalAccuracy,
	int Parry,
	int MagicalAccuracy,
	int MagicalBoost,
	int AttackRange,
	int HitCount,
	int ReduceMax)
{
	public int MeanDamage => (int)((MinDamage + MaxDamage) / 2f);
}

// Java parity: model/templates/item/GodstoneInfo.
public sealed record ItemGodstoneInfo(
	int SkillId,
	int SkillLevel,
	int Probability,
	int ProbabilityLeft,
	int BreakProbability,
	int NonBreakCount);

// Java parity: model/templates/item/Improvement.
public sealed record ItemImprovement(
	int ChargeWay,
	int Level,
	int BurnAttack,
	int BurnDefend,
	int Price1,
	int Price2);

public sealed record ItemStatModifier(
	string Operation,
	string Name,
	int Value,
	bool Bonus,
	int ChargeCondition = 0)
{
	public int Priority => Operation switch
	{
		"rate" => Bonus ? 50 : 20,
		"set" or "abs" => Bonus ? 70 : 40,
		_ => Bonus ? 60 : 30,
	};
}
