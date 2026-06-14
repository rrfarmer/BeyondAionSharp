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
	// NOTE: SM_GROUP_DATA_EXCHANGE is intentionally omitted: the faithful src packet
	// (SM_GROUP_DATA_EXCHANGE : AionServerPacket) serializes via AionServerPacket.Write
	// (connection-bound), not the GameServerPacket.SerializeFrame(GameCrypt) path this
	// golden harness uses. Re-enabling it requires unifying the dual serialization paths
	// in src (out of test-only scope).
	[InlineData("SM_GF_WEBSHOP_TOKEN_RESPONSE.json")]
	[InlineData("SM_QUIT_RESPONSE.json")]
	[InlineData("SM_DELETE_ITEM.json")]
	[InlineData("SM_DELETE_WAREHOUSE_ITEM.json")]
	[InlineData("SM_DELETE_HOUSE_OBJECT.json")]
	[InlineData("SM_DELETE_HOUSE.json")]
	[InlineData("SM_RECIPE_DELETE.json")]
	[InlineData("SM_CRAFT_ANIMATION.json")]
	[InlineData("SM_BLOCK_RESPONSE.json")]
	[InlineData("SM_FRIEND_RESPONSE.json")]
	[InlineData("SM_CLOSE_QUESTION_WINDOW.json")]
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
		"SM_GF_WEBSHOP_TOKEN_RESPONSE" => new SmGfWebshopTokenResponse(inputs.GetProperty("token").GetString()!),
		"SM_QUIT_RESPONSE" => new SmQuitResponse(inputs.GetProperty("editMode").GetBoolean()),
		"SM_DELETE_ITEM" => new SmDeleteItem(inputs.GetProperty("itemObjectId").GetInt32(), inputs.GetProperty("deleteType").GetInt32()),
		"SM_DELETE_WAREHOUSE_ITEM" => new SmDeleteWarehouseItem(inputs.GetProperty("warehouseType").GetInt32(), inputs.GetProperty("itemObjectId").GetInt32(), inputs.GetProperty("deleteType").GetInt32()),
		"SM_DELETE_HOUSE_OBJECT" => new SmDeleteHouseObject(inputs.GetProperty("itemObjectId").GetInt32()),
		"SM_DELETE_HOUSE" => new SmDeleteHouse(inputs.GetProperty("addressId").GetInt32()),
		"SM_RECIPE_DELETE" => new SmRecipeDelete(inputs.GetProperty("recipeId").GetInt32()),
		"SM_CRAFT_ANIMATION" => new SmCraftAnimation(inputs.GetProperty("playerObjectId").GetInt32(), inputs.GetProperty("targetObjectId").GetInt32(), inputs.GetProperty("skillId").GetInt32(), inputs.GetProperty("action").GetInt32()),
		"SM_BLOCK_RESPONSE" => new SmBlockResponse((byte)inputs.GetProperty("code").GetInt32(), inputs.GetProperty("playerName").GetString()!),
		"SM_FRIEND_RESPONSE" => new SmFriendResponse((byte)inputs.GetProperty("code").GetInt32(), inputs.GetProperty("playerName").GetString()!),
		"SM_CLOSE_QUESTION_WINDOW" => ReconstructCloseQuestionWindow(inputs),
		_ => throw new NotSupportedException($"No C# reconstruction registered for {packetName}"),
	};

	private static SmCloseQuestionWindow ReconstructCloseQuestionWindow(JsonElement inputs)
	{
		var messageId = inputs.GetProperty("messageId").GetInt32();
		var parameters = inputs.GetProperty("params").EnumerateArray().Select(p => p.GetString()!).ToArray();
		return messageId switch
		{
			0 => SmCloseQuestionWindow.CloseQuestionWindow(),
			1300134 => SmCloseQuestionWindow.DuelRequesterWithdrawRequest(parameters[0]),
			1300097 => SmCloseQuestionWindow.DuelHeRejectDuel(parameters[0]),
			_ => throw new NotSupportedException($"No SM_CLOSE_QUESTION_WINDOW factory for messageId {messageId}"),
		};
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
