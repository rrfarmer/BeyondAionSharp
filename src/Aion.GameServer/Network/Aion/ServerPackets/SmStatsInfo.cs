using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmStatsInfo : GameServerPacket
{
	public const int PacketOpCode = 1;
	private const int BaseFlyTime = 60;
	private const int KinahItemId = 182400001;

	private readonly Player _player;
	private readonly PlayerExperienceTable? _experienceTable;
	private readonly int _gameMinutes;
	private readonly ItemTemplateTable? _itemTemplates;
	private readonly ItemRandomBonusTable? _itemRandomBonuses;
	private readonly ItemSetTable? _itemSets;
	private readonly EnchantTable? _enchantTemplates;
	private readonly TemperingTable? _temperingTemplates;

	public SmStatsInfo(
		Player player,
		PlayerExperienceTable? experienceTable,
		int gameMinutes,
		ItemTemplateTable? itemTemplates = null,
		ItemRandomBonusTable? itemRandomBonuses = null,
		ItemSetTable? itemSets = null,
		EnchantTable? enchantTemplates = null,
		TemperingTable? temperingTemplates = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_STATS_INFO(Player).
		_player = player;
		_experienceTable = experienceTable;
		_gameMinutes = gameMinutes;
		_itemTemplates = itemTemplates;
		_itemRandomBonuses = itemRandomBonuses;
		_itemSets = itemSets;
		_enchantTemplates = enchantTemplates;
		_temperingTemplates = temperingTemplates;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_STATS_INFO.writeImpl. Full effects remain deferred, but equipped item template stats are applied when templates are loaded.
		var context = PlayerStatsContext.Create(_player, _experienceTable, _itemTemplates, _itemRandomBonuses, _itemSets, _enchantTemplates, _temperingTemplates);

		buffer.WriteD(_player.ObjectId);
		buffer.WriteD(_gameMinutes);

		WritePrimaryStats(buffer, context.Current);
		WriteElementalResists(buffer);
		buffer.WriteH(context.Level);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);

		buffer.WriteQ(context.ExpNeed);
		buffer.WriteQ(_player.RecoverableExp);
		buffer.WriteQ(context.ExpShown);
		buffer.WriteD(0);

		buffer.WriteD(context.Current.MaxHp);
		buffer.WriteD(context.CurrentHp);
		buffer.WriteD(context.Current.MaxMp);
		buffer.WriteD(context.CurrentMp);
		buffer.WriteH(context.Current.MaxDp);
		buffer.WriteH(_player.Dp);
		buffer.WriteD(context.Current.FlyTime);
		buffer.WriteD(context.CurrentFp);
		buffer.WriteC(0);
		buffer.WriteC(0);

		WriteCombatStats(buffer, context.Current);
		buffer.WriteD(GetInventoryLimit(_player));
		buffer.WriteD(GetInventorySize(_player));
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(context.ClassStats.ClassId);

		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteQ(context.CurrentReposeEnergy);
		buffer.WriteQ(context.MaxReposeEnergy);
		buffer.WriteQ(0);

		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(1);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);

		WritePrimaryStats(buffer, context.Base);
		WriteElementalResists(buffer);
		buffer.WriteD(context.Base.MaxHp);
		buffer.WriteD(context.Base.MaxMp);
		buffer.WriteH(context.Base.MaxDp);
		buffer.WriteH(21592);
		buffer.WriteD(context.Base.FlyTime);
		WriteBaseCombatStats(buffer, context.Base);
	}

	private static void WritePrimaryStats(PacketBuffer buffer, PlayerCalculatedStats stats)
	{
		buffer.WriteH(stats.Power);
		buffer.WriteH(stats.Health);
		buffer.WriteH(stats.Accuracy);
		buffer.WriteH(stats.Agility);
		buffer.WriteH(stats.Knowledge);
		buffer.WriteH(stats.Will);
	}

	private static void WriteElementalResists(PacketBuffer buffer)
	{
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteH(0);
	}

	private static void WriteCombatStats(PacketBuffer buffer, PlayerCalculatedStats stats)
	{
		buffer.WriteH(stats.MainHandPhysicalAttack);
		buffer.WriteH(stats.OffHandPhysicalAttack);
		buffer.WriteH(0);
		buffer.WriteD(stats.PhysicalDefense);
		buffer.WriteH(stats.MainHandMagicalAttack);
		buffer.WriteH(stats.OffHandMagicalAttack);
		buffer.WriteD(stats.MagicalDefense);
		buffer.WriteH(stats.MagicalResist);
		buffer.WriteH(0);
		buffer.WriteF(stats.AttackRange / 1000f);
		buffer.WriteH(stats.AttackSpeed);
		buffer.WriteH(stats.Evasion);
		buffer.WriteH(stats.Parry);
		buffer.WriteH(stats.Block);
		buffer.WriteH(stats.MainHandPhysicalCritical);
		buffer.WriteH(stats.OffHandPhysicalCritical);
		buffer.WriteH(stats.MainHandPhysicalAccuracy);
		buffer.WriteH(stats.OffHandPhysicalAccuracy);
		buffer.WriteH(1);
		buffer.WriteH(stats.MagicalAccuracy);
		buffer.WriteH(stats.MagicalCritical);
		buffer.WriteH(0);
		buffer.WriteF(stats.CastingSpeed / 1000f);
		buffer.WriteH(0);
		buffer.WriteH(stats.Concentration);
		buffer.WriteH(stats.MagicalBoost);
		buffer.WriteH(stats.MagicalSuppression);
		buffer.WriteH(stats.HealBoost);
		buffer.WriteH(0);
		buffer.WriteH(stats.PhysicalCriticalResist);
		buffer.WriteH(stats.MagicalCriticalResist);
		buffer.WriteH(stats.PhysicalCriticalDamageReduce);
		buffer.WriteH(stats.MagicalCriticalDamageReduce);
	}

	private static void WriteBaseCombatStats(PacketBuffer buffer, PlayerCalculatedStats stats)
	{
		buffer.WriteH(stats.MainHandPhysicalAttack);
		buffer.WriteH(stats.OffHandPhysicalAttack);
		buffer.WriteH(stats.MainHandMagicalAttack);
		buffer.WriteH(stats.OffHandMagicalAttack);
		buffer.WriteD(stats.PhysicalDefense);
		buffer.WriteD(stats.MagicalDefense);
		buffer.WriteH(stats.MagicalResist);
		buffer.WriteF(stats.AttackRange / 1000f);
		buffer.WriteH(0);
		buffer.WriteH(stats.Evasion);
		buffer.WriteH(stats.Parry);
		buffer.WriteH(stats.Block);
		buffer.WriteH(stats.MainHandPhysicalCritical);
		buffer.WriteH(stats.OffHandPhysicalCritical);
		buffer.WriteH(stats.MagicalCritical);
		buffer.WriteH(0);
		buffer.WriteH(stats.MainHandPhysicalAccuracy);
		buffer.WriteH(stats.OffHandPhysicalAccuracy);
		buffer.WriteH(0);
		buffer.WriteH(stats.MagicalAccuracy);
		buffer.WriteH(stats.Concentration);
		buffer.WriteH(stats.MagicalBoost);
		buffer.WriteH(stats.MagicalSuppression);
		buffer.WriteH(stats.HealBoost);
		buffer.WriteH(0);
		buffer.WriteH(stats.PhysicalCriticalResist);
		buffer.WriteH(stats.MagicalCriticalResist);
		buffer.WriteH(stats.PhysicalCriticalDamageReduce);
		buffer.WriteH(stats.MagicalCriticalDamageReduce);
	}

	private static int GetInventoryLimit(Player player)
	{
		// Java parity: model/gameobjects/player/Player.setCubeLimit with StorageType.CUBE base row length.
		return 27 + (player.NpcExpands + player.QuestExpands + player.ItemExpands) * 9;
	}

	private static int GetInventorySize(Player player)
	{
		// Java parity: player.getInventory().size excludes equipped items and kinah, which are stored separately in Java.
		return player.InventoryItems.Count(item => item.Location == 0 && !item.IsEquipped && item.ItemId != KinahItemId);
	}

	private sealed record PlayerStatsContext(
		PlayerClassStats ClassStats,
		PlayerCalculatedStats Base,
		PlayerCalculatedStats Current,
		int Level,
		long ExpNeed,
		long ExpShown,
		long CurrentReposeEnergy,
		long MaxReposeEnergy,
		int CurrentHp,
		int CurrentMp,
		int CurrentFp)
	{
		public static PlayerStatsContext Create(
			Player player,
			PlayerExperienceTable? experienceTable,
			ItemTemplateTable? itemTemplates,
			ItemRandomBonusTable? itemRandomBonuses,
			ItemSetTable? itemSets,
			EnchantTable? enchantTemplates,
			TemperingTable? temperingTemplates)
		{
			// Java parity: PlayerCommonData.setExp/updateMaxRepose plus PlayerClass.createStatsTemplate.
			var classStats = PlayerClassStats.Get(player.PlayerClass);
			var level = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
			var expStart = GetStartExp(experienceTable, level);
			var expNeed = GetExpNeed(experienceTable, level);
			var maxRepose = level >= 10 ? (long)(expNeed * 0.25f) : 0;
			var currentRepose = Math.Clamp(player.ReposeEnergy, 0, maxRepose);
			var baseStats = PlayerCalculatedStats.Create(classStats, level);
			var currentStats = itemTemplates == null
				? baseStats
				: PlayerEquipmentStats.Apply(player, itemTemplates, itemRandomBonuses, itemSets, enchantTemplates, temperingTemplates, baseStats);
			var lifeStats = player.LifeStats;
			return new PlayerStatsContext(
				classStats,
				baseStats,
				currentStats,
				level,
				expNeed,
				Math.Max(0, player.Exp - expStart),
				currentRepose,
				maxRepose,
				lifeStats?.GetCurrentHp(currentStats.MaxHp) ?? currentStats.MaxHp,
				lifeStats?.GetCurrentMp(currentStats.MaxMp) ?? currentStats.MaxMp,
				lifeStats?.GetCurrentFp() ?? currentStats.FlyTime);
		}

		private static long GetStartExp(PlayerExperienceTable? experienceTable, int level)
		{
			if (experienceTable == null || level <= 0 || level > experienceTable.MaxLevel)
				return 0;
			return experienceTable.GetStartExpForLevel(level);
		}

		private static long GetExpNeed(PlayerExperienceTable? experienceTable, int level)
		{
			if (experienceTable == null || level <= 0 || level >= experienceTable.MaxLevel)
				return 0;
			return experienceTable.GetStartExpForLevel(level + 1) - experienceTable.GetStartExpForLevel(level);
		}
	}

	private static class PlayerEquipmentStats
	{
		private const long MainHand = 1L;
		private const long SubHand = 1L << 1;
		private const long MainOffHand = 1L << 17;
		private const long SubOffHand = 1L << 18;
		private const int ChargeLevel1 = 500000;

		private static readonly HashSet<string> FusionMagicalBoostWeaponGroups = new(StringComparer.Ordinal)
		{
			"ORB", "STAFF", "SPELLBOOK", "GUN", "CANNON", "HARP", "KEYBLADE",
		};

		public static PlayerCalculatedStats Apply(
			Player player,
			ItemTemplateTable itemTemplates,
			ItemRandomBonusTable? itemRandomBonuses,
			ItemSetTable? itemSets,
			EnchantTable? enchantTemplates,
			TemperingTable? temperingTemplates,
			PlayerCalculatedStats baseStats)
		{
			// Java parity: model/stats/listeners/ItemEquipmentListener.onItemEquipment plus PlayerGameStats weapon stat accessors.
			var equippedItems = player.InventoryItems
				.Where(item => item.IsEquipped && item.Location == 0)
				.Select(item => (Item: item, Template: itemTemplates.GetItemTemplate(item.ItemId)))
				.Where(item => item.Template != null)
				.Select(item => new EquippedItem(item.Item, item.Template!))
				.ToArray();
			if (equippedItems.Length == 0)
				return baseStats;

			var modifiers = equippedItems
				.SelectMany(item => GetEquipmentModifiers(item, itemTemplates, itemRandomBonuses, enchantTemplates, temperingTemplates))
				.Concat(GetItemSetModifiers(equippedItems, itemSets))
				.Where(modifier => !string.IsNullOrEmpty(modifier.Name))
				.ToArray();

			var mainWeapon = equippedItems.FirstOrDefault(item => item.Template.IsWeapon && IsRightHandSlot(item.Item.Slot));
			var offHandWeapon = equippedItems.FirstOrDefault(item =>
				item.Template.IsWeapon
				&& item != mainWeapon
				&& IsLeftHandSlot(item.Item.Slot)
				&& !IsTwoHandedSlot(item.Item.Slot));
			var mainWeaponStats = mainWeapon?.Template.WeaponStats;
			var offHandWeaponStats = offHandWeapon?.Template.WeaponStats;

			var current = baseStats with
			{
				Power = CalculateStat("POWER", baseStats.Power, modifiers),
				Health = CalculateStat("HEALTH", baseStats.Health, modifiers),
				Accuracy = CalculateStat("ACCURACY", baseStats.Accuracy, modifiers),
				Agility = CalculateStat("AGILITY", baseStats.Agility, modifiers),
				Knowledge = CalculateStat("KNOWLEDGE", baseStats.Knowledge, modifiers),
				Will = CalculateStat("WILL", baseStats.Will, modifiers),
			};

			var mainAttackRange = mainWeaponStats?.AttackRange ?? baseStats.AttackRange;
			var offHandAttackRange = offHandWeaponStats?.AttackRange;
			var attackRange = offHandAttackRange.HasValue
				? Math.Min(mainAttackRange, offHandAttackRange.Value)
				: mainAttackRange;
			var attackSpeed = mainWeaponStats == null
				? baseStats.AttackSpeed
				: mainWeaponStats.AttackSpeed + (offHandWeaponStats?.AttackSpeed / 4 ?? 0);
			var mainPhysicalAttack = mainWeaponStats != null && mainWeapon?.Template.IsMagicalAttackWeapon == false
				? CalculateStat("PHYSICAL_ATTACK", mainWeaponStats.MeanDamage, modifiers, baseRate: current.Power * 0.01f)
				: CalculateStat("PHYSICAL_ATTACK", baseStats.MainHandPhysicalAttack, modifiers);
			var offPhysicalAttack = offHandWeaponStats != null && offHandWeapon?.Template.IsMagicalAttackWeapon == false
				? CalculateStat("PHYSICAL_ATTACK", offHandWeaponStats.MeanDamage, modifiers, baseRate: current.Power * 0.01f)
				: 0;
			var mainMagicalAttack = mainWeaponStats != null && mainWeapon?.Template.IsMagicalAttackWeapon == true
				? CalculateStat("MAGICAL_ATTACK", mainWeaponStats.MeanDamage, modifiers, baseRate: current.Knowledge * 0.01f)
				: CalculateStat("MAGICAL_ATTACK", baseStats.MainHandMagicalAttack, modifiers, baseRate: current.Knowledge * 0.01f);
			var offMagicalAttack = offHandWeaponStats != null && offHandWeapon?.Template.IsMagicalAttackWeapon == true
				? CalculateStat("MAGICAL_ATTACK", offHandWeaponStats.MeanDamage, modifiers, baseRate: current.Knowledge * 0.01f)
				: 0;

			return current with
			{
				MaxHp = CalculateStat("MAXHP", baseStats.MaxHp, modifiers),
				MaxMp = CalculateStat("MAXMP", baseStats.MaxMp, modifiers),
				MaxDp = CalculateStat("MAXDP", baseStats.MaxDp, modifiers),
				FlyTime = CalculateStat("FLY_TIME", baseStats.FlyTime, modifiers),
				Evasion = CalculateStat("EVASION", baseStats.Evasion, modifiers),
				Parry = CalculateStat("PARRY", baseStats.Parry + (mainWeaponStats?.Parry ?? 0), modifiers),
				Block = CalculateStat("BLOCK", baseStats.Block, modifiers),
				MainHandPhysicalAccuracy = CalculateStat("PHYSICAL_ACCURACY", baseStats.MainHandPhysicalAccuracy + (mainWeaponStats?.PhysicalAccuracy ?? 0), modifiers),
				OffHandPhysicalAccuracy = offHandWeaponStats == null
					? 0
					: CalculateStat("PHYSICAL_ACCURACY", baseStats.MainHandPhysicalAccuracy + offHandWeaponStats.PhysicalAccuracy, modifiers),
				MagicalAccuracy = CalculateStat("MAGICAL_ACCURACY", baseStats.MagicalAccuracy + (mainWeaponStats?.MagicalAccuracy ?? 0), modifiers),
				PhysicalCriticalResist = CalculateStat("PHYSICAL_CRITICAL_RESIST", baseStats.PhysicalCriticalResist, modifiers),
				MagicalCriticalResist = CalculateStat("MAGICAL_CRITICAL_RESIST", baseStats.MagicalCriticalResist, modifiers),
				MainHandPhysicalAttack = mainPhysicalAttack,
				OffHandPhysicalAttack = offPhysicalAttack,
				PhysicalDefense = CalculateStat("PHYSICAL_DEFENSE", baseStats.PhysicalDefense, modifiers),
				MainHandMagicalAttack = mainMagicalAttack,
				OffHandMagicalAttack = offMagicalAttack,
				MagicalDefense = CalculateStat("MAGICAL_DEFEND", baseStats.MagicalDefense, modifiers),
				MagicalResist = CalculateStat("MAGICAL_RESIST", baseStats.MagicalResist, modifiers),
				AttackRange = CalculateStat("ATTACK_RANGE", attackRange, modifiers),
				AttackSpeed = CalculateStat("ATTACK_SPEED", attackSpeed, modifiers),
				MainHandPhysicalCritical = CalculateStat(
					"PHYSICAL_CRITICAL",
					baseStats.MainHandPhysicalCritical + (mainWeaponStats != null && mainWeapon?.Template.IsMagicalAttackWeapon == false ? mainWeaponStats.PhysicalCritical : 0),
					modifiers),
				OffHandPhysicalCritical = offHandWeaponStats == null || offHandWeapon?.Template.IsMagicalAttackWeapon == true
					? 0
					: CalculateStat("PHYSICAL_CRITICAL", baseStats.MainHandPhysicalCritical + offHandWeaponStats.PhysicalCritical, modifiers),
				MagicalCritical = CalculateStat(
					"MAGICAL_CRITICAL",
					baseStats.MagicalCritical + (mainWeaponStats != null && mainWeapon?.Template.IsMagicalAttackWeapon == true ? mainWeaponStats.PhysicalCritical : 0),
					modifiers),
				CastingSpeed = CalculateStat("BOOST_CASTING_TIME", baseStats.CastingSpeed, modifiers, reverse: true),
				Concentration = CalculateStat("CONCENTRATION", baseStats.Concentration, modifiers),
				MagicalBoost = CalculateStat("BOOST_MAGICAL_SKILL", baseStats.MagicalBoost + (mainWeaponStats?.MagicalBoost ?? 0), modifiers),
				MagicalSuppression = CalculateStat("MAGIC_SKILL_BOOST_RESIST", baseStats.MagicalSuppression, modifiers),
				HealBoost = CalculateStat("HEAL_BOOST", baseStats.HealBoost, modifiers),
				PhysicalCriticalDamageReduce = CalculateStat("PHYSICAL_CRITICAL_DAMAGE_REDUCE", baseStats.PhysicalCriticalDamageReduce, modifiers),
				MagicalCriticalDamageReduce = CalculateStat("MAGICAL_CRITICAL_DAMAGE_REDUCE", baseStats.MagicalCriticalDamageReduce, modifiers),
			};
		}

		private static IEnumerable<ItemStatModifier> GetEquipmentModifiers(
			EquippedItem item,
			ItemTemplateTable itemTemplates,
			ItemRandomBonusTable? itemRandomBonuses,
			EnchantTable? enchantTemplates,
			TemperingTable? temperingTemplates)
		{
			foreach (var modifier in GetTemplateModifiers(item))
				yield return modifier;
			foreach (var modifier in GetRandomBonusModifiers(itemRandomBonuses, item.Template.StatBonusSetId, item.Item.RandomBonus))
				yield return modifier;
			foreach (var modifier in GetFusionedWeaponModifiers(item, itemTemplates))
				yield return modifier;
			var fusionedTemplate = item.Item.FusionedItem == 0 ? null : itemTemplates.GetItemTemplate(item.Item.FusionedItem);
			if (fusionedTemplate != null)
			{
				foreach (var modifier in GetRandomBonusModifiers(itemRandomBonuses, fusionedTemplate.StatBonusSetId, item.Item.FusionRandomBonus))
					yield return modifier;
			}

			// Java parity: model/stats/listeners/ItemEquipmentListener.addStonesStats + model/items/ManaStone constructor.
			foreach (var modifier in GetStoneModifiers(item.Item.ManaStones, itemTemplates))
				yield return modifier;
			foreach (var modifier in GetStoneModifiers(item.Item.FusionStones, itemTemplates))
				yield return modifier;
			foreach (var modifier in GetIdianModifiers(item, itemTemplates, itemRandomBonuses))
				yield return modifier;
			foreach (var modifier in GetEnchantModifiers(item, enchantTemplates))
				yield return modifier;
			foreach (var modifier in GetTemperingModifiers(item, temperingTemplates))
				yield return modifier;
		}

		private static IReadOnlyList<ItemStatModifier> GetRandomBonusModifiers(
			ItemRandomBonusTable? itemRandomBonuses,
			int statBonusSetId,
			int statBonusId)
		{
			// Java parity: model/items/RandomBonusEffect applies StatBonusType.INVENTORY selected by item rnd_bonus/fusion_rnd_bonus.
			return itemRandomBonuses?.GetModifiers("INVENTORY", statBonusSetId, statBonusId) ?? Array.Empty<ItemStatModifier>();
		}

		private static IEnumerable<ItemStatModifier> GetTemplateModifiers(EquippedItem item)
		{
			// Java parity: model/stats/calc/functions/StatFunction.validate with skillengine/condition/ItemChargeCondition.
			var chargeLevel = GetChargeLevel(item.Item.Charge);
			foreach (var modifier in item.Template.StatModifiers)
			{
				if (modifier.ChargeCondition <= 0 || chargeLevel >= modifier.ChargeCondition)
					yield return modifier;
			}
		}

		private static IEnumerable<ItemStatModifier> GetStoneModifiers(IReadOnlyList<ItemStoneSocket> stones, ItemTemplateTable itemTemplates)
		{
			foreach (var stone in stones)
			{
				var template = itemTemplates.GetItemTemplate(stone.ItemId);
				if (template == null)
					continue;

				foreach (var modifier in template.StatModifiers)
					yield return modifier;
			}
		}

		private static IReadOnlyList<ItemStatModifier> GetIdianModifiers(
			EquippedItem item,
			ItemTemplateTable itemTemplates,
			ItemRandomBonusTable? itemRandomBonuses)
		{
			// Java parity: model/items/IdianStone.onEquip applies RandomBonusEffect(StatBonusType.POLISH) only for charged main-hand idians.
			if (item.Item.IdianStone is not { PolishCharge: > 0, PolishNumber: > 0 } idianStone
				|| (item.Item.Slot & MainHand) == 0)
			{
				return Array.Empty<ItemStatModifier>();
			}

			var idianTemplate = itemTemplates.GetItemTemplate(idianStone.ItemId);
			return itemRandomBonuses?.GetModifiers("POLISH", idianTemplate?.PolishSetId ?? 0, idianStone.PolishNumber)
				?? Array.Empty<ItemStatModifier>();
		}

		private static IEnumerable<ItemStatModifier> GetFusionedWeaponModifiers(EquippedItem item, ItemTemplateTable itemTemplates)
		{
			// Java parity: model/stats/listeners/ItemEquipmentListener.addWeaponStats fusioned-item branch.
			if (item.Item.FusionedItem == 0 || !IsMainOrSubSlot(item.Item.Slot))
				yield break;

			var template = itemTemplates.GetItemTemplate(item.Item.FusionedItem);
			if (template == null)
				yield break;

			foreach (var modifier in template.StatModifiers.Where(IsApplicableFusionWeaponModifier))
				yield return modifier;

			var weaponStats = template.WeaponStats;
			if (weaponStats == null)
				yield break;

			var magicalBoost = (int)(0.1f * weaponStats.MagicalBoost);
			if (magicalBoost != 0 && FusionMagicalBoostWeaponGroups.Contains(template.ItemGroup))
				yield return new ItemStatModifier("add", "BOOST_MAGICAL_SKILL", magicalBoost, Bonus: false);

			var attack = (int)(0.1f * weaponStats.MeanDamage);
			if (attack != 0)
				yield return new ItemStatModifier("add", item.Template.IsMagicalAttackWeapon ? "MAGICAL_ATTACK" : "PHYSICAL_ATTACK", attack, Bonus: false);
		}

		private static IReadOnlyList<ItemStatModifier> GetEnchantModifiers(EquippedItem item, EnchantTable? enchantTemplates)
		{
			// Java parity: services/EnchantService.applyEnchantEffect adds model/enchants/EnchantEffect when an equipped item has enchant level > 0.
			return enchantTemplates?.GetModifiers(item.Template, item.Item.Enchant, item.Item.Slot) ?? Array.Empty<ItemStatModifier>();
		}

		private static IReadOnlyList<ItemStatModifier> GetTemperingModifiers(EquippedItem item, TemperingTable? temperingTemplates)
		{
			// Java parity: model/enchants/TemperingEffect.apply when equipped item tempering > 0.
			return temperingTemplates?.GetModifiers(item.Template, item.Item.Tempering, item.Item.RandomPlumeBonus) ?? Array.Empty<ItemStatModifier>();
		}

		private static IEnumerable<ItemStatModifier> GetItemSetModifiers(
			IReadOnlyList<EquippedItem> equippedItems,
			ItemSetTable? itemSets)
		{
			// Java parity: model/stats/listeners/ItemEquipmentListener.recalculateItemSet + Equipment.itemSetPartsEquipped.
			if (itemSets == null)
				yield break;

			var equippedSetItemIds = new Dictionary<int, HashSet<int>>();
			foreach (var item in equippedItems)
			{
				if (IsAlternateWeaponSlot(item.Item.Slot))
					continue;

				var set = itemSets.GetItemSetTemplateByItemId(item.Item.ItemId);
				if (set == null)
					continue;

				if (!equippedSetItemIds.TryGetValue(set.SetId, out var itemIds))
				{
					itemIds = [];
					equippedSetItemIds.Add(set.SetId, itemIds);
				}

				itemIds.Add(item.Item.ItemId);
			}

			foreach (var (setId, itemIds) in equippedSetItemIds)
			{
				var set = itemSets.GetItemSetTemplate(setId);
				if (set == null)
					continue;

				var equippedPartCount = itemIds.Count;
				foreach (var partBonus in set.PartBonuses.Where(partBonus => partBonus.Count <= equippedPartCount))
				foreach (var modifier in partBonus.Modifiers)
					yield return modifier;

				if (set.FullBonus != null && equippedPartCount == set.FullBonus.Count)
				{
					foreach (var modifier in set.FullBonus.Modifiers)
						yield return modifier;
				}
			}
		}

		private static bool IsApplicableFusionWeaponModifier(ItemStatModifier modifier)
		{
			return modifier.Name is not ("ATTACK_SPEED" or "PVP_ATTACK_RATIO" or "BOOST_CASTING_TIME");
		}

		private static int CalculateStat(
			string statName,
			float baseValue,
			IReadOnlyList<ItemStatModifier> modifiers,
			bool reverse = false,
			float baseRate = 1f)
		{
			var value = new MutableStat(baseValue, reverse) { BaseRate = baseRate };
			foreach (var modifier in modifiers
				.Where(modifier => string.Equals(modifier.Name, statName, StringComparison.Ordinal))
				.OrderBy(modifier => modifier.Priority))
			{
				value.Apply(modifier);
			}

			return value.Current;
		}

		private static bool IsRightHandSlot(long slot)
		{
			return (slot & (MainHand | MainOffHand)) != 0;
		}

		private static bool IsLeftHandSlot(long slot)
		{
			return (slot & (SubHand | SubOffHand)) != 0;
		}

		private static bool IsTwoHandedSlot(long slot)
		{
			return (slot & (MainHand | SubHand)) == (MainHand | SubHand)
				|| (slot & (MainOffHand | SubOffHand)) == (MainOffHand | SubOffHand);
		}

		private static bool IsMainOrSubSlot(long slot)
		{
			return (slot & (MainHand | SubHand)) != 0;
		}

		private static bool IsAlternateWeaponSlot(long slot)
		{
			return (slot & (MainOffHand | SubOffHand)) != 0;
		}

		private static int GetChargeLevel(int charge)
		{
			if (charge <= 0)
				return 0;
			return charge > ChargeLevel1 ? 2 : 1;
		}

		private sealed record EquippedItem(InventoryItem Item, ItemTemplateSummary Template);

		private sealed class MutableStat
		{
			private readonly bool _reverse;
			private float _base;
			private float _bonus;

			public MutableStat(float baseValue, bool reverse)
			{
				_base = baseValue;
				_reverse = reverse;
			}

			public float BaseRate { get; init; } = 1f;

			public int Current => (int)(_base * BaseRate + _bonus);

			public void Apply(ItemStatModifier modifier)
			{
				switch (modifier.Operation)
				{
					case "rate":
						if (modifier.Bonus)
							AddToBonus(_base * modifier.Value / 100f);
						else
							_base *= CalculatePercent(modifier.Value);
						break;
					case "set":
					case "abs":
						if (modifier.Bonus)
							_bonus = modifier.Value;
						else
							_base = modifier.Value;
						break;
					case "sub":
						Add(modifier, -modifier.Value);
						break;
					default:
						Add(modifier, modifier.Value);
						break;
				}
			}

			private void Add(ItemStatModifier modifier, int value)
			{
				if (modifier.Bonus)
					AddToBonus(value);
				else
					AddToBase(value);
			}

			private void AddToBase(float value)
			{
				if (_reverse)
					_base = Math.Max(0, _base - value);
				else
					_base += value;
			}

			private void AddToBonus(float value)
			{
				if (_reverse)
					_bonus -= value;
				else
					_bonus += value;
			}

			private float CalculatePercent(int delta)
			{
				if (!_reverse)
					return (100 + delta) / 100f;

				var percent = (100 - delta) / 100f;
				return percent < 0 ? 0 : percent;
			}
		}
	}

	private sealed record PlayerCalculatedStats(
		int Power,
		int Health,
		int Accuracy,
		int Agility,
		int Knowledge,
		int Will,
		int MaxHp,
		int MaxMp,
		int Evasion,
		int Parry,
		int Block,
		int MainHandPhysicalAccuracy,
		int MagicalAccuracy,
		int PhysicalCriticalResist,
		int MagicalCriticalResist,
		int MaxDp = 4000,
		int FlyTime = BaseFlyTime,
		int MainHandPhysicalAttack = 18,
		int OffHandPhysicalAttack = 0,
		int PhysicalDefense = 0,
		int MainHandMagicalAttack = 0,
		int OffHandMagicalAttack = 0,
		int MagicalDefense = 0,
		int MagicalResist = 0,
		int AttackRange = 1500,
		int AttackSpeed = 1500,
		int MainHandPhysicalCritical = 2,
		int OffHandPhysicalCritical = 0,
		int OffHandPhysicalAccuracy = 0,
		int MagicalCritical = 50,
		int CastingSpeed = 1000,
		int Concentration = 0,
		int MagicalBoost = 0,
		int MagicalSuppression = 0,
		int HealBoost = 0,
		int PhysicalCriticalDamageReduce = 0,
		int MagicalCriticalDamageReduce = 0)
	{
		public static PlayerCalculatedStats Create(PlayerClassStats classStats, int level)
		{
			// Java parity: model/stats/calc/PlayerStatCalculator and PlayerClass.PlayerStatsTemplate.
			return new PlayerCalculatedStats(
				classStats.Power,
				classStats.Health,
				classStats.Accuracy,
				classStats.Agility,
				classStats.Knowledge,
				classStats.Will,
				CalculateMaxHp(classStats, level),
				CalculateMaxMp(classStats, level),
				(int)(62 + 12.4f * level),
				(int)(62 + 12.4f * level),
				(int)(62 + 12.4f * level),
				190 + 8 * level,
				(int)(14.26f * level),
				level > 50 ? 6 * (level - 50) : 0,
				classStats.MagicalCriticalResist);
		}

		private static int CalculateMaxHp(PlayerClassStats classStats, int level)
		{
			var baseHp = classStats.HealthMultiplier / 2;
			var mod1 = 0.1075f * classStats.HealthMultiplier;
			var mod2 = 0.002875f * classStats.HealthMultiplier;
			return (int)(baseHp + level * mod1 + level * level * mod2);
		}

		private static int CalculateMaxMp(PlayerClassStats classStats, int level)
		{
			var baseMp = classStats.WillMultiplier * 0.35f;
			var mod1 = level * baseMp / 2f;
			var mod2 = level * level * classStats.WillMultiplier * 0.125f / 10000;
			return (int)(baseMp + mod1 + mod2);
		}
	}

	private sealed record PlayerClassStats(
		int ClassId,
		int Power,
		int Health,
		int Agility,
		int Accuracy,
		int Knowledge,
		int Will,
		int HealthMultiplier,
		int WillMultiplier,
		int MagicalCriticalResist)
	{
		public static PlayerClassStats Get(string playerClass)
		{
			// Java parity: model/PlayerClass enum ids and base stat constructor values.
			return playerClass.ToUpperInvariant() switch
			{
				"GLADIATOR" => new(1, 115, 115, 100, 100, 90, 90, 440, 400, 0),
				"TEMPLAR" => new(2, 115, 100, 100, 100, 90, 105, 460, 400, 0),
				"SCOUT" => new(3, 100, 100, 110, 110, 90, 90, 360, 400, 0),
				"ASSASSIN" => new(4, 110, 100, 110, 110, 90, 90, 360, 400, 0),
				"RANGER" => new(5, 100, 100, 115, 115, 90, 90, 280, 400, 0),
				"MAGE" => new(6, 90, 90, 95, 95, 115, 115, 260, 600, 0),
				"SORCERER" => new(7, 90, 90, 100, 100, 120, 110, 260, 600, 50),
				"SPIRIT_MASTER" => new(8, 90, 90, 100, 100, 115, 115, 280, 600, 50),
				"PRIEST" => new(9, 95, 95, 100, 100, 100, 100, 360, 600, 0),
				"CLERIC" => new(10, 105, 110, 90, 90, 105, 110, 320, 600, 50),
				"CHANTER" => new(11, 110, 105, 90, 90, 105, 110, 360, 600, 0),
				"ENGINEER" => new(12, 100, 100, 110, 110, 90, 90, 360, 400, 0),
				"RIDER" => new(13, 100, 100, 100, 100, 105, 105, 420, 480, 0),
				"GUNNER" => new(14, 100, 105, 105, 100, 100, 100, 360, 400, 0),
				"ARTIST" => new(15, 95, 95, 100, 100, 100, 105, 320, 600, 0),
				"BARD" => new(16, 90, 100, 100, 100, 110, 110, 320, 520, 50),
				_ => new(0, 110, 110, 100, 100, 90, 90, 400, 400, 0),
			};
		}
	}
}
