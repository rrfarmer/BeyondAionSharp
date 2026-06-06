using System.Net;
using Aion.GameServer.Model;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Configuration;

namespace Aion.GameServer.Tests;

public sealed class SmVersionCheckTests
{
	[Fact]
	public void WritePayload_IncompatibleClientVersionWritesJavaAnswerIdOnly()
	{
		Assert.Equal(207, SmVersionCheck.InternalVersion);

		var payload = SerializeUnencryptedPayload(new SmVersionCheck(206, EventTheme.None));

		Assert.Equal([0x01], payload);
	}

	[Fact]
	public void WritePayload_CompatibleClientVersionWritesJavaSuccessPayload()
	{
		var packet = new SmVersionCheck(
			SmVersionCheck.InternalVersion,
			EventTheme.Christmas,
			new GameServerOptions
			{
				Network = new GameServerNetworkOptions { GameServerId = 3 },
				Core = new GameServerCoreOptions
				{
					ServerCountryCode = 45,
					CharacterLimitCount = 8,
					CharacterFactionLimitationMode = 2,
					CharacterCreationMode = 1,
					MinimumSkillCastIntervalMillis = 350,
					ChatServerMinLevel = 10,
					CharacterReentryTimeSeconds = 20,
					ItemWrapLimit = 12,
					TimeZoneId = "UTC"
				}
			},
			clock: () => DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
			serverStartTime: DateTimeOffset.FromUnixTimeSeconds(1_690_000_000));

		var payload = SerializeUnencryptedPayload(packet);

		Assert.Equal(0, payload[0]); // answerID
		Assert.Equal(3, payload[1]); // serverId
		Assert.Equal(150602, ReadInt(payload, 2));
		Assert.Equal(150326, ReadInt(payload, 6));
		Assert.Equal(0, ReadInt(payload, 10));
		Assert.Equal(150317, ReadInt(payload, 14));
		Assert.Equal(1_690_000_000, ReadInt(payload, 18));
		Assert.Equal(0, payload[22]);
		Assert.Equal(45, payload[23]);
		Assert.Equal(0, payload[24]);
		Assert.Equal((8 * 0x10) | (2 * 4) | 1, payload[25]);
		Assert.Equal(1_700_000_000, ReadInt(payload, 26));
		Assert.Equal(350, ReadShort(payload, 30));
		Assert.Equal(ChristmasThemeId, ReadInt(payload, 43));
		Assert.Equal(0, ReadInt(payload, 48)); // UTC standard offset written as -offset
		Assert.Equal(0, ReadInt(payload, 72)); // UTC daylight savings bias
		Assert.Equal(12, ReadInt(payload, 90));
		Assert.Equal(0, ReadShort(payload, 143)); // ChatServersCount when chat server is not authenticated
	}

	[Fact]
	public void WritePayload_CompatibleClientVersionWritesAuthenticatedChatEndpoint()
	{
		var packet = new SmVersionCheck(
			SmVersionCheck.InternalVersion,
			EventTheme.None,
			new GameServerOptions { Core = new GameServerCoreOptions { TimeZoneId = "UTC" } },
			clock: () => DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
			serverStartTime: DateTimeOffset.FromUnixTimeSeconds(1_690_000_000),
			publicChatEndPoint: new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10241));

		var payload = SerializeUnencryptedPayload(packet);

		Assert.Equal(1, ReadShort(payload, 143)); // ChatServersCount
		Assert.Equal(0, payload[145]); // Java writes an extra C(0) before the public IP bytes.
		Assert.Equal([127, 0, 0, 1], payload[146..150]);
		Assert.Equal(10241, ReadShort(payload, 150));
	}

	private const int ChristmasThemeId = 1;

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static int ReadInt(byte[] payload, int offset)
	{
		return BitConverter.ToInt32(payload, offset);
	}

	private static int ReadShort(byte[] payload, int offset)
	{
		return BitConverter.ToUInt16(payload, offset);
	}
}
