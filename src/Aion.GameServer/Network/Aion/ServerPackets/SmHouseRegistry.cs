using Aion.Commons.Network;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseRegistry : GameServerPacket
{
	public const int PacketOpCode = 116;
	public const byte RegisteredObjectsAction = 1;
	public const byte DecorationItemsAction = 2;
	private const byte UseItemTypeId = 1;

	private readonly byte _action;
	private readonly IReadOnlyList<RegisteredHouseObjectSummary> _objects;
	private readonly IReadOnlyList<int> _defaultPartIds;
	private readonly IReadOnlyList<RegisteredHouseDecorationSummary> _unusedDecorations;

	private SmHouseRegistry(
		byte action,
		IReadOnlyList<RegisteredHouseObjectSummary> objects,
		IReadOnlyList<int> defaultPartIds,
		IReadOnlyList<RegisteredHouseDecorationSummary> unusedDecorations)
		: base(PacketOpCode)
	{
		_action = action;
		_objects = objects;
		_defaultPartIds = defaultPartIds;
		_unusedDecorations = unusedDecorations;
	}

	public static SmHouseRegistry CreateRegisteredObjects(IReadOnlyList<RegisteredHouseObjectSummary>? objects = null)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_REGISTRY action 1.
		return new SmHouseRegistry(
			RegisteredObjectsAction,
			objects ?? Array.Empty<RegisteredHouseObjectSummary>(),
			Array.Empty<int>(),
			Array.Empty<RegisteredHouseDecorationSummary>());
	}

	public static SmHouseRegistry CreateRegisteredObjects(HouseRegistrySummary registry)
	{
		// Java parity: model/house/HouseRegistry.getNotSpawnedObjects.
		return CreateRegisteredObjects(registry.NotSpawnedObjects);
	}

	public static SmHouseRegistry CreateRegisteredObjects(
		HouseRegistrySummary registry,
		IReadOnlyDictionary<int, long> cooldowns)
	{
		// Java parity: SM_HOUSE_REGISTRY writes active player house-object cooldown seconds.
		return CreateRegisteredObjects(registry.GetNotSpawnedObjects(cooldowns));
	}

	public static SmHouseRegistry CreateDecorationItems(
		HousingTemplateTable? housingTemplates,
		int buildingId,
		IReadOnlyList<RegisteredHouseDecorationSummary>? unusedDecorations = null)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_REGISTRY action 2.
		return new SmHouseRegistry(
			DecorationItemsAction,
			Array.Empty<RegisteredHouseObjectSummary>(),
			housingTemplates?.GetDefaultPartIds(buildingId) ?? Array.Empty<int>(),
			unusedDecorations ?? Array.Empty<RegisteredHouseDecorationSummary>());
	}

	public static SmHouseRegistry CreateDecorationItems(
		HousingTemplateTable? housingTemplates,
		int buildingId,
		HouseRegistrySummary registry)
	{
		// Java parity: model/house/HouseRegistry.getUnusedDecors.
		return CreateDecorationItems(housingTemplates, buildingId, registry.UnusedDecorations);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_action);
		if (_action == RegisteredObjectsAction)
		{
			WriteRegisteredObjects(buffer);
			return;
		}

		if (_action == DecorationItemsAction)
			WriteDecorationItems(buffer);
	}

	private void WriteRegisteredObjects(PacketBuffer buffer)
	{
		// Java parity: SM_HOUSE_REGISTRY action 1 getNotSpawnedObjects rows.
		buffer.WriteH(_objects.Count);
		foreach (var obj in _objects)
		{
			buffer.WriteD(obj.ObjectId);
			buffer.WriteD(obj.TemplateId);
			buffer.WriteD(obj.CooldownSeconds);
			buffer.WriteD(obj.ExpirationSeconds);
			HouseObjectPacketWriter.WriteDyeInfo(buffer, obj.Color);
			buffer.WriteD(0);
			buffer.WriteC(obj.TypeId);
			if (obj.TypeId == UseItemTypeId && obj.UsageData is { Length: > 0 })
				buffer.WriteB(obj.UsageData);
		}
	}

	private void WriteDecorationItems(PacketBuffer buffer)
	{
		// Java parity: SM_HOUSE_REGISTRY action 2 default Building parts followed by HouseRegistry.getUnusedDecors.
		buffer.WriteH(_defaultPartIds.Count + _unusedDecorations.Count);
		foreach (var defaultPartId in _defaultPartIds)
		{
			buffer.WriteD(0);
			buffer.WriteD(defaultPartId);
		}

		foreach (var decoration in _unusedDecorations)
		{
			buffer.WriteD(decoration.ObjectId);
			buffer.WriteD(decoration.TemplateId);
		}
	}

}
