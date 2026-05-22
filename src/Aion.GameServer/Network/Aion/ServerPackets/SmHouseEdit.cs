using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmHouseEdit : GameServerPacket
{
	public const int PacketOpCode = 82;

	private readonly int _action;
	private readonly int _storeId;
	private readonly int _ownerPlayerId;
	private readonly PlacedHouseObjectSummary? _placedObject;
	private readonly RegisteredHouseObjectSummary? _registryObject;
	private readonly RegisteredHouseDecorationSummary? _decoration;

	public SmHouseEdit(int action)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_HOUSE_EDIT simple mode actions.
		_action = action;
	}

	public SmHouseEdit(int action, int storeId, RegisteredHouseObjectSummary registryObject, int ownerPlayerId = 0)
		: this(action)
	{
		_storeId = storeId;
		_ownerPlayerId = ownerPlayerId;
		_registryObject = registryObject;
	}

	public SmHouseEdit(int action, int storeId, RegisteredHouseDecorationSummary decoration)
		: this(action)
	{
		_storeId = storeId;
		_decoration = decoration;
	}

	public SmHouseEdit(int action, int storeId, int itemObjectId)
		: this(action)
	{
		_storeId = storeId;
		_registryObject = new RegisteredHouseObjectSummary(itemObjectId, 0);
	}

	public SmHouseEdit(int action, PlacedHouseObjectSummary placedObject)
		: this(action)
	{
		_placedObject = placedObject;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		if (_action == 3 && (_registryObject != null || _decoration != null))
		{
			// Java parity: SM_HOUSE_EDIT action 3 adds a registered house item back to the edit inventory.
			buffer.WriteC(_action);
			buffer.WriteC(_storeId);
			buffer.WriteD(_registryObject?.ObjectId ?? _decoration!.ObjectId);
			buffer.WriteD(_registryObject?.TemplateId ?? _decoration!.TemplateId);
			buffer.WriteD(_registryObject?.ExpirationSeconds ?? 0);
			HouseObjectPacketWriter.WriteDyeInfo(buffer, _registryObject?.Color);
			buffer.WriteD(0);
			buffer.WriteC(_registryObject?.TypeId ?? 0);
			if (_registryObject != null && _registryObject.TypeId == 1 && _registryObject.UsageData is { Length: > 0 })
			{
				buffer.WriteD(_ownerPlayerId);
				buffer.WriteB(_registryObject.UsageData);
			}
			return;
		}

		if (_action == 4 && _registryObject != null)
		{
			// Java parity: SM_HOUSE_EDIT action 4 removes a house item from the edit inventory.
			buffer.WriteC(_action);
			buffer.WriteC(_storeId);
			buffer.WriteD(_registryObject.ObjectId);
			return;
		}

		if (_action == 5 && _placedObject != null)
		{
			// Java parity: SM_HOUSE_EDIT action 5 spawns or moves a house object.
			buffer.WriteC(_action);
			buffer.WriteD(_placedObject.AddressId);
			buffer.WriteD(_placedObject.OwnerPlayerId);
			buffer.WriteD(_placedObject.ObjectId);
			buffer.WriteD(_placedObject.TemplateId);
			buffer.WriteF(_placedObject.X);
			buffer.WriteF(_placedObject.Y);
			buffer.WriteF(_placedObject.Z);
			buffer.WriteH(_placedObject.Rotation);
			buffer.WriteD(_placedObject.CooldownSeconds);
			buffer.WriteD(_placedObject.ExpirationSeconds);
			HouseObjectPacketWriter.WriteDyeInfo(buffer, _placedObject.Color);
			buffer.WriteD(0);
			buffer.WriteC(_placedObject.TypeId);
			if (_placedObject.TypeId == 1 && _placedObject.UsageData is { Length: > 0 })
				buffer.WriteB(_placedObject.UsageData);
			return;
		}

		if (_action == 7 && _registryObject != null)
		{
			// Java parity: SM_HOUSE_EDIT action 7 despawns a house object.
			buffer.WriteC(_action);
			buffer.WriteD(_registryObject.ObjectId);
			return;
		}

		buffer.WriteC(_action);
	}
}
