using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmObjectUseUpdate : GameServerPacket
{
	public const int PacketOpCode = 264;

	private readonly int _usingPlayerId;
	private readonly int _ownerPlayerId;
	private readonly int _useCount;
	private readonly RegisteredHouseObjectSummary _houseObject;

	public SmObjectUseUpdate(
		int usingPlayerId,
		int ownerPlayerId,
		int useCount,
		RegisteredHouseObjectSummary houseObject)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_OBJECT_USE_UPDATE.
		_usingPlayerId = usingPlayerId;
		_ownerPlayerId = ownerPlayerId;
		_useCount = useCount;
		_houseObject = houseObject;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteC(_houseObject.TypeId);
		if (_houseObject.TypeId is 2 or 3)
		{
			buffer.WriteD(_usingPlayerId);
			buffer.WriteC(1);
			buffer.WriteD(_houseObject.ObjectId);
			return;
		}

		if (_houseObject.TypeId == 1)
		{
			buffer.WriteD(_usingPlayerId);
			buffer.WriteD(_ownerPlayerId);
			buffer.WriteD(_houseObject.ObjectId);
			buffer.WriteD(_useCount);
			buffer.WriteC(GetUseActionCheckType(_houseObject));
		}
	}

	private static int GetUseActionCheckType(RegisteredHouseObjectSummary houseObject)
	{
		// Java parity: UseableItemObject.getObjectTemplate().getAction().getCheckType tail.
		return houseObject.UsageData is { Length: >= 5 } ? houseObject.UsageData[4] : 0;
	}
}
