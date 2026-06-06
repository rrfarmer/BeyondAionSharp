using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionSendEmblem : GameServerPacket
{
	public const int PacketOpCode = 213;

	private readonly int _legionId;
	private readonly byte _emblemId;
	private readonly byte _emblemType;
	private readonly int _emblemDataSize;
	private readonly byte _colorA;
	private readonly byte _colorR;
	private readonly byte _colorG;
	private readonly byte _colorB;
	private readonly string _legionName;

	public SmLegionSendEmblem(
		int legionId,
		byte emblemId,
		byte emblemType,
		int emblemDataSize,
		byte colorA,
		byte colorR,
		byte colorG,
		byte colorB,
		string legionName)
		: base(PacketOpCode)
	{
		_legionId = legionId;
		_emblemId = emblemId;
		_emblemType = emblemType;
		_emblemDataSize = Math.Max(0, emblemDataSize);
		_colorA = colorA;
		_colorR = colorR;
		_colorG = colorG;
		_colorB = colorB;
		_legionName = legionName;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_SEND_EMBLEM.writeImpl.
		buffer.WriteD(_legionId);
		buffer.WriteC(_emblemId);
		buffer.WriteC(_emblemType);
		buffer.WriteD(_emblemDataSize);
		buffer.WriteC(_colorA);
		buffer.WriteC(_colorR);
		buffer.WriteC(_colorG);
		buffer.WriteC(_colorB);
		buffer.WriteS(_legionName);
		buffer.WriteC(0x01);
	}
}
