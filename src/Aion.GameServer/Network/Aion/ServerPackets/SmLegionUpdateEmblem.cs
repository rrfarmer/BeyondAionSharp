using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLegionUpdateEmblem : GameServerPacket
{
	public const int PacketOpCode = 215;

	private readonly int _legionId;
	private readonly byte _emblemId;
	private readonly byte _emblemType;
	private readonly byte _colorA;
	private readonly byte _colorR;
	private readonly byte _colorG;
	private readonly byte _colorB;

	public SmLegionUpdateEmblem(
		int legionId,
		byte emblemId,
		byte emblemType,
		byte colorA,
		byte colorR,
		byte colorG,
		byte colorB)
		: base(PacketOpCode)
	{
		_legionId = legionId;
		_emblemId = emblemId;
		_emblemType = emblemType;
		_colorA = colorA;
		_colorR = colorR;
		_colorG = colorG;
		_colorB = colorB;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_LEGION_UPDATE_EMBLEM.writeImpl.
		buffer.WriteD(_legionId);
		buffer.WriteC(_emblemId);
		buffer.WriteC(_emblemType);
		buffer.WriteC(_colorA);
		buffer.WriteC(_colorR);
		buffer.WriteC(_colorG);
		buffer.WriteC(_colorB);
	}
}
