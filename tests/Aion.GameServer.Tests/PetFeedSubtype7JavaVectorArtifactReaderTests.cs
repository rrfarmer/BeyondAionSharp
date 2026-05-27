using System.Buffers.Binary;
using System.Text.Json;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PetFeedSubtype7JavaVectorArtifactReaderTests(ITestOutputHelper output)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Fact]
	public void ParsePetFeedSubtype7Artifact_ReadsSchemaV1PacketFields()
	{
		// Java parity breadcrumb: docs/Phase-6-BindPointTeleport-PetFeedSubtype7RuntimeVectorDesign.md
		// schema for future PetService.checkFeeding rewarded-feed subtype 7 runtime vectors.
		const string json = """
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "pet-feed-reward-subtype7",
			  "serializationMode": "production-send",
			  "input": {
			    "playerObjectId": 7001,
			    "petObjectId": 7101,
			    "foodItemObjectId": 5001,
			    "foodItemId": 188000001,
			    "rewardItemId": 186000001,
			    "requestedCount": 1,
			    "cooldownMinutes": 1,
			    "preCallFeedProgressData": 1193040,
			    "postCallFeedProgressData": 0,
			    "postCallRefeedDelaySeconds": 60
			  },
			  "packets": [
			    {
			      "sequence": 0,
			      "packetClass": "SM_PET",
			      "opcode": 101,
			      "semanticKey": "pet-feed-progress",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "actionId": 9,
			        "foodHeader": 1,
			        "foodFunction": 1,
			        "foodSubType": 2,
			        "feedProgressData": 1193040,
			        "refeedDelaySeconds": 0,
			        "itemObjectId": 5001,
			        "count": 0,
			        "trailingByte": 0
			      }
			    },
			    {
			      "sequence": 1,
			      "packetClass": "SM_PET",
			      "opcode": 101,
			      "semanticKey": "pet-feed-reward-item",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "actionId": 9,
			        "foodHeader": 1,
			        "foodFunction": 1,
			        "foodSubType": 6,
			        "feedProgressData": 1193040,
			        "refeedDelaySeconds": 0,
			        "itemObjectId": 186000001,
			        "trailingByte": 0
			      }
			    },
			    {
			      "sequence": 2,
			      "packetClass": "SM_PET",
			      "opcode": 101,
			      "semanticKey": "pet-feed-end",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "actionId": 9,
			        "foodHeader": 1,
			        "foodFunction": 1,
			        "foodSubType": 5,
			        "feedProgressData": 1193040,
			        "refeedDelaySeconds": 0
			      }
			    },
			    {
			      "sequence": 3,
			      "packetClass": "SM_EMOTION",
			      "opcode": 37,
			      "semanticKey": "end-feeding-emotion",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "playerObjectId": 7001,
			        "emotionType": 51,
			        "state": 0,
			        "speed": 0,
			        "targetObjectId": 7001
			      }
			    },
			    {
			      "sequence": 4,
			      "packetClass": "SM_PET",
			      "opcode": 101,
			      "semanticKey": "pet-feed-refeed-notification",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "actionId": 9,
			        "foodHeader": 1,
			        "foodFunction": 1,
			        "foodSubType": 7,
			        "feedProgressData": 1193040,
			        "refeedDelaySeconds": 0,
			        "itemObjectId": 0,
			        "trailingInt": 0
			      }
			    }
			  ],
			  "stateSnapshots": [
			    {
			      "label": "before-checkFeeding",
			      "feedProgressData": 1193040,
			      "refeedDelaySeconds": 0,
			      "persistedRefeedTimeDeltaMillis": null
			    },
			    {
			      "label": "after-checkFeeding",
			      "feedProgressData": 0,
			      "refeedDelaySeconds": 60,
			      "persistedRefeedTimeDeltaMillis": 60000
			    }
			  ],
			  "notes": []
			}
			""";

		var artifact = JsonSerializer.Deserialize<PetFeedSubtype7JavaVectorArtifact>(json, JsonOptions);

		Assert.NotNull(artifact);
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.Equal("pet-feed-reward-subtype7", artifact.Scenario);
		Assert.Equal("production-send", artifact.SerializationMode);
		Assert.Equal(7001, artifact.Input.PlayerObjectId);
		Assert.Equal(7101, artifact.Input.PetObjectId);
		Assert.Equal(5001, artifact.Input.FoodItemObjectId);
		Assert.Equal(186000001, artifact.Input.RewardItemId);
		Assert.Equal(
			["pet-feed-progress", "pet-feed-reward-item", "pet-feed-end", "end-feeding-emotion", "pet-feed-refeed-notification"],
			artifact.Packets.Select(packet => packet.SemanticKey));
		AssertArtifactPacketSemantics(artifact);
	}

	[Fact]
	public async Task FindPetFeedSubtype7JavaArtifacts_IsGuardedUntilGeneratorOutputExists()
	{
		var artifactRoot = GetArtifactRoot();
		var artifacts = Directory.Exists(artifactRoot)
			? Directory.GetFiles(artifactRoot, "*.json").Order(StringComparer.Ordinal).ToArray()
			: [];

		if (artifacts.Length == 0)
		{
			output.WriteLine("Needs Verification: Java pet feed subtype 7 vector artifacts are not present yet.");
			return;
		}

		foreach (var artifactPath in artifacts)
		{
			var json = await File.ReadAllTextAsync(artifactPath);
			var artifact = JsonSerializer.Deserialize<PetFeedSubtype7JavaVectorArtifact>(json, JsonOptions);
			Assert.NotNull(artifact);
			Assert.Equal(1, artifact.SchemaVersion);
			Assert.NotEmpty(artifact.Scenario);
			Assert.NotEmpty(artifact.Packets);
			AssertArtifactPacketSemantics(artifact);
			AssertGeneratedBodyMatchesCSharpWhenPresent(artifact);
			AssertGeneratedCanonicalPayloadMatchesCSharpWhenPresent(artifact);
		}
	}

	private static void AssertArtifactPacketSemantics(PetFeedSubtype7JavaVectorArtifact artifact)
	{
		Assert.True(artifact.Input.CooldownMinutes > 0, "Subtype 7 vectors need a positive cooldown.");
		Assert.True(artifact.Input.PreCallFeedProgressData != artifact.Input.PostCallFeedProgressData,
			"Subtype 7 vectors need distinct pre-call and post-call feed progress.");
		Assert.Contains(artifact.Packets, packet => packet.SemanticKey == "pet-feed-refeed-notification");

		foreach (var packet in artifact.Packets)
		{
			Assert.True(packet.Sequence >= 0, $"{packet.PacketClass} sequence must be non-negative.");
			Assert.NotEmpty(packet.PacketClass);
			Assert.NotEmpty(packet.SemanticKey);
			Assert.NotNull(packet.Decoded);

			switch (packet.PacketClass)
			{
				case "SM_PET":
					Assert.Equal(SmPet.PacketOpCode, packet.Opcode);
					Assert.Equal((int)PetAction.Food, RequiredInt(packet.Decoded.ActionId, "actionId"));
					Assert.Equal(1, RequiredInt(packet.Decoded.FoodHeader, "foodHeader"));
					Assert.Equal(1, RequiredInt(packet.Decoded.FoodFunction, "foodFunction"));
					Assert.Contains(RequiredInt(packet.Decoded.FoodSubType, "foodSubType"), new[] { 2, 5, 6, 7 });
					Assert.True(packet.Decoded.FeedProgressData.HasValue, "SM_PET FOOD missing feedProgressData.");
					AssertPetFoodPacketSemantics(packet, artifact);
					break;
				case "SM_EMOTION":
					Assert.Equal(SmEmotion.PacketOpCode, packet.Opcode);
					Assert.True(packet.SemanticKey == "end-feeding-emotion", $"Unsupported SM_EMOTION semantic key: {packet.SemanticKey}");
					Assert.True(packet.Decoded.PlayerObjectId.HasValue, "SM_EMOTION missing playerObjectId.");
					Assert.Equal((int)EmotionType.EndFeeding, RequiredInt(packet.Decoded.EmotionType, "emotionType"));
					Assert.True(packet.Decoded.State.HasValue, "SM_EMOTION missing state.");
					Assert.True(packet.Decoded.Speed.HasValue, "SM_EMOTION missing speed.");
					Assert.True(packet.Decoded.TargetObjectId.HasValue, "SM_EMOTION missing targetObjectId.");
					break;
				default:
					Assert.Fail($"Unsupported pet feed subtype 7 vector packet class: {packet.PacketClass}");
					break;
			}
		}
	}

	private static void AssertPetFoodPacketSemantics(
		PetFeedSubtype7JavaVectorPacket packet,
		PetFeedSubtype7JavaVectorArtifact artifact)
	{
		switch (RequiredInt(packet.Decoded.FoodSubType, "foodSubType"))
		{
			case 2:
				Assert.Equal("pet-feed-progress", packet.SemanticKey);
				Assert.Equal(artifact.Input.FoodItemObjectId, RequiredInt(packet.Decoded.ItemObjectId, "itemObjectId"));
				Assert.True(packet.Decoded.Count.HasValue, "Subtype 2 needs count.");
				Assert.Equal(0, RequiredInt(packet.Decoded.TrailingByte, "trailingByte"));
				break;
			case 5:
				Assert.Equal("pet-feed-end", packet.SemanticKey);
				Assert.True(packet.Decoded.RefeedDelaySeconds.HasValue, "Subtype 5 needs refeedDelaySeconds.");
				break;
			case 6:
				Assert.Equal("pet-feed-reward-item", packet.SemanticKey);
				Assert.Equal(artifact.Input.RewardItemId, RequiredInt(packet.Decoded.ItemObjectId, "itemObjectId"));
				Assert.Equal(0, RequiredInt(packet.Decoded.TrailingByte, "trailingByte"));
				break;
			case 7:
				Assert.Equal("pet-feed-refeed-notification", packet.SemanticKey);
				Assert.True(packet.Decoded.RefeedDelaySeconds.HasValue, "Subtype 7 needs refeedDelaySeconds.");
				Assert.Equal(0, RequiredInt(packet.Decoded.ItemObjectId, "itemObjectId"));
				Assert.Equal(0, RequiredInt(packet.Decoded.TrailingInt, "trailingInt"));
				break;
		}
	}

	private static void AssertGeneratedBodyMatchesCSharpWhenPresent(PetFeedSubtype7JavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet => SupportsCSharpComparison(packet) && !string.IsNullOrWhiteSpace(packet.BodyHex)))
		{
			Assert.Equal(
				NormalizeHex(packet.BodyHex!),
				Convert.ToHexString(SerializeUnencryptedBody(CreateCSharpPacketFromArtifact(packet))));
		}
	}

	private static void AssertGeneratedCanonicalPayloadMatchesCSharpWhenPresent(PetFeedSubtype7JavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet => SupportsCSharpComparison(packet) && !string.IsNullOrWhiteSpace(packet.CanonicalPayloadHex)))
		{
			Assert.Equal(
				NormalizeHex(packet.CanonicalPayloadHex!),
				Convert.ToHexString(SerializeCanonicalPayload(CreateCSharpPacketFromArtifact(packet))));
		}
	}

	private static GameServerPacket CreateCSharpPacketFromArtifact(PetFeedSubtype7JavaVectorPacket packet)
	{
		return packet.PacketClass switch
		{
			"SM_PET" => SmPet.Food(new SmPetFoodSnapshot(
				RequiredInt(packet.Decoded.FoodSubType, "foodSubType"),
				RequiredInt(packet.Decoded.FeedProgressData, "feedProgressData"),
				packet.Decoded.ItemObjectId ?? 0,
				packet.Decoded.Count ?? 0,
				packet.Decoded.RefeedDelaySeconds ?? 0)),
			"SM_EMOTION" when packet.SemanticKey == "end-feeding-emotion" => new SmEmotion(
				new Player
				{
					ObjectId = RequiredInt(packet.Decoded.PlayerObjectId, "playerObjectId"),
					CreatureState = (PlayerCreatureState)RequiredInt(packet.Decoded.State, "state"),
				},
				(EmotionType)RequiredInt(packet.Decoded.EmotionType, "emotionType"),
				emotion: 0,
				targetObjectId: packet.Decoded.TargetObjectId ?? 0,
				speed: RequiredFloat(packet.Decoded.Speed, "speed")),
			_ => throw new NotSupportedException($"Unsupported pet feed subtype 7 vector packet: {packet.PacketClass}/{packet.SemanticKey}"),
		};
	}

	private static bool SupportsCSharpComparison(PetFeedSubtype7JavaVectorPacket packet) =>
		packet.PacketClass is "SM_PET" || packet.PacketClass == "SM_EMOTION" && packet.SemanticKey == "end-feeding-emotion";

	private static int RequiredInt(int? value, string fieldName)
	{
		Assert.True(value.HasValue, $"Missing decoded {fieldName}.");
		return value.Value;
	}

	private static float RequiredFloat(float? value, string fieldName)
	{
		Assert.True(value.HasValue, $"Missing decoded {fieldName}.");
		return value.Value;
	}

	private static byte[] SerializeUnencryptedBody(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static byte[] SerializeCanonicalPayload(GameServerPacket packet)
	{
		var body = SerializeUnencryptedBody(packet);
		var payload = new byte[sizeof(ushort) + body.Length];
		BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0, sizeof(ushort)), checked((ushort)packet.OpCode));
		body.CopyTo(payload.AsSpan(sizeof(ushort)));
		return payload;
	}

	private static string NormalizeHex(string hex) =>
		hex.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

	private static string GetArtifactRoot() =>
		Path.Combine(FindRepositoryRoot(), "parity-artifacts", "pet-feed-subtype7", "java");

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, ".git")) && Directory.Exists(Path.Combine(directory.FullName, "docs")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate repository root.");
	}

	private sealed record PetFeedSubtype7JavaVectorArtifact(
		int SchemaVersion,
		string JavaCommit,
		string Scenario,
		string SerializationMode,
		PetFeedSubtype7JavaVectorInput Input,
		IReadOnlyList<PetFeedSubtype7JavaVectorPacket> Packets,
		IReadOnlyList<PetFeedSubtype7StateSnapshot> StateSnapshots,
		IReadOnlyList<string> Notes);

	private sealed record PetFeedSubtype7JavaVectorInput(
		int PlayerObjectId,
		int PetObjectId,
		int FoodItemObjectId,
		int FoodItemId,
		int RewardItemId,
		int RequestedCount,
		int CooldownMinutes,
		int PreCallFeedProgressData,
		int PostCallFeedProgressData,
		int PostCallRefeedDelaySeconds);

	private sealed record PetFeedSubtype7JavaVectorPacket(
		int Sequence,
		string PacketClass,
		int Opcode,
		string SemanticKey,
		string? CanonicalPayloadHex,
		string? BodyHex,
		string? WireFrameHex,
		PetFeedSubtype7JavaVectorDecodedFields Decoded);

	private sealed record PetFeedSubtype7JavaVectorDecodedFields(
		int? ActionId,
		int? FoodHeader,
		int? FoodFunction,
		int? FoodSubType,
		int? FeedProgressData,
		int? RefeedDelaySeconds,
		int? ItemObjectId,
		int? Count,
		int? TrailingByte,
		int? TrailingInt,
		int? PlayerObjectId,
		int? EmotionType,
		int? State,
		float? Speed,
		int? TargetObjectId);

	private sealed record PetFeedSubtype7StateSnapshot(
		string Label,
		int FeedProgressData,
		int RefeedDelaySeconds,
		long? PersistedRefeedTimeDeltaMillis);
}
