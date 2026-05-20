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
	long ValidEquipmentSlots)
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

	public bool CanPolish => (Mask & CanPolishMask) == CanPolishMask;

	public bool IsCloth => IsArmor && ((!IsAccessory && !string.Equals(ItemGroup, "BELT", StringComparison.Ordinal)) || string.Equals(ItemGroup, "HEAD", StringComparison.Ordinal));

	public string? GetClientName()
	{
		// Java parity: model/templates/L10n.getL10n -> utils/ChatUtil.l10n.
		if (DescriptionId == 0)
			return null;

		var l10nId = (DescriptionId << 1) | 1;
		return string.Concat("$", (char)(l10nId & 0xffff), (char)((l10nId >>> 16) & 0xffff));
	}
}
