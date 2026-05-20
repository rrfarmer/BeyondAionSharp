using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmPrices : GameServerPacket
{
	public const int PacketOpCode = 252;

	private readonly int _globalPrices;
	private readonly int _globalPricesModifier;
	private readonly int _taxes;

	public SmPrices(int globalPrices = 100, int globalPricesModifier = 100, int taxes = 100)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_PRICES using services/trade/PricesService defaults.
		_globalPrices = globalPrices;
		_globalPricesModifier = globalPricesModifier;
		_taxes = taxes;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_PRICES.writeImpl.
		buffer.WriteC(_globalPrices);
		buffer.WriteC(_globalPricesModifier);
		buffer.WriteC(_taxes);
	}
}
