using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmWeather : GameServerPacket
{
	public const int PacketOpCode = 67;

	private readonly IReadOnlyList<int> _weatherCodes;

	public SmWeather(IReadOnlyList<int> weatherCodes)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_WEATHER stores WeatherEntry[]
		// and writeImpl writes entry.getCode() for each array element.
		_weatherCodes = weatherCodes ?? throw new ArgumentNullException(nameof(weatherCodes));
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_WEATHER.writeImpl writes unknown 0, array length,
		// then each WeatherEntry.getCode() through writeC low-byte semantics.
		buffer.WriteC(0x00);
		buffer.WriteC(_weatherCodes.Count);

		foreach (var weatherCode in _weatherCodes)
			buffer.WriteC(weatherCode);
	}
}
