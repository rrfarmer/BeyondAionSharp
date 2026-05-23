using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcCustomDropService
{
	private static readonly CustomNpcDropTable EmptyCustomNpcDrops = new([]);
	private readonly GameServerRuntimeContext? _runtimeContext;
	private readonly CustomNpcDropTable? _customNpcDrops;
	private readonly Func<float> _chanceRoll;
	private readonly Func<int, int, int> _countRoll;
	private readonly Func<int, int> _indexRoll;

	public WorldNpcCustomDropService(GameServerRuntimeContext runtimeContext)
		: this(runtimeContext, null, null, null, null)
	{
	}

	public WorldNpcCustomDropService(
		CustomNpcDropTable customNpcDrops,
		Func<float>? chanceRoll = null,
		Func<int, int, int>? countRoll = null,
		Func<int, int>? indexRoll = null)
		: this(null, customNpcDrops, chanceRoll, countRoll, indexRoll)
	{
	}

	private WorldNpcCustomDropService(
		GameServerRuntimeContext? runtimeContext,
		CustomNpcDropTable? customNpcDrops,
		Func<float>? chanceRoll,
		Func<int, int, int>? countRoll,
		Func<int, int>? indexRoll)
	{
		_runtimeContext = runtimeContext;
		_customNpcDrops = customNpcDrops;
		_chanceRoll = chanceRoll ?? (() => Random.Shared.NextSingle() * 100f);
		_countRoll = countRoll ?? ((minInclusive, maxInclusive) => minInclusive == maxInclusive ? minInclusive : Random.Shared.Next(minInclusive, maxInclusive + 1));
		_indexRoll = indexRoll ?? (exclusiveMax => exclusiveMax <= 1 ? 0 : Random.Shared.Next(exclusiveMax));
	}

	public WorldNpcCustomDropResult CreateDrops(
		int npcObjectId,
		int npcTemplateId,
		WorldNpcDropModifiers dropModifiers,
		IReadOnlyList<Player>? groupMembers = null,
		int startIndex = 1)
	{
		// Java parity: model/drop/NpcDrop.dropCalculator delegates each matching DropGroup into DropGroup.tryAddDropItems.
		var npcDrop = GetCustomNpcDrops().GetNpcDrop(npcTemplateId);
		if (npcDrop == null)
			return new WorldNpcCustomDropResult(Array.Empty<WorldNpcDropItem>(), startIndex);

		var result = new List<WorldNpcDropItem>();
		var index = startIndex;
		foreach (var group in npcDrop.Groups)
		{
			if (!IsRaceMatched(group.Race, dropModifiers.DropRace))
				continue;

			index = TryAddDropItems(npcObjectId, result, index, group, dropModifiers, groupMembers);
		}

		return new WorldNpcCustomDropResult(result, index);
	}

	private CustomNpcDropTable GetCustomNpcDrops()
	{
		return _customNpcDrops ?? _runtimeContext?.DataManager?.StaticData.CustomNpcDrops ?? EmptyCustomNpcDrops;
	}

	private int TryAddDropItems(
		int npcObjectId,
		List<WorldNpcDropItem> result,
		int index,
		CustomDropGroupSummary group,
		WorldNpcDropModifiers dropModifiers,
		IReadOnlyList<Player>? groupMembers)
	{
		// Java parity: model/drop/DropGroup.tryAddDropItems selects up to max_items nearest successful drops per roll.
		var remainingDrops = group.Drops.ToList();
		for (var i = 0; i < group.MaxItems && remainingDrops.Count > 0; i++)
		{
			var chance = _chanceRoll();
			var nearestChanceDiff = float.MaxValue;
			var nearestDropsOfSameChance = new List<CustomDropSummary>();
			foreach (var drop in remainingDrops)
			{
				var finalChance = dropModifiers.CalculateDropChance(drop.Chance, group.UseLevelBasedChanceReduction);
				if (chance >= finalChance)
					continue;

				var chanceDiff = finalChance - chance;
				if (nearestDropsOfSameChance.Count == 0 || chanceDiff <= nearestChanceDiff)
				{
					if (chanceDiff < nearestChanceDiff)
					{
						nearestDropsOfSameChance.Clear();
						nearestChanceDiff = chanceDiff;
					}

					nearestDropsOfSameChance.Add(drop);
				}
			}

			var selectedDrop = GetRandom(nearestDropsOfSameChance);
			if (selectedDrop == null)
				continue;

			index = AddDropItem(npcObjectId, result, index, selectedDrop, groupMembers);
			remainingDrops.Remove(selectedDrop);
		}

		return index;
	}

	private int AddDropItem(
		int npcObjectId,
		List<WorldNpcDropItem> result,
		int index,
		CustomDropSummary drop,
		IReadOnlyList<Player>? groupMembers)
	{
		// Java parity: DropGroup.addDropItem creates one distributed drop per member for each_member groups.
		if (drop.EachMember && groupMembers is { Count: > 0 })
		{
			foreach (var player in groupMembers)
			{
				result.Add(CreateDropItem(npcObjectId, index++, drop, new HashSet<int> { player.ObjectId }, isDistributeItem: true));
			}

			return index;
		}

		result.Add(CreateDropItem(npcObjectId, index++, drop, null, isDistributeItem: false));
		return index;
	}

	private WorldNpcDropItem CreateDropItem(
		int npcObjectId,
		int index,
		CustomDropSummary drop,
		IReadOnlySet<int>? playerObjectIds,
		bool isDistributeItem)
	{
		return new WorldNpcDropItem(
			index,
			drop.ItemId,
			_countRoll(drop.MinAmount, drop.MaxAmount),
			playerObjectIds,
			NpcObjectId: npcObjectId,
			IsDistributeItem: isDistributeItem);
	}

	private CustomDropSummary? GetRandom(IReadOnlyList<CustomDropSummary> drops)
	{
		return drops.Count == 0 ? null : drops.Count == 1 ? drops[0] : drops[_indexRoll(drops.Count)];
	}

	private static bool IsRaceMatched(string groupRace, string dropRace)
	{
		return string.Equals(NormalizeRace(groupRace), "PC_ALL", StringComparison.Ordinal)
			|| string.Equals(NormalizeRace(groupRace), NormalizeRace(dropRace), StringComparison.Ordinal);
	}

	private static string NormalizeRace(string race)
	{
		return string.Equals(race, "ASMODIAN", StringComparison.OrdinalIgnoreCase)
			? "ASMODIANS"
			: race.ToUpperInvariant();
	}
}

public sealed record WorldNpcCustomDropResult(
	IReadOnlyList<WorldNpcDropItem> Drops,
	int NextIndex);

public sealed record WorldNpcDropModifiers(
	string DropRace,
	float BoostDropRate = 1f,
	float? ReductionDropRate = null,
	bool IsDropNpcChest = false,
	int? MaxDropsPerGroup = null,
	IReadOnlySet<string>? InsideZones = null)
{
	public float CalculateDropChance(float chance, bool allowReductionDropRate)
	{
		// Java parity: model/drop/DropModifiers.calculateDropChance.
		if (allowReductionDropRate && ReductionDropRate != null)
			chance *= ReductionDropRate.Value;
		return chance * BoostDropRate;
	}
}
