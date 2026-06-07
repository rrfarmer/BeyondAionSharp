using System.Text.Json;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Tests;

/// <summary>
/// Phase A2 of the Port Fidelity &amp; Remediation Plan: the C# half of the golden pipeline.
///
/// Reads the SHARED packet fixtures produced by the Java harness
/// (game-server GoldenPacketFixtureGeneratorTest -> parity-artifacts/golden/packets/*.json)
/// and asserts the C# packet writers emit byte-for-byte identical payloads. The Java bytes
/// are the single source of truth; this is "Java as the oracle" with no live client.
///
/// To add a packet: capture it in the Java generator, then add a reconstruction case below.
/// </summary>
public sealed class GoldenPacketFixtureTests
{
	[Theory]
	[InlineData("SM_GROUP_DATA_EXCHANGE.json")]
	[InlineData("SM_GF_WEBSHOP_TOKEN_RESPONSE.json")]
	public void CsharpPayloadMatchesJavaGoldenFixture(string fixtureFile)
	{
		var fixture = LoadFixture(fixtureFile);
		var packetName = fixture.RootElement.GetProperty("packet").GetString()!;

		foreach (var caseElement in fixture.RootElement.GetProperty("cases").EnumerateArray())
		{
			var caseName = caseElement.GetProperty("name").GetString()!;
			var expectedHex = caseElement.GetProperty("payloadHex").GetString()!;
			var inputs = caseElement.GetProperty("inputs");

			var packet = Reconstruct(packetName, inputs);
			var actual = SerializeUnencryptedPayload(packet);
			var actualHex = Convert.ToHexString(actual);

			Assert.True(expectedHex == actualHex,
				$"{packetName}/{caseName}: C# payload diverged from Java golden.\n" +
				$"  Java : {expectedHex}\n  C#   : {actualHex}");
		}
	}

	private static GameServerPacket Reconstruct(string packetName, JsonElement inputs) => packetName switch
	{
		"SM_GROUP_DATA_EXCHANGE" => BuildGroupDataExchange(inputs),
		"SM_GF_WEBSHOP_TOKEN_RESPONSE" => new SmGfWebshopTokenResponse(inputs.GetProperty("token").GetString()!),
		_ => throw new NotSupportedException($"No C# reconstruction registered for {packetName}"),
	};

	private static GameServerPacket BuildGroupDataExchange(JsonElement inputs)
	{
		var data = inputs.GetProperty("byteData").EnumerateArray()
			.Select(e => (byte)e.GetInt32()).ToArray();
		if (inputs.TryGetProperty("action", out var action))
		{
			var unk2 = (byte)inputs.GetProperty("unk2").GetInt32();
			return SmGroupDataExchange.GroupBroadcast(data, (byte)action.GetInt32(), unk2);
		}
		return SmGroupDataExchange.NearbyBroadcast(data);
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		// Mirrors existing SmGroupDataExchangeTests: deterministic key, strip the 7-byte frame header.
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		return packet.SerializeFrame(crypt)[7..];
	}

	private static JsonDocument LoadFixture(string fileName)
	{
		var path = Path.Combine(FixtureRoot(), fileName);
		Assert.True(File.Exists(path), $"Missing Java golden fixture: {path}. " +
			"Regenerate with: mvn -pl game-server -am test -Dtest=GoldenPacketFixtureGeneratorTest " +
			"-Dmaven.test.skip=false -Dsurefire.failIfNoSpecifiedTests=false");
		return JsonDocument.Parse(File.ReadAllText(path));
	}

	private static string FixtureRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			var candidate = Path.Combine(dir.FullName, "parity-artifacts", "golden", "packets");
			if (Directory.Exists(candidate))
				return candidate;
			dir = dir.Parent;
		}
		throw new DirectoryNotFoundException(
			"Could not locate parity-artifacts/golden/packets above " + AppContext.BaseDirectory);
	}
}
