using Aion.Commons.Network;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

public class SmWeatherPacketTests
{
	[Fact]
	public void SmWeather_WritesEmptyWeatherArrayLikeJava()
	{
		var payload = SerializeUnencryptedPayload(new SmWeather([]));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(SmWeather.PacketOpCode, 67);
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmWeather_WritesWeatherEntryCodesLikeJava()
	{
		var payload = SerializeUnencryptedPayload(new SmWeather([0, 7, 255]));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(3, (int)reader.ReadC());
		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(7, (int)reader.ReadC());
		Assert.Equal(255, (int)reader.ReadC());
		Assert.Equal(0, reader.Remaining);
	}

	[Fact]
	public void SmWeather_UsesWriteCLowByteSemanticsForCountAndCodes()
	{
		var weatherCodes = Enumerable.Range(0, 300).ToArray();
		var payload = SerializeUnencryptedPayload(new SmWeather(weatherCodes));
		using var reader = new PacketBuffer(payload);

		Assert.Equal(0, (int)reader.ReadC());
		Assert.Equal(44, (int)reader.ReadC());

		for (var i = 0; i < weatherCodes.Length; i++)
			Assert.Equal((byte)i, reader.ReadC());

		Assert.Equal(0, reader.Remaining);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
