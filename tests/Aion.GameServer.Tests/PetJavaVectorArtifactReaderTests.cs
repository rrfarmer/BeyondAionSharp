using System.Text.Json;
using Aion.GameServer.Model.GameObjects;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PetJavaVectorArtifactReaderTests(ITestOutputHelper output)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Fact]
	public void ParseKnownListPetArtifact_ReadsSchemaV1PacketFields()
	{
		// Java parity breadcrumb: docs/Phase-6-BindPointTeleport-KnownListPetGoldenVectorDesign.md
		// schema for future PlayerController.see/notSee Pet packet runtime vectors.
		const string json = """
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "known-list-pet-spawn-fly-dismiss",
			  "input": {
			    "viewerObjectId": 9001,
			    "masterObjectId": 9002,
			    "petObjectId": 9102,
			    "petTemplateId": 900001,
			    "petName": "Tog",
			    "masterIsInFlyingState": true
			  },
			  "packets": [
			    {
			      "sequence": 0,
			      "packetClass": "SM_PET",
			      "opcode": 101,
			      "semanticKey": "pet-spawn",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "actionId": 3,
			        "petName": "Tog",
			        "templateId": 900001,
			        "petObjectId": 9102,
			        "x": 10,
			        "y": 20,
			        "z": 30,
			        "targetX": 11,
			        "targetY": 21,
			        "targetZ": 31,
			        "heading": 90,
			        "masterObjectId": 9002,
			        "decoration": 12345
			      }
			    },
			    {
			      "sequence": 1,
			      "packetClass": "SM_PET_EMOTE",
			      "opcode": 187,
			      "semanticKey": "pet-fly-start",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "petObjectId": 9102,
			        "emoteId": 129,
			        "emotionId": 0,
			        "param1": 0
			      }
			    },
			    {
			      "sequence": 2,
			      "packetClass": "SM_PET",
			      "opcode": 101,
			      "semanticKey": "pet-dismiss",
			      "canonicalPayloadHex": null,
			      "bodyHex": null,
			      "wireFrameHex": null,
			      "decoded": {
			        "actionId": 4,
			        "petObjectId": 9102,
			        "animationId": 1
			      }
			    }
			  ],
			  "notes": []
			}
			""";

		var artifact = JsonSerializer.Deserialize<PetJavaVectorArtifact>(json, JsonOptions);

		Assert.NotNull(artifact);
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.Equal("known-list-pet-spawn-fly-dismiss", artifact.Scenario);
		Assert.Equal(9001, artifact.Input.ViewerObjectId);
		Assert.Equal(9002, artifact.Input.MasterObjectId);
		Assert.Equal(9102, artifact.Input.PetObjectId);
		Assert.True(artifact.Input.MasterIsInFlyingState);
		Assert.Equal(["pet-spawn", "pet-fly-start", "pet-dismiss"], artifact.Packets.Select(packet => packet.SemanticKey));
		AssertArtifactPacketSemantics(artifact);
	}

	[Fact]
	public async Task FindPetJavaArtifacts_IsGuardedUntilGeneratorOutputExists()
	{
		var artifactRoot = GetPetArtifactRoot();
		var artifacts = Directory.Exists(artifactRoot)
			? Directory.GetFiles(artifactRoot, "*.json").Order(StringComparer.Ordinal).ToArray()
			: [];

		if (artifacts.Length == 0)
		{
			output.WriteLine("Needs Verification: Java known-list pet vector artifacts are not present yet.");
			return;
		}

		foreach (var artifactPath in artifacts)
		{
			var json = await File.ReadAllTextAsync(artifactPath);
			var artifact = JsonSerializer.Deserialize<PetJavaVectorArtifact>(json, JsonOptions);
			Assert.NotNull(artifact);
			Assert.Equal(1, artifact.SchemaVersion);
			Assert.NotEmpty(artifact.Scenario);
			Assert.NotEmpty(artifact.Packets);
			AssertArtifactPacketSemantics(artifact);
		}
	}

	private static void AssertArtifactPacketSemantics(PetJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets)
		{
			Assert.True(packet.Sequence >= 0, $"{packet.PacketClass} sequence must be non-negative.");
			Assert.NotEmpty(packet.PacketClass);
			Assert.NotEmpty(packet.SemanticKey);
			Assert.NotNull(packet.Decoded);

			switch (packet.PacketClass)
			{
				case "SM_PET":
					// Canonical NCSoft opcode from ServerPacketsOpcodes (addPacketOpcode(101, SM_PET)).
					Assert.Equal(SmPetOpCode, packet.Opcode);
					Assert.True(packet.SemanticKey is "pet-spawn" or "pet-dismiss", $"Unsupported SM_PET semantic key: {packet.SemanticKey}");
					Assert.True(packet.Decoded.ActionId.HasValue, "SM_PET missing actionId.");
					Assert.True(packet.Decoded.PetObjectId.HasValue, "SM_PET missing petObjectId.");
					if (packet.SemanticKey == "pet-spawn")
					{
						Assert.Equal((int)PetAction.Spawn, packet.Decoded.ActionId);
						Assert.False(string.IsNullOrWhiteSpace(packet.Decoded.PetName));
						Assert.True(packet.Decoded.TemplateId.HasValue);
						Assert.True(packet.Decoded.X.HasValue);
						Assert.True(packet.Decoded.Y.HasValue);
						Assert.True(packet.Decoded.Z.HasValue);
						Assert.True(packet.Decoded.TargetX.HasValue);
						Assert.True(packet.Decoded.TargetY.HasValue);
						Assert.True(packet.Decoded.TargetZ.HasValue);
						Assert.True(packet.Decoded.Heading.HasValue);
						Assert.True(packet.Decoded.MasterObjectId.HasValue);
						Assert.True(packet.Decoded.Decoration.HasValue);
					}
					else
					{
						Assert.Equal((int)PetAction.Dismiss, packet.Decoded.ActionId);
						Assert.True(packet.Decoded.AnimationId.HasValue);
					}

					break;
				case "SM_PET_EMOTE":
					// Canonical NCSoft opcode from ServerPacketsOpcodes (addPacketOpcode(187, SM_PET_EMOTE)).
					Assert.Equal(SmPetEmoteOpCode, packet.Opcode);
					Assert.True(packet.Decoded.PetObjectId.HasValue, "SM_PET_EMOTE missing petObjectId.");
					Assert.True(packet.Decoded.EmoteId.HasValue, "SM_PET_EMOTE missing emoteId.");
					Assert.True(packet.SemanticKey is "pet-fly-start" or "pet-move-stop" or "pet-move-to",
						$"Unsupported SM_PET_EMOTE semantic key: {packet.SemanticKey}");
					if (packet.SemanticKey == "pet-move-stop" || packet.SemanticKey == "pet-move-to")
					{
						Assert.True(packet.Decoded.X.HasValue);
						Assert.True(packet.Decoded.Y.HasValue);
						Assert.True(packet.Decoded.Z.HasValue);
						Assert.True(packet.Decoded.Heading.HasValue);
					}

					if (packet.SemanticKey == "pet-move-to")
					{
						Assert.True(packet.Decoded.TargetX.HasValue);
						Assert.True(packet.Decoded.TargetY.HasValue);
						Assert.True(packet.Decoded.TargetZ.HasValue);
					}

					break;
				default:
					Assert.Fail($"Unsupported pet vector packet class: {packet.PacketClass}");
					break;
			}
		}
	}

	// Canonical NCSoft opcodes from ServerPacketsOpcodes: addPacketOpcode(101, SM_PET),
	// addPacketOpcode(187, SM_PET_EMOTE). The faithful SM_PET/SM_PET_EMOTE : AionServerPacket
	// take a live Pet; this design-vector test asserts only schema/semantics until real Java
	// byte oracles (bodyHex/canonicalPayloadHex) exist on disk — at which point the byte-exact
	// comparison should drive the faithful packets via the GoldenPacketFixtureTests
	// CaptureWriteImplPayload path with an uninitialized-Pet fixture (no faked bytes).
	private const int SmPetOpCode = 101;
	private const int SmPetEmoteOpCode = 187;

	private static string GetPetArtifactRoot() =>
		Path.Combine(FindRepositoryRoot(), "parity-artifacts", "known-list-pet", "java");

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

	private sealed record PetJavaVectorArtifact(
		int SchemaVersion,
		string JavaCommit,
		string Scenario,
		PetJavaVectorInput Input,
		IReadOnlyList<PetJavaVectorPacket> Packets,
		IReadOnlyList<string> Notes);

	private sealed record PetJavaVectorInput(
		int ViewerObjectId,
		int MasterObjectId,
		int PetObjectId,
		int PetTemplateId,
		string PetName,
		bool MasterIsInFlyingState);

	private sealed record PetJavaVectorPacket(
		int Sequence,
		string PacketClass,
		int Opcode,
		string SemanticKey,
		string? CanonicalPayloadHex,
		string? BodyHex,
		string? WireFrameHex,
		PetJavaVectorDecodedFields Decoded);

	private sealed record PetJavaVectorDecodedFields(
		int? ActionId,
		string? PetName,
		int? TemplateId,
		int? PetObjectId,
		float? X,
		float? Y,
		float? Z,
		float? TargetX,
		float? TargetY,
		float? TargetZ,
		int? Heading,
		int? MasterObjectId,
		int? Decoration,
		int? EmoteId,
		int? EmotionId,
		int? Param1,
		int? AnimationId);
}
