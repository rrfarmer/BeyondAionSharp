using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using Aion.GameServer.Model.Templates.Pet;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ToyPet;

namespace Aion.GameServer.Dataholders;

public sealed partial class StaticData
{
	private sealed class PetTemplateBuilder
	{
		private readonly List<PetFunctionSummary> _functions = [];

		public PetTemplateBuilder(int id, string name, int nameId, int conditionReward)
		{
			Id = id;
			Name = name;
			NameId = nameId;
			ConditionReward = conditionReward;
		}

		private int Id { get; }

		private string Name { get; }

		private int NameId { get; }

		private int ConditionReward { get; }

		public void AddFunction(PetFunctionSummary function)
		{
			_functions.Add(function);
		}

		public PetTemplateSummary ToSummary()
		{
			return new PetTemplateSummary(
				Id,
				Name,
				NameId,
				ConditionReward,
				_functions.AsReadOnly());
		}
	}

	private sealed class TradeListTemplateBuilder
	{
		private readonly List<int> _goodsListIds = [];

		public TradeListTemplateBuilder(
			int npcId,
			string npcType,
			int sellPriceRate,
			int sellPriceRate2,
			int apSellPriceRate2,
			int buyPriceRate,
			int saveCount)
		{
			NpcId = npcId;
			NpcType = npcType;
			SellPriceRate = sellPriceRate;
			SellPriceRate2 = sellPriceRate2;
			ApSellPriceRate2 = apSellPriceRate2;
			BuyPriceRate = buyPriceRate;
			SaveCount = saveCount;
		}

		private int NpcId { get; }

		private string NpcType { get; }

		private int SellPriceRate { get; }

		private int SellPriceRate2 { get; }

		private int ApSellPriceRate2 { get; }

		private int BuyPriceRate { get; }

		private int SaveCount { get; }

		public void AddGoodsListId(int id)
		{
			_goodsListIds.Add(id);
		}

		public TradeListTemplateSummary ToSummary()
		{
			return new TradeListTemplateSummary(
				NpcId,
				_goodsListIds.AsReadOnly(),
				NpcType,
				SellPriceRate,
				SellPriceRate2,
				ApSellPriceRate2,
				BuyPriceRate,
				SaveCount);
		}
	}

	private sealed class GoodsListBuilder
	{
		private readonly List<GoodsListItemSummary> _items = [];

		public GoodsListBuilder(int id, int legionLevel)
		{
			Id = id;
			LegionLevel = legionLevel;
		}

		private int Id { get; }

		private int LegionLevel { get; }

		public string? SalesTime { get; set; }

		public void AddItem(GoodsListItemSummary item)
		{
			_items.Add(item);
		}

		public GoodsListSummary ToSummary()
		{
			return new GoodsListSummary(
				Id,
				LegionLevel,
				SalesTime,
				_items.AsReadOnly());
		}
	}

	private sealed class FlightZoneBuilder
	{
		private readonly List<ZonePoint2D> _points = [];
		private float _bottom;
		private float _top;

		private FlightZoneBuilder(int mapId, string name, FlightZoneType zoneType, int flags)
		{
			MapId = mapId;
			Name = name;
			ZoneType = zoneType;
			Flags = flags;
		}

		private int MapId { get; }

		private string Name { get; }

		private FlightZoneType ZoneType { get; }

		private int Flags { get; }

		public bool HasEnoughPoints => _points.Count >= 3;

		public static FlightZoneBuilder? TryCreate(XmlReader reader)
		{
			// Java parity: model/templates/zone/ZoneTemplate restricted to ZoneClassName.FLY/NO_FLY polygon areas for this Phase 6 slice.
			if (!TryReadZoneType(reader.GetAttribute("zone_type"), out var zoneType))
				return null;

			var areaType = reader.GetAttribute("area_type") ?? "POLYGON";
			if (!string.Equals(areaType, "POLYGON", StringComparison.Ordinal))
				return null;

			if (!int.TryParse(reader.GetAttribute("mapid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId))
				return null;

			return new FlightZoneBuilder(
				mapId,
				reader.GetAttribute("name") ?? string.Empty,
				zoneType,
				ReadOptionalIntAttribute(reader, "flags", -1));
		}

		public void SetVerticalBounds(float bottom, float top)
		{
			_bottom = bottom;
			_top = top;
		}

		public void AddPoint(float x, float y)
		{
			_points.Add(new ZonePoint2D(x, y));
		}

		public FlightZoneSummary ToSummary()
		{
			return new FlightZoneSummary(MapId, Name, ZoneType, Flags, _bottom, _top, _points.ToArray());
		}

		private static bool TryReadZoneType(string? value, out FlightZoneType zoneType)
		{
			switch (value)
			{
				case "FLY":
					zoneType = FlightZoneType.Fly;
					return true;
				case "NO_FLY":
					zoneType = FlightZoneType.NoFly;
					return true;
				default:
					zoneType = default;
					return false;
			}
		}
	}

	private sealed class CreaturePvpZoneBuilder
	{
		private readonly List<ZonePoint2D> _points = [];
		private float _bottom;
		private float _top;

		private CreaturePvpZoneBuilder(int mapId, string name, CreaturePvpZoneType zoneType, int flags)
		{
			MapId = mapId;
			Name = name;
			ZoneType = zoneType;
			Flags = flags;
		}

		private int MapId { get; }

		private string Name { get; }

		private CreaturePvpZoneType ZoneType { get; }

		private int Flags { get; }

		public bool HasEnoughPoints => _points.Count >= 3;

		public static CreaturePvpZoneBuilder? TryCreate(XmlReader reader)
		{
			// Java parity: ZoneService creates PvPZoneInstance for PVP and SiegeZoneInstance + FortressLocation for FORT.
			if (!TryReadZoneType(reader.GetAttribute("zone_type"), out var zoneType))
				return null;

			var areaType = reader.GetAttribute("area_type") ?? "POLYGON";
			if (!string.Equals(areaType, "POLYGON", StringComparison.Ordinal))
				return null;

			if (!int.TryParse(reader.GetAttribute("mapid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId))
				return null;

			return new CreaturePvpZoneBuilder(
				mapId,
				reader.GetAttribute("name") ?? string.Empty,
				zoneType,
				ReadOptionalIntAttribute(reader, "flags", -1));
		}

		public void SetVerticalBounds(float bottom, float top)
		{
			_bottom = bottom;
			_top = top;
		}

		public void AddPoint(float x, float y)
		{
			_points.Add(new ZonePoint2D(x, y));
		}

		public CreaturePvpZoneSummary ToSummary()
		{
			return new CreaturePvpZoneSummary(MapId, Name, ZoneType, Flags, _bottom, _top, _points.ToArray());
		}

		private static bool TryReadZoneType(string? value, out CreaturePvpZoneType zoneType)
		{
			switch (value)
			{
				case "PVP":
					zoneType = CreaturePvpZoneType.Pvp;
					return true;
				case "FORT":
					zoneType = CreaturePvpZoneType.Siege;
					return true;
				default:
					zoneType = default;
					return false;
			}
		}
	}

	private sealed class HousingBuildingBuilder
	{
		private readonly Dictionary<string, int> _defaultParts = new(StringComparer.OrdinalIgnoreCase);

		public HousingBuildingBuilder(int buildingId, string size, int houseTypeId, string buildingType, string partsMatch)
		{
			BuildingId = buildingId;
			Size = size;
			HouseTypeId = houseTypeId;
			BuildingType = buildingType;
			PartsMatch = partsMatch;
		}

		private int BuildingId { get; }

		private string Size { get; }

		private int HouseTypeId { get; }

		private string BuildingType { get; }

		private string PartsMatch { get; }

		public void SetDefaultPart(string partName, int partId)
		{
			if (partId <= 0)
				return;

			_defaultParts[partName] = partId;
		}

		public HousingBuildingSummary ToSummary()
		{
			// Java parity: model/templates/housing/Building.partsByType consumed by HouseRegistry default decor fallback.
			return new HousingBuildingSummary(
				BuildingId,
				Size,
				HouseTypeId,
				BuildingType,
				BuildDefaultDecorIds(),
				BuildDefaultPartIds(),
				PartsMatch);
		}

		private int[] BuildDefaultPartIds()
		{
			// Java parity: model/templates/housing/Building.getDefaultPartIds returns EnumMap values in PartType order without room repeats.
			return
			[
				.. new[]
				{
					GetPart("roof"),
					GetPart("outwall"),
					GetPart("frame"),
					GetPart("door"),
					GetPart("garden"),
					GetPart("fence"),
					GetPart("inwall"),
					GetPart("infloor"),
				}.Where(partId => partId > 0),
			];
		}

		private int[] BuildDefaultDecorIds()
		{
			return
			[
				GetPart("roof"),
				GetPart("outwall"),
				GetPart("frame"),
				GetPart("door"),
				GetPart("garden"),
				GetPart("fence"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("inwall"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("infloor"),
				GetPart("addon"),
			];
		}

		private int GetPart(string partName)
		{
			return _defaultParts.GetValueOrDefault(partName);
		}
	}

	private static bool IsHousingBuildingPartElement(string elementName)
	{
		// Java parity: model/templates/housing/Building.Parts fields serialized from housing/house_buildings.xml.
		return elementName is "roof" or "outwall" or "frame" or "door" or "garden" or "fence" or "inwall" or "infloor" or "addon";
	}

	private static IReadOnlySet<string> SplitHousePartTags(string? buildingTags)
	{
		// Java parity: model/templates/housing/HousePart.buildingTags JAXB Set<String> from whitespace-separated XML attribute values.
		if (string.IsNullOrWhiteSpace(buildingTags))
			return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		return buildingTags
			.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private static bool IsHousingObjectTemplateElement(string elementName)
	{
		return elementName is "jukebox"
			or "moviejukebox"
			or "picture"
			or "postbox"
			or "chair"
			or "storage"
			or "npc"
			or "move_item"
			or "use_item"
			or "passive"
			or "emblem";
	}

	private static byte GetHousingObjectTypeId(string elementName)
	{
		// Java parity: concrete model/templates/housing PlaceableHouseObject.getTypeId implementations.
		return elementName switch
		{
			"use_item" => 1,
			"storage" => 2,
			"postbox" => 3,
			"chair" => 5,
			"jukebox" => 6,
			"npc" => 7,
			"emblem" => 11,
			_ => 0,
		};
	}

	private sealed class InstanceCooltimeBuilder
	{
		public InstanceCooltimeBuilder(int id, int worldId, string race)
		{
			Id = id;
			WorldId = worldId;
			Race = race;
		}

		private int Id { get; }

		private int WorldId { get; }

		private string Race { get; }

		public int MaxCount { get; set; }

		public int MaxMemberLight { get; set; }

		public int MaxMemberDark { get; set; }

		public int EnterMinLevelLight { get; set; }

		public int EnterMaxLevelLight { get; set; }

		public int EnterMinLevelDark { get; set; }

		public int EnterMaxLevelDark { get; set; }

		public bool CanEnterMentor { get; set; }

		public string CoolTimeType { get; set; } = string.Empty;

		public string TypeValue { get; set; } = string.Empty;

		public int EntCoolTime { get; set; }

		public InstanceCooltimeSummary ToSummary()
		{
			// Java parity: model/templates/InstanceCooltime fields consumed by SM_INSTANCE_INFO and InstanceCooltimeData.getMaxMemberCount.
			return new InstanceCooltimeSummary(
				Id,
				WorldId,
				Race,
				MaxCount,
				MaxMemberLight,
				MaxMemberDark,
				EnterMinLevelLight,
				EnterMaxLevelLight,
				EnterMinLevelDark,
				EnterMaxLevelDark,
				CanEnterMentor,
				CoolTimeType,
				TypeValue,
				EntCoolTime);
		}
	}

	private sealed class ItemRandomBonusBuilder
	{
		private readonly List<IReadOnlyList<ItemStatModifier>> _modifierGroups = [];
		private readonly List<double> _chances = [];
		private List<ItemStatModifier>? _currentModifierGroup;

		public ItemRandomBonusBuilder(string type, int setId)
		{
			Type = type;
			SetId = setId;
		}

		private string Type { get; }

		private int SetId { get; }

		public void AddModifierGroup(double chance)
		{
			_currentModifierGroup = [];
			_modifierGroups.Add(_currentModifierGroup);
			_chances.Add(chance);
		}

		public void AddModifier(ItemStatModifier modifier)
		{
			_currentModifierGroup ??= [];
			if (_modifierGroups.Count == 0)
			{
				_modifierGroups.Add(_currentModifierGroup);
				_chances.Add(0);
			}
			_currentModifierGroup.Add(modifier);
		}

		public ItemRandomBonusSummary ToSummary()
		{
			// Java parity: model/templates/item/bonuses/RandomBonusSet modifier groups are selected by 1-based rnd_bonus rows.
			return new ItemRandomBonusSummary(Type, SetId, _modifierGroups.ToArray(), _chances.ToArray());
		}
	}

	private sealed class NpcSkillListBuilder
	{
		private readonly List<NpcSkillTemplateSummary> _skills = [];

		public NpcSkillListBuilder(IReadOnlyList<int> npcIds)
		{
			NpcIds = npcIds;
		}

		private IReadOnlyList<int> NpcIds { get; }

		public void AddSkill(NpcSkillTemplateSummary skill)
		{
			_skills.Add(skill);
		}

		public NpcSkillListSummary ToSummary()
		{
			return new NpcSkillListSummary(NpcIds, _skills.ToArray());
		}
	}

	private sealed class NpcSkillTemplateBuilder
	{
		public NpcSkillTemplateBuilder(
			int skillId,
			int skillLevel,
			int probability,
			int minHp,
			int maxHp,
			int maxTime,
			int minTime,
			string conjunction,
			int cooldown,
			bool isPostSpawn,
			int priority,
			int nextSkillTime,
			int nextChainId,
			int chainId,
			int maxChainTime,
			string target)
		{
			SkillId = skillId;
			SkillLevel = skillLevel;
			Probability = probability;
			MinHp = minHp;
			MaxHp = maxHp;
			MaxTime = maxTime;
			MinTime = minTime;
			Conjunction = conjunction;
			Cooldown = cooldown;
			IsPostSpawn = isPostSpawn;
			Priority = priority;
			NextSkillTime = nextSkillTime;
			NextChainId = nextChainId;
			ChainId = chainId;
			MaxChainTime = maxChainTime;
			Target = target;
		}

		private int SkillId { get; }
		private int SkillLevel { get; }
		private int Probability { get; }
		private int MinHp { get; }
		private int MaxHp { get; }
		private int MaxTime { get; }
		private int MinTime { get; }
		private string Conjunction { get; }
		private int Cooldown { get; }
		private bool IsPostSpawn { get; }
		private int Priority { get; }
		private int NextSkillTime { get; }
		private int NextChainId { get; }
		private int ChainId { get; }
		private int MaxChainTime { get; }
		private string Target { get; }
		public NpcSkillSpawnSummary? Spawn { get; set; }
		public NpcSkillConditionSummary? Condition { get; set; }

		public NpcSkillTemplateSummary ToSummary()
		{
			return new NpcSkillTemplateSummary(
				SkillId,
				SkillLevel,
				Probability,
				MinHp,
				MaxHp,
				MaxTime,
				MinTime,
				Conjunction,
				Cooldown,
				IsPostSpawn,
				Priority,
				NextSkillTime,
				NextChainId,
				ChainId,
				MaxChainTime,
				Target,
				Spawn,
				Condition);
		}
	}

	private sealed class TitleTemplateBuilder
	{
		private readonly List<ItemStatModifier> _modifiers = [];

		public TitleTemplateBuilder(int titleId, int nameId, string description, string race)
		{
			TitleId = titleId;
			NameId = nameId;
			Description = description;
			Race = race;
		}

		private int TitleId { get; }

		private int NameId { get; }

		private string Description { get; }

		private string Race { get; }

		public void AddModifier(ItemStatModifier modifier)
		{
			_modifiers.Add(modifier);
		}

		public TitleTemplateSummary ToSummary()
		{
			// Java parity: model/templates/TitleTemplate modifiers.
			return new TitleTemplateSummary(
				TitleId,
				NameId,
				Description,
				Race,
				_modifiers.ToArray());
		}
	}

	private sealed class RecipeTemplateBuilder
	{
		private readonly List<int> _comboProducts = [];
		private readonly List<RecipeComponentDataSummary> _componentGroups = [];
		private List<RecipeComponentSummary>? _currentComponents;

		public RecipeTemplateBuilder(
			int recipeId,
			int nameId,
			int skillId,
			string race,
			int skillPoint,
			int dp,
			int autoLearn,
			int productId,
			int quantity,
			int? craftDelayId,
			int? craftDelayTime,
			int? maxProductionCount)
		{
			RecipeId = recipeId;
			NameId = nameId;
			SkillId = skillId;
			Race = race;
			SkillPoint = skillPoint;
			Dp = dp;
			AutoLearn = autoLearn;
			ProductId = productId;
			Quantity = quantity;
			CraftDelayId = craftDelayId;
			CraftDelayTime = craftDelayTime;
			MaxProductionCount = maxProductionCount;
		}

		public int RecipeId { get; }

		public int NameId { get; }

		public int SkillId { get; }

		public string Race { get; }

		public int SkillPoint { get; }

		public int Dp { get; }

		public int AutoLearn { get; }

		public int ProductId { get; }

		public int Quantity { get; }

		public int? CraftDelayId { get; }

		public int? CraftDelayTime { get; }

		public int? MaxProductionCount { get; }

		public void AddComboProduct(int itemId)
		{
			_comboProducts.Add(itemId);
		}

		public void BeginComponentData()
		{
			_currentComponents = [];
		}

		public void AddComponent(int itemId, long quantity)
		{
			_currentComponents ??= [];
			_currentComponents.Add(new RecipeComponentSummary(itemId, quantity));
		}

		public void EndComponentData()
		{
			if (_currentComponents is { Count: > 0 })
				_componentGroups.Add(new RecipeComponentDataSummary(_currentComponents.AsReadOnly()));

			_currentComponents = null;
		}

		public RecipeTemplateSummary ToSummary()
		{
			return new RecipeTemplateSummary(
				RecipeId,
				NameId,
				SkillId,
				Race,
				SkillPoint,
				Dp,
				AutoLearn,
				ProductId,
				Quantity,
				_comboProducts.Count == 0 ? null : _comboProducts.AsReadOnly(),
				CraftDelayId,
				CraftDelayTime,
				_componentGroups.Count == 0 ? null : _componentGroups.AsReadOnly(),
				MaxProductionCount);
		}
	}

	private sealed class ItemSetBuilder
	{
		private readonly HashSet<int> _itemIds = [];
		private readonly List<ItemSetPartBonus> _partBonuses = [];
		private List<ItemStatModifier>? _currentModifiers;
		private int _currentPartBonusIndex = -1;
		private bool _isBuildingFullBonus;

		public ItemSetBuilder(int setId, string name)
		{
			SetId = setId;
			Name = name;
		}

		private int SetId { get; }

		private string Name { get; }

		private ItemSetFullBonus? FullBonus { get; set; }

		public void AddItemPart(int itemId)
		{
			_itemIds.Add(itemId);
		}

		public void StartPartBonus(int count)
		{
			_currentModifiers = [];
			_currentPartBonusIndex = _partBonuses.Count;
			_isBuildingFullBonus = false;
			_partBonuses.Add(new ItemSetPartBonus(count, _currentModifiers));
		}

		public void StartFullBonus()
		{
			_currentModifiers = [];
			_currentPartBonusIndex = -1;
			_isBuildingFullBonus = true;
			FullBonus = new ItemSetFullBonus(_itemIds.Count, _currentModifiers);
		}

		public void AddModifier(ItemStatModifier modifier)
		{
			_currentModifiers ??= [];
			_currentModifiers.Add(modifier);
			if (_isBuildingFullBonus)
				FullBonus = new ItemSetFullBonus(_itemIds.Count, _currentModifiers);
			else if (_currentPartBonusIndex >= 0)
				_partBonuses[_currentPartBonusIndex] = _partBonuses[_currentPartBonusIndex] with { Modifiers = _currentModifiers };
		}

		public void EndBonus()
		{
			_currentModifiers = null;
			_currentPartBonusIndex = -1;
			_isBuildingFullBonus = false;
		}

		public ItemSetSummary ToSummary()
		{
			// Java parity: model/templates/itemset/ItemSetTemplate.afterUnmarshal sets full-bonus count to itempart size.
			return new ItemSetSummary(
				SetId,
				Name,
				_itemIds.ToHashSet(),
				_partBonuses.AsReadOnly(),
				FullBonus);
		}
	}

	private sealed class EnchantGroupBuilder
	{
		private readonly List<EnchantLevelSummary> _levels = [];
		private List<EnchantStatSummary>? _currentStats;
		private int _currentLevelIndex = -1;

		public EnchantGroupBuilder(string itemGroup)
		{
			ItemGroup = itemGroup;
		}

		private string ItemGroup { get; }

		public void StartLevel(int level)
		{
			_currentStats = [];
			_currentLevelIndex = _levels.Count;
			_levels.Add(new EnchantLevelSummary(level, _currentStats));
		}

		public void AddStat(EnchantStatSummary stat)
		{
			_currentStats ??= [];
			_currentStats.Add(stat);
			if (_currentLevelIndex >= 0)
				_levels[_currentLevelIndex] = _levels[_currentLevelIndex] with { Stats = _currentStats };
		}

		public void EndLevel()
		{
			_currentStats = null;
			_currentLevelIndex = -1;
		}

		public EnchantGroupSummary ToSummary()
		{
			// Java parity: model/enchants/EnchantList item_group mapped by dataholders/EnchantData.afterUnmarshal.
			return new EnchantGroupSummary(ItemGroup, _levels.AsReadOnly());
		}
	}
	private sealed class CosmeticItemBuilder
	{
		public CosmeticItemBuilder(string type, string cosmeticName, int id, string race, string genderPermitted)
		{
			Type = type;
			CosmeticName = cosmeticName;
			Id = id;
			Race = race;
			GenderPermitted = genderPermitted;
		}

		private string Type { get; }

		private string CosmeticName { get; }

		private int Id { get; }

		private string Race { get; }

		private string GenderPermitted { get; }

		private float Scale { get; set; }

		private int HairType { get; set; }

		private int FaceType { get; set; }

		private int HairColor { get; set; }

		private int LipColor { get; set; }

		private int EyeColor { get; set; }

		private int SkinColor { get; set; }

		private bool HasPreset { get; set; }

		public void SetPresetValue(string name, string value)
		{
			// Java parity: model/templates/cosmeticitems/CosmeticItemTemplate.Preset JAXB fields.
			HasPreset = true;
			switch (name)
			{
				case "scale":
					Scale = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale) ? parsedScale : 0f;
					break;
				case "hair_type":
					HairType = ParseInt(value);
					break;
				case "face_type":
					FaceType = ParseInt(value);
					break;
				case "hair_color":
					HairColor = ParseInt(value);
					break;
				case "lip_color":
					LipColor = ParseInt(value);
					break;
				case "eye_color":
					EyeColor = ParseInt(value);
					break;
				case "skin_color":
					SkinColor = ParseInt(value);
					break;
			}
		}

		public CosmeticItemSummary ToSummary()
		{
			return new CosmeticItemSummary(
				Type,
				CosmeticName,
				Id,
				Race,
				GenderPermitted,
				HasPreset
					? new CosmeticPresetSummary(Scale, HairType, FaceType, HairColor, LipColor, EyeColor, SkinColor)
					: null);
		}

		private static int ParseInt(string value)
		{
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
		}
	}

	private sealed class DecomposableItemBuilder
	{
		private readonly List<ExtractedItemsCollectionBuilder> _collections = [];
		private ExtractedItemsCollectionBuilder? _currentCollection;

		public DecomposableItemBuilder(int itemId, bool isSelectable)
		{
			ItemId = itemId;
			IsSelectable = isSelectable;
		}

		private int ItemId { get; }

		private bool IsSelectable { get; }

		public void StartCollection(float chance, int minLevel, int maxLevel)
		{
			_currentCollection = new ExtractedItemsCollectionBuilder(chance, minLevel, maxLevel);
			_collections.Add(_currentCollection);
		}

		public void AddItem(ResultedItemSummary item)
		{
			_currentCollection?.Items.Add(item);
		}

		public void AddRandomItem(RandomItemSummary item)
		{
			_currentCollection?.RandomItems.Add(item);
		}

		public void EndCollection()
		{
			_currentCollection = null;
		}

		public DecomposableItemSummary ToSummary()
		{
			// Java parity: dataholders/DecomposableItemsData maps normal groups separately from selectable rewards.
			return new DecomposableItemSummary(
				ItemId,
				IsSelectable,
				_collections.Select(collection => collection.ToSummary()).ToArray());
		}
	}

	private sealed class ItemPurificationResultBuilder
	{
		private readonly int _resultItemId;
		private readonly int _minEnchantCount;
		private readonly int _necessaryAbyssPoints;
		private readonly long _necessaryKinah;
		private readonly List<ItemPurificationMaterialSummary> _requiredMaterials = [];

		public ItemPurificationResultBuilder(
			int resultItemId,
			int minEnchantCount,
			int necessaryAbyssPoints,
			long necessaryKinah)
		{
			_resultItemId = resultItemId;
			_minEnchantCount = minEnchantCount;
			_necessaryAbyssPoints = necessaryAbyssPoints;
			_necessaryKinah = necessaryKinah;
		}

		public void AddRequiredMaterial(ItemPurificationMaterialSummary requiredMaterial)
		{
			_requiredMaterials.Add(requiredMaterial);
		}

		public ItemPurificationResultSummary ToSummary()
		{
			return new ItemPurificationResultSummary(
				_resultItemId,
				_minEnchantCount,
				_necessaryAbyssPoints,
				_necessaryKinah,
				_requiredMaterials.AsReadOnly());
		}
	}

	private sealed class WalkerTemplateBuilder
	{
		private readonly List<WalkerRouteStepBuilder> _routeSteps = [];

		public WalkerTemplateBuilder(string routeId, int pool, string formation, string loopType, string rows)
		{
			RouteId = routeId;
			Pool = pool;
			Formation = string.IsNullOrWhiteSpace(formation) ? "POINT" : formation.ToUpperInvariant();
			LoopType = string.IsNullOrWhiteSpace(loopType) ? "NORMAL" : loopType.ToUpperInvariant();
			Rows = rows;
		}

		private string RouteId { get; }

		private int Pool { get; }

		private string Formation { get; set; }

		private string LoopType { get; }

		private string Rows { get; }

		public void AddRouteStep(float x, float y, float z, int restTime)
		{
			_routeSteps.Add(new WalkerRouteStepBuilder(x, y, z, restTime));
		}

		public WalkerTemplateSummary ToSummary()
		{
			// Java parity: model/templates/walker/WalkerTemplate.afterUnmarshal expands WALK_BACK routes and normalizes formations.
			if (LoopType == "WALK_BACK" && _routeSteps.Count > 2)
			{
				for (var i = _routeSteps.Count - 2; i > 0; i--)
				{
					var step = _routeSteps[i];
					_routeSteps.Add(new WalkerRouteStepBuilder(step.X, step.Y, step.Z, step.RestTime));
				}
			}

			var rows = ResolveRows();
			var routeSteps = _routeSteps
				.Select(
					(step, index) => new WalkerRouteStepSummary(
						step.X,
						step.Y,
						step.Z,
						step.RestTime,
						index,
						index == _routeSteps.Count - 1))
				.ToArray();
			return new WalkerTemplateSummary(RouteId, Pool, Formation, LoopType, rows, routeSteps);
		}

		private IReadOnlyList<int> ResolveRows()
		{
			if (Pool == 2)
			{
				Formation = "SQUARE";
				return [2];
			}

			if (Formation != "SQUARE")
				return Array.Empty<int>();

			var rows = Rows
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
				.Where(value => value > 0)
				.ToArray();
			if (rows.Length > 0)
				return rows;

			Formation = "POINT";
			return Array.Empty<int>();
		}

		private readonly record struct WalkerRouteStepBuilder(float X, float Y, float Z, int RestTime);
	}

	private sealed class ExtractedItemsCollectionBuilder
	{
		public ExtractedItemsCollectionBuilder(float chance, int minLevel, int maxLevel)
		{
			Chance = chance;
			MinLevel = minLevel;
			MaxLevel = maxLevel;
		}

		private float Chance { get; }

		private int MinLevel { get; }

		private int MaxLevel { get; }

		public List<ResultedItemSummary> Items { get; } = [];

		public List<RandomItemSummary> RandomItems { get; } = [];

		public ExtractedItemsCollectionSummary ToSummary()
		{
			// Java parity: model/templates/rewards/ResultedItemsCollection fixed items plus random_item entries.
			return new ExtractedItemsCollectionSummary(
				Chance,
				MinLevel,
				MaxLevel,
				Items.ToArray(),
				RandomItems.ToArray());
		}
	}

	private static PlayerSpawnLocation ReadSpawnLocation(XmlReader reader)
	{
		// Java parity: dataholders/PlayerInitialData.LocationData.
		return new PlayerSpawnLocation(
			ReadRequiredIntAttribute(reader, "map_id"),
			ReadFloatAttribute(reader, "x"),
			ReadFloatAttribute(reader, "y"),
			ReadFloatAttribute(reader, "z"),
			ReadIntAttribute(reader, "heading"));
	}

	private sealed class QuestDropBuilder
	{
		private readonly List<PendingQuestDrop> _questDrops = [];
		private readonly List<QuestCollectItemSummary> _collectItems = [];

		public QuestDropBuilder(int questId, string target, string mentorType)
		{
			QuestId = questId;
			Target = string.IsNullOrWhiteSpace(target) ? "NONE" : target;
			MentorType = string.IsNullOrWhiteSpace(mentorType) ? "NONE" : mentorType;
		}

		private int QuestId { get; }

		private string Target { get; }

		private string MentorType { get; }

		public void AddQuestDrop(int npcId, int itemId, int chance, int dropEachMember, int collectingStep)
		{
			_questDrops.Add(new PendingQuestDrop(npcId, itemId, chance, dropEachMember, collectingStep));
		}

		public void AddCollectItem(int itemId, long count)
		{
			_collectItems.Add(new QuestCollectItemSummary(itemId, count));
		}

		public IReadOnlyList<QuestDropSummary> ToQuestDrops()
		{
			var collectItems = _collectItems.ToArray();
			return _questDrops
				.Select(
					drop => new QuestDropSummary(
						QuestId,
						drop.NpcId,
						drop.ItemId,
						drop.Chance,
						drop.DropEachMember,
						drop.CollectingStep,
						Target,
						MentorType,
						collectItems))
				.ToArray();
		}

		private sealed record PendingQuestDrop(int NpcId, int ItemId, int Chance, int DropEachMember, int CollectingStep);
	}

}
