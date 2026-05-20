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

	public SmStatsInfo(Player player, PlayerExperienceTable? experienceTable, int gameMinutes)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_STATS_INFO(Player).
		_player = player;
		_experienceTable = experienceTable;
		_gameMinutes = gameMinutes;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_STATS_INFO.writeImpl. This is the no-equipment/no-effect baseline until stat functions are ported.
		var context = PlayerStatsContext.Create(_player, _experienceTable);

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
		public static PlayerStatsContext Create(Player player, PlayerExperienceTable? experienceTable)
		{
			// Java parity: PlayerCommonData.setExp/updateMaxRepose plus PlayerClass.createStatsTemplate.
			var classStats = PlayerClassStats.Get(player.PlayerClass);
			var level = Math.Max(1, experienceTable?.GetLevelForExp(player.Exp) ?? 1);
			var expStart = GetStartExp(experienceTable, level);
			var expNeed = GetExpNeed(experienceTable, level);
			var maxRepose = level >= 10 ? (long)(expNeed * 0.25f) : 0;
			var currentRepose = Math.Clamp(player.ReposeEnergy, 0, maxRepose);
			var baseStats = PlayerCalculatedStats.Create(classStats, level);
			var lifeStats = player.LifeStats;
			return new PlayerStatsContext(
				classStats,
				baseStats,
				baseStats,
				level,
				expNeed,
				Math.Max(0, player.Exp - expStart),
				currentRepose,
				maxRepose,
				lifeStats?.GetCurrentHp(baseStats.MaxHp) ?? baseStats.MaxHp,
				lifeStats?.GetCurrentMp(baseStats.MaxMp) ?? baseStats.MaxMp,
				lifeStats?.GetCurrentFp() ?? baseStats.FlyTime);
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
		int MagicalCriticalResist)
	{
		public int MaxDp => 4000;
		public int FlyTime => BaseFlyTime;
		public int MainHandPhysicalAttack => 18;
		public int OffHandPhysicalAttack => 0;
		public int PhysicalDefense => 0;
		public int MainHandMagicalAttack => 0;
		public int OffHandMagicalAttack => 0;
		public int MagicalDefense => 0;
		public int MagicalResist => 0;
		public int AttackRange => 1500;
		public int AttackSpeed => 1500;
		public int MainHandPhysicalCritical => 2;
		public int OffHandPhysicalCritical => 0;
		public int OffHandPhysicalAccuracy => 0;
		public int MagicalCritical => 50;
		public int CastingSpeed => 1000;
		public int Concentration => 0;
		public int MagicalBoost => 0;
		public int MagicalSuppression => 0;
		public int HealBoost => 0;
		public int PhysicalCriticalDamageReduce => 0;
		public int MagicalCriticalDamageReduce => 0;

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
