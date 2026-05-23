using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmShowBrand : GameClientPacket
{
	public CmShowBrand(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Action { get; private set; }

	public int BrandId { get; private set; }

	public int TargetObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_SHOW_BRAND.readImpl.
		Action = buffer.ReadD();
		BrandId = buffer.ReadD();
		TargetObjectId = buffer.ReadD();
	}
}
