using System.Collections.ObjectModel;

namespace Aion.GameServer.Dataholders;

public sealed class ItemTemplateTable
{
	private readonly IReadOnlyDictionary<int, ItemTemplateSummary> _templatesById;
	private readonly IReadOnlySet<int> _learnableEmotionIds;

	public ItemTemplateTable(IReadOnlyList<ItemTemplateSummary> templates, IReadOnlySet<int>? learnableEmotionIds = null)
	{
		Templates = templates;
		_templatesById = new ReadOnlyDictionary<int, ItemTemplateSummary>(
			templates.ToDictionary(template => template.TemplateId));
		_learnableEmotionIds = learnableEmotionIds?.ToHashSet() ?? new HashSet<int>();
	}

	public IReadOnlyList<ItemTemplateSummary> Templates { get; }

	public IReadOnlySet<int> LearnableEmotionIds => _learnableEmotionIds;

	public int Count => Templates.Count;

	public ItemTemplateSummary? GetItemTemplate(int itemId)
	{
		return _templatesById.GetValueOrDefault(itemId);
	}

	public bool IsLearnableEmotion(int emotionId)
	{
		// Java parity: model/templates/item/actions/EmotionLearnAction.isLearnable.
		return _learnableEmotionIds.Contains(emotionId);
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
	ItemSkillLearnActionInfo? SkillLearnAction = null,
	int ActivationCount = 0,
	int ExpireTimeMinutes = 0,
	int EnchantType = 0,
	bool CanTune = false,
	int MaxTuneCount = 0,
	int ConditioningMaxLevel = 0,
	string AttackType = "",
	ItemWeaponStats? WeaponStats = null,
	IReadOnlyList<ItemStatModifier>? Modifiers = null,
	int StatBonusSetId = 0,
	string EnchantName = "",
	string TemperingName = "",
	int PolishSetId = 0,
	int ChargeActionMaxLevel = 0,
	ItemGodstoneInfo? GodstoneInfo = null,
	ItemImprovement? Improvement = null,
	int RecommendRank = 0,
	ItemIdianInfo? IdianInfo = null,
	ItemStigmaInfo? StigmaInfo = null,
	IReadOnlyDictionary<string, int>? RequiredLevels = null,
	IReadOnlyDictionary<string, int>? MaxLevelRestrictions = null,
	string GenderPermitted = "",
	int MinRank = 1,
	int MaxRank = 18,
	int MaxEnchantLevel = 0,
	bool CanExceedEnchant = false,
	int ManastoneSlots = 0,
	int SpecialManastoneSlots = 0,
	string ExceedEnchantSkill = "",
	ItemEnchantActionInfo? EnchantAction = null,
	int UseDelayId = 0,
	int UseDelayMillis = 0,
	int RideNpcId = 0,
	int EmotionLearnId = 0,
	int EmotionLearnMinutes = 0,
	bool HasEmotionLearnAction = false,
	int TitleAddTitleId = 0,
	int TitleAddMinutes = 0,
	bool HasTitleAddAction = false,
	bool HasTitleAddMinutes = false,
	ItemExpandInventoryActionInfo? ExpandInventoryAction = null,
	ItemDyeActionInfo? DyeAction = null,
	ItemAnimationActionInfo? AnimationAction = null,
	ItemRemodelActionInfo? RemodelAction = null,
	string CosmeticActionName = "",
	bool HasDecomposeAction = false,
	int ExtraInventoryId = -1,
	int AssemblyItemId = 0,
	ItemExpExtractActionInfo? ExpExtractAction = null)
{
	private const int CanPolishMask = 1 << 17;
	private const int SoulBoundMask = 1 << 7;
	private const int NoEnchantMask = 1 << 9;
	private const int CanProcEnchantMask = 1 << 10;
	private const int DyeableMask = 1 << 15;

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

	private static readonly IReadOnlyDictionary<string, int[]> RequiredSkillsByGroup = new Dictionary<string, int[]>(StringComparer.Ordinal)
	{
		["SWORD"] = [37, 44],
		["GREATSWORD"] = [51],
		["DAGGER"] = [66, 45],
		["MACE"] = [39, 46],
		["ORB"] = [111],
		["SPELLBOOK"] = [100],
		["POLEARM"] = [52],
		["STAFF"] = [89],
		["BOW"] = [53],
		["HARP"] = [124, 114],
		["GUN"] = [117, 112],
		["CANNON"] = [113],
		["KEYBLADE"] = [112, 115],
		["SHIELD"] = [43, 50],
		["TORSO"] = [103, 106],
		["GLOVE"] = [103, 106],
		["SHOULDER"] = [103, 106],
		["PANTS"] = [103, 106],
		["SHOES"] = [103, 106],
		["RB_TORSO"] = [103, 106],
		["RB_GLOVE"] = [103, 106],
		["RB_SHOULDER"] = [103, 106],
		["RB_PANTS"] = [103, 106],
		["RB_SHOES"] = [103, 106],
		["CL_TORSO"] = [40],
		["CL_GLOVE"] = [40],
		["CL_SHOULDER"] = [40],
		["CL_PANTS"] = [40],
		["CL_SHOES"] = [40],
		["LT_TORSO"] = [41, 48],
		["LT_GLOVE"] = [41, 48],
		["LT_SHOULDER"] = [41, 48],
		["LT_PANTS"] = [41, 48],
		["LT_SHOES"] = [41, 48],
		["CH_TORSO"] = [42, 49],
		["CH_GLOVE"] = [42, 49],
		["CH_SHOULDER"] = [42, 49],
		["CH_PANTS"] = [42, 49],
		["CH_SHOES"] = [42, 49],
		["PL_TORSO"] = [54],
		["PL_GLOVE"] = [54],
		["PL_SHOULDER"] = [54],
		["PL_PANTS"] = [54],
		["PL_SHOES"] = [54],
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

	public bool IsStigma => StigmaInfo != null || string.Equals(ItemGroup, "STIGMA", StringComparison.Ordinal);

	public bool IsStigmaShard => string.Equals(ItemGroup, "STIGMA_SHARD", StringComparison.Ordinal);

	public bool IsTwoHandWeapon => TwoHandWeaponGroups.Contains(ItemGroup);

	public bool IsTradeable => (Mask & (1 << 1)) == (1 << 1);

	public bool IsRemodelable => (Mask & (1 << 12)) == (1 << 12);

	public bool IsSoulBound => (Mask & SoulBoundMask) == SoulBoundMask;

	public bool IsNoEnchant => (Mask & NoEnchantMask) == NoEnchantMask;

	public bool CanPolish => (Mask & CanPolishMask) == CanPolishMask;

	public bool CanSocketGodstone => (Mask & CanProcEnchantMask) == CanProcEnchantMask;

	public bool IsItemDyePermitted => (Mask & DyeableMask) == DyeableMask;

	public bool IsCloth => IsArmor && ((!IsAccessory && !string.Equals(ItemGroup, "BELT", StringComparison.Ordinal)) || string.Equals(ItemGroup, "HEAD", StringComparison.Ordinal));

	public bool IsMagicalAttackWeapon => string.Equals(AttackType, "MAGICAL", StringComparison.Ordinal);

	public IReadOnlyList<ItemStatModifier> StatModifiers => Modifiers ?? Array.Empty<ItemStatModifier>();

	public IReadOnlyList<int> RequiredEquipSkills
	{
		get
		{
			// Java parity: model/templates/item/enums/ItemGroup.getRequiredSkills.
			return RequiredSkillsByGroup.GetValueOrDefault(ItemGroup) ?? Array.Empty<int>();
		}
	}

	public bool IsClassSpecific(string playerClass)
	{
		// Java parity: model/templates/item/ItemTemplate.isClassSpecific.
		var classRestrictions = ClassRestrictions ?? RequiredLevels?.Keys.ToHashSet(StringComparer.Ordinal);
		if (classRestrictions == null || classRestrictions.Count == 0)
			return false;

		var normalizedClass = playerClass.ToUpperInvariant();
		if (classRestrictions.Contains(normalizedClass))
			return true;

		return StartingClasses.TryGetValue(normalizedClass, out var startingClass)
			&& classRestrictions.Contains(startingClass);
	}

	public int GetRequiredLevel(string playerClass)
	{
		// Java parity: model/templates/item/ItemTemplate.getRequiredLevel.
		if (RequiredLevels == null || RequiredLevels.Count == 0)
			return -1;

		var normalizedClass = playerClass.ToUpperInvariant();
		return RequiredLevels.TryGetValue(normalizedClass, out var requiredLevel) && requiredLevel > 0
			? requiredLevel
			: -1;
	}

	public int GetMaxLevelRestrict(string playerClass)
	{
		// Java parity: model/templates/item/ItemTemplate.getMaxLevelRestrict.
		if (MaxLevelRestrictions == null || MaxLevelRestrictions.Count == 0)
			return 0;

		var normalizedClass = playerClass.ToUpperInvariant();
		return MaxLevelRestrictions.GetValueOrDefault(normalizedClass);
	}

	public bool IsRacePermitted(string playerRace)
	{
		// Java parity: model/gameobjects/player/Equipment.equipItem race guard.
		return string.IsNullOrEmpty(Race)
			|| string.Equals(Race, "PC_ALL", StringComparison.Ordinal)
			|| string.Equals(Race, playerRace, StringComparison.Ordinal);
	}

	public bool IsGenderPermitted(string playerGender)
	{
		// Java parity: model/templates/item/ItemUseLimits.getGenderPermitted.
		return string.IsNullOrEmpty(GenderPermitted)
			|| string.Equals(GenderPermitted, playerGender, StringComparison.Ordinal);
	}

	public bool VerifyRank(int rank)
	{
		// Java parity: model/templates/item/ItemUseLimits.verifyRank.
		return MinRank <= rank && MaxRank >= rank;
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

// Java parity: model/templates/item/actions/IdianAction.
public sealed record ItemIdianInfo(int BurnAttack, int BurnDefend);

// Java parity: model/templates/item/Stigma.
public sealed record ItemStigmaInfo(IReadOnlyList<string> GainSkillGroups, bool Chargeable);

// Java parity: model/templates/item/actions/EnchantItemAction.
public sealed record ItemEnchantActionInfo(int Count, int MinLevel, int MaxLevel, bool ManastoneOnly, float Chance);

// Java parity: model/templates/item/actions/SkillLearnAction.
public sealed record ItemSkillLearnActionInfo(int SkillId, int Level, string PlayerClass);

// Java parity: model/templates/item/actions/ExpandInventoryAction.
public sealed record ItemExpandInventoryActionInfo(int Level, string Storage);

// Java parity: model/templates/item/actions/ExpExtractAction.
public sealed record ItemExpExtractActionInfo(int ItemId, bool IsPercent, long Cost);

// Java parity: model/templates/item/actions/DyeAction.
public sealed record ItemDyeActionInfo(int? Color, int Minutes, bool HasMinutes);

// Java parity: model/templates/item/actions/AnimationAddAction.
public sealed record ItemAnimationActionInfo(
	int? Idle,
	int? Run,
	int? Jump,
	int? Rest,
	int? Shop,
	int Minutes)
{
	public IReadOnlyList<int> MotionIds => new[] { Idle, Run, Jump, Rest, Shop }
		.Where(id => id.HasValue)
		.Select(id => id!.Value)
		.ToArray();
}

// Java parity: model/templates/item/actions/RemodelAction.
public sealed record ItemRemodelActionInfo(int ExtractType, int ExpireMinutes);

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
