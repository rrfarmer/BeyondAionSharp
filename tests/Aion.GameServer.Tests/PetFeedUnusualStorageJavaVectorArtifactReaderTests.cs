using System.Text.Json;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.ToyPet;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PetFeedUnusualStorageJavaVectorArtifactReaderTests(ITestOutputHelper output)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Fact]
	public void ParseUnusualStorageArtifact_ReadsSchemaV1PacketAndBlobFields()
	{
		// Java parity breadcrumb: docs/Phase-6-BindPointTeleport-PetFeedUnusualStorageRuntimeArtifactSchema.md
		// schema for future PetService.checkFeeding rejected-food unusual-storage runtime vectors.
		const string json = """
			{
			  "schemaVersion": 1,
			  "scenario": "pet-bag-moved",
			  "javaSources": [
			    "com.aionemu.gameserver.services.toypet.PetService.checkFeeding",
			    "com.aionemu.gameserver.services.item.ItemPacketService.sendStorageUpdatePacket",
			    "com.aionemu.gameserver.network.aion.serverpackets.SM_WAREHOUSE_ADD_ITEM",
			    "com.aionemu.gameserver.network.aion.serverpackets.SM_CUBE_UPDATE"
			  ],
			  "storage": {
			    "storageId": 32,
			    "storageTypeName": "PET_BAG_6",
			    "storageTypeOrdinal": 4,
			    "expectedReachability": "delayed-mutable-item-reference",
			    "normalUiFlow": false
			  },
			  "timing": {
			    "feedItemLookupPhase": "pre-delay-cube-inventory",
			    "unlockDecisionPhase": "post-delay-rejected-food",
			    "packetConstructionPhase": "sendItemUnlockPacket/sendStorageUpdatePacket",
			    "packetSerializationPhase": "AionConnection.writeData/AionServerPacket.write",
			    "itemReferenceIsMutable": true
			  },
			  "constructionSnapshot": {
			    "warehouseType": 32,
			    "addType": "ALL_SLOT",
			    "addTypeMask": 19,
			    "packetOrder": [
			      "SM_WAREHOUSE_ADD_ITEM",
			      "SM_CUBE_UPDATE"
			    ]
			  },
			  "encodeSnapshot": {
			    "item": {
			      "objectId": 5001,
			      "itemId": 188000001,
			      "count": 2,
			      "itemLocation": 32,
			      "equipmentSlot": 4,
			      "itemTemplateId": 188000001,
			      "localizedName": "Odd Snack",
			      "packCount": 0,
			      "expireTime": 0,
			      "temporaryExchangeTime": 0,
			      "charge": 0,
			      "enchantLevel": 0,
			      "itemMask": 0,
			      "color": null
			    },
			    "itemBlob": {
			      "hex": "0003010203",
			      "size": 3,
			      "packetBodyVerification": "matched",
			      "entryIds": [1, 16],
			      "decodedEntries": [
			        { "entryId": 1, "name": "GENERAL_INFO" },
			        { "entryId": 16, "name": "WRAP_INFO" }
			      ],
			      "templateDerivedInputs": {
			        "itemMask": 0,
			        "slotGroup": "NONE",
			        "polishEligible": false,
			        "conditionable": false,
			        "bonusStatModifiers": []
			      },
			      "dynamicInputs": {
			        "fusionRandomBonusStatsId": 0,
			        "temporaryExchangeTime": 0,
			        "cleanupSealFlag": 0,
			        "accountLegionWarehouseRestrictionFlag": 0,
			        "unsealTime": 0,
			        "conditioningInfoPresent": false,
			        "plumeTemperingStats": []
			      },
			      "timeNormalization": {
			        "capturedAtEpochSeconds": 1800000000,
			        "expirationRemainingSeconds": 0,
			        "dyeRemainingSeconds": 0
			      }
			    }
			  },
			  "packets": [
			    {
			      "javaClass": "com.aionemu.gameserver.network.aion.serverpackets.SM_WAREHOUSE_ADD_ITEM",
			      "opcode": 169,
			      "bodyHex": "2000130001000013890B35138100004F0064006400200053006E00610063006B000000030102030004",
			      "canonicalPayloadHex": "2000130001000013890B35138100004F0064006400200053006E00610063006B000000030102030004",
			      "decoded": {
			        "warehouseType": 32,
			        "addTypeMask": 19,
			        "itemCount": 1
			      }
			    },
			    {
			      "javaClass": "com.aionemu.gameserver.network.aion.serverpackets.SM_CUBE_UPDATE",
			      "opcode": 130,
			      "bodyHex": "000400000000000000",
			      "canonicalPayloadHex": "000400000000000000",
			      "decoded": {
			        "action": 0,
			        "actionValue": 4,
			        "itemsCount": 0,
			        "npcExpands": 0,
			        "questExpands": 0,
			        "itemExpands": 0
			      }
			    }
			  ],
			  "notes": []
			}
			""";

		var artifact = JsonSerializer.Deserialize<PetFeedUnusualStorageJavaVectorArtifact>(json, JsonOptions);

		Assert.NotNull(artifact);
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.Equal("pet-bag-moved", artifact.Scenario);
		Assert.Equal(32, artifact.Storage.StorageId);
		Assert.Equal(4, artifact.Storage.StorageTypeOrdinal);
		Assert.False(artifact.Storage.NormalUiFlow);
		Assert.True(artifact.Timing.ItemReferenceIsMutable);
		Assert.Equal(["SM_WAREHOUSE_ADD_ITEM", "SM_CUBE_UPDATE"], artifact.ConstructionSnapshot.PacketOrder);
		Assert.Equal([1, 16], artifact.EncodeSnapshot.ItemBlob.EntryIds);
		Assert.Equal(2, artifact.EncodeSnapshot.ItemBlob.DecodedEntries.Count);
		Assert.Equal("matched", artifact.EncodeSnapshot.ItemBlob.PacketBodyVerification);
		AssertArtifactSemantics(artifact);
		AssertBridgeConstructsGuardedUnusualStorageSequence(artifact);
		AssertGeneratedCubeUpdateBodyMatchesCSharpWhenPresent(artifact);
		AssertGeneratedCubeUpdateCanonicalPayloadMatchesCSharpWhenPresent(artifact);
		Assert.Contains(KnownBlobSerializerGap.StatBonuses, GetKnownBlobSerializerGaps());
	}

	[Fact]
	public async Task FindUnusualStorageJavaArtifacts_IsGuardedUntilGeneratorOutputExists()
	{
		var artifactRoot = GetArtifactRoot();
		var artifacts = Directory.Exists(artifactRoot)
			? Directory.GetFiles(artifactRoot, "*.json").Order(StringComparer.Ordinal).ToArray()
			: [];

		if (artifacts.Length == 0)
		{
			output.WriteLine("Needs Verification: Java pet feed unusual-storage vector artifacts are not present yet.");
			return;
		}

		foreach (var artifactPath in artifacts)
		{
			var json = await File.ReadAllTextAsync(artifactPath);
			var artifact = JsonSerializer.Deserialize<PetFeedUnusualStorageJavaVectorArtifact>(json, JsonOptions);
			Assert.NotNull(artifact);
			Assert.Equal(1, artifact.SchemaVersion);
			Assert.NotEmpty(artifact.Scenario);
			AssertArtifactSemantics(artifact);
			AssertBridgeConstructsGuardedUnusualStorageSequence(artifact);
			AssertGeneratedCubeUpdateBodyMatchesCSharpWhenPresent(artifact);
			AssertGeneratedCubeUpdateCanonicalPayloadMatchesCSharpWhenPresent(artifact);
			ReportWarehouseByteComparisonGapWhenPresent(artifact);
		}
	}

	private void ReportWarehouseByteComparisonGapWhenPresent(PetFeedUnusualStorageJavaVectorArtifact artifact)
	{
		if (artifact.Packets.Any(packet => IsPacketClass(packet, "SM_WAREHOUSE_ADD_ITEM")
				&& (!string.IsNullOrWhiteSpace(packet.BodyHex) || !string.IsNullOrWhiteSpace(packet.CanonicalPayloadHex))))
		{
			output.WriteLine("Needs Verification: SM_WAREHOUSE_ADD_ITEM bytes are present, but full C# item-blob parity is still guarded.");
			output.WriteLine("Known blob serializer gaps: " + string.Join(", ", GetKnownBlobSerializerGaps()));
		}
	}

	private static void AssertArtifactSemantics(PetFeedUnusualStorageJavaVectorArtifact artifact)
	{
		Assert.True(SmCubeUpdate.TryGetJavaStorageOrdinal(artifact.Storage.StorageId, out var modeledOrdinal),
			$"Storage id {artifact.Storage.StorageId} is not modeled by the C# Java ordinal resolver.");
		Assert.Equal(modeledOrdinal, artifact.Storage.StorageTypeOrdinal);
		Assert.Equal("delayed-mutable-item-reference", artifact.Storage.ExpectedReachability);
		Assert.Equal("pre-delay-cube-inventory", artifact.Timing.FeedItemLookupPhase);
		Assert.Equal("post-delay-rejected-food", artifact.Timing.UnlockDecisionPhase);
		Assert.True(artifact.Timing.ItemReferenceIsMutable, "Unusual-storage vectors need Java's delayed mutable item reference timing.");
		Assert.Equal(artifact.Storage.StorageId, artifact.ConstructionSnapshot.WarehouseType);
		Assert.Equal("ALL_SLOT", artifact.ConstructionSnapshot.AddType);
		Assert.Equal(SmWarehouseAddItem.AllSlot, artifact.ConstructionSnapshot.AddTypeMask);
		Assert.Equal(["SM_WAREHOUSE_ADD_ITEM", "SM_CUBE_UPDATE"], artifact.ConstructionSnapshot.PacketOrder);
		Assert.Equal(artifact.Storage.StorageId, artifact.EncodeSnapshot.Item.ItemLocation);
		Assert.Equal(artifact.EncodeSnapshot.Item.ItemId, artifact.EncodeSnapshot.Item.ItemTemplateId);
		Assert.NotEmpty(artifact.EncodeSnapshot.Item.LocalizedName);
		AssertBlobMetadataIsComparable(artifact.EncodeSnapshot.ItemBlob);

		var warehousePacket = Assert.Single(artifact.Packets, packet => IsPacketClass(packet, "SM_WAREHOUSE_ADD_ITEM"));
		Assert.Equal(SmWarehouseAddItem.PacketOpCode, warehousePacket.Opcode);
		Assert.False(string.IsNullOrWhiteSpace(warehousePacket.BodyHex));
		Assert.Equal(warehousePacket.BodyHex, warehousePacket.CanonicalPayloadHex);
		Assert.Equal(artifact.Storage.StorageId, RequiredInt(warehousePacket.Decoded.WarehouseType, "warehouseType"));
		Assert.Equal(SmWarehouseAddItem.AllSlot, RequiredInt(warehousePacket.Decoded.AddTypeMask, "addTypeMask"));
		Assert.Equal(1, RequiredInt(warehousePacket.Decoded.ItemCount, "itemCount"));

		var cubePacket = Assert.Single(artifact.Packets, packet => IsPacketClass(packet, "SM_CUBE_UPDATE"));
		Assert.Equal(SmCubeUpdate.PacketOpCode, cubePacket.Opcode);
		Assert.False(string.IsNullOrWhiteSpace(cubePacket.BodyHex));
		Assert.Equal(cubePacket.BodyHex, cubePacket.CanonicalPayloadHex);
		Assert.Equal(0, RequiredInt(cubePacket.Decoded.Action, "action"));
		Assert.Equal(artifact.Storage.StorageTypeOrdinal, RequiredInt(cubePacket.Decoded.ActionValue, "actionValue"));
		Assert.Equal(0, RequiredInt(cubePacket.Decoded.ItemsCount, "itemsCount"));
		Assert.Equal(0, RequiredInt(cubePacket.Decoded.NpcExpands, "npcExpands"));
		Assert.Equal(0, RequiredInt(cubePacket.Decoded.QuestExpands, "questExpands"));
		Assert.Equal(0, RequiredInt(cubePacket.Decoded.ItemExpands, "itemExpands"));
	}

	private static void AssertBridgeConstructsGuardedUnusualStorageSequence(PetFeedUnusualStorageJavaVectorArtifact artifact)
	{
		var bridge = new PetFeedPacketMetadataBridge();
		var plan = new PetFeedServiceOperationPlan(
			PetFeedServiceOperationPlanStatus.RejectedFood,
			Evaluation: null,
			Operations: [new PetFeedServiceOperation(PetFeedServiceOperationKind.UnlockFoodItem, ItemObjectId: artifact.EncodeSnapshot.Item.ObjectId)],
			RemainingRequestedCount: 1,
			RefeedTimeMilliseconds: null);

		var bridgeResult = bridge.Construct(new PetFeedPacketMetadataBridgeRequest(
			plan,
			FeedProgressData: 0,
			SupplementalContext: new PetFeedSupplementalPacketContext(
				UnlockPacketContext: new PetFeedUnlockPacketContext(
					PetFeedUnlockPacketStorageKind.UnusualWarehouse,
					CreateInventoryItem(artifact),
					CreateItemTemplate(artifact)))));

		Assert.Equal(PetFeedPacketMetadataBridgeStatus.Constructed, bridgeResult.Status);
		var result = Assert.Single(bridgeResult.Results);
		Assert.Equal(PetFeedPacketMetadataResultStatus.Constructed, result.Status);
		Assert.Collection(
			result.Packets,
			packet => Assert.IsType<SmWarehouseAddItem>(packet),
			packet => Assert.IsType<SmCubeUpdate>(packet));
	}

	private static void AssertBlobMetadataIsComparable(PetFeedUnusualStorageItemBlob blob)
	{
		Assert.NotNull(blob.EntryIds);
		Assert.NotNull(blob.DecodedEntries);
		Assert.NotNull(blob.TemplateDerivedInputs);
		Assert.NotNull(blob.DynamicInputs);
		Assert.NotNull(blob.TimeNormalization);
		Assert.Equal(blob.EntryIds.Count, blob.DecodedEntries.Count);
		Assert.NotEmpty(blob.Hex);
		Assert.Equal((blob.Size + 2) * 2, blob.Hex.Length);
		Assert.Contains(blob.PacketBodyVerification, new[] { "matched", "mismatched", "unavailable" });
		Assert.True(blob.TimeNormalization.CapturedAtEpochSeconds >= 0);
		Assert.True(blob.TimeNormalization.ExpirationRemainingSeconds >= 0);
		Assert.True(blob.TimeNormalization.DyeRemainingSeconds >= 0);
	}

	private static void AssertGeneratedCubeUpdateBodyMatchesCSharpWhenPresent(PetFeedUnusualStorageJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet => IsPacketClass(packet, "SM_CUBE_UPDATE") && !string.IsNullOrWhiteSpace(packet.BodyHex)))
		{
			Assert.Equal(
				NormalizeHex(packet.BodyHex!),
				Convert.ToHexString(SerializeUnencryptedBody(SmCubeUpdate.ZeroSizeForJavaStorageId(artifact.Storage.StorageId))));
		}
	}

	private static void AssertGeneratedCubeUpdateCanonicalPayloadMatchesCSharpWhenPresent(PetFeedUnusualStorageJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet => IsPacketClass(packet, "SM_CUBE_UPDATE") && !string.IsNullOrWhiteSpace(packet.CanonicalPayloadHex)))
		{
			Assert.Equal(
				NormalizeHex(packet.CanonicalPayloadHex!),
				Convert.ToHexString(SerializeUnencryptedBody(SmCubeUpdate.ZeroSizeForJavaStorageId(artifact.Storage.StorageId))));
		}
	}

	private static bool IsPacketClass(PetFeedUnusualStoragePacket packet, string simpleName) =>
		string.Equals(packet.JavaClass, simpleName, StringComparison.Ordinal)
		|| packet.JavaClass.EndsWith("." + simpleName, StringComparison.Ordinal);

	private static int RequiredInt(int? value, string fieldName)
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

	private static string NormalizeHex(string hex) =>
		hex.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

	private static InventoryItem CreateInventoryItem(PetFeedUnusualStorageJavaVectorArtifact artifact) =>
		new()
		{
			ObjectId = artifact.EncodeSnapshot.Item.ObjectId,
			ItemId = artifact.EncodeSnapshot.Item.ItemId,
			Count = artifact.EncodeSnapshot.Item.Count,
			Color = artifact.EncodeSnapshot.Item.Color,
			ExpireTime = artifact.EncodeSnapshot.Item.ExpireTime,
			Slot = artifact.EncodeSnapshot.Item.EquipmentSlot,
			Location = artifact.EncodeSnapshot.Item.ItemLocation,
			Enchant = artifact.EncodeSnapshot.Item.EnchantLevel,
			Charge = artifact.EncodeSnapshot.Item.Charge,
			PackCount = artifact.EncodeSnapshot.Item.PackCount,
			FusionRandomBonus = artifact.EncodeSnapshot.ItemBlob.DynamicInputs.FusionRandomBonusStatsId,
		};

	private static ItemTemplateSummary CreateItemTemplate(PetFeedUnusualStorageJavaVectorArtifact artifact) =>
		new(
			artifact.EncodeSnapshot.Item.ItemTemplateId,
			artifact.EncodeSnapshot.Item.LocalizedName,
			DescriptionId: 0,
			Mask: artifact.EncodeSnapshot.ItemBlob.TemplateDerivedInputs.ItemMask,
			Level: 1,
			ItemGroup: artifact.EncodeSnapshot.ItemBlob.TemplateDerivedInputs.SlotGroup ?? "NONE",
			ItemType: "NORMAL",
			Quality: "COMMON",
			Race: "PC_ALL",
			MaxStackCount: 100,
			Price: 0,
			ValidEquipmentSlots: artifact.EncodeSnapshot.Item.EquipmentSlot);

	private static IReadOnlyList<KnownBlobSerializerGap> GetKnownBlobSerializerGaps() =>
	[
		KnownBlobSerializerGap.StatBonuses,
		KnownBlobSerializerGap.FusionRandomBonusStatsId,
		KnownBlobSerializerGap.TemporaryExchangeAndSealFlags,
		KnownBlobSerializerGap.PlumeTemperingStats,
		KnownBlobSerializerGap.RuntimeConditioningPresence,
		KnownBlobSerializerGap.TimeDependentExpirationAndDyeValues,
	];

	private static string GetArtifactRoot() =>
		Path.Combine(FindRepositoryRoot(), "parity-artifacts", "pet-feed-unusual-storage", "java");

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

	private enum KnownBlobSerializerGap
	{
		StatBonuses,
		FusionRandomBonusStatsId,
		TemporaryExchangeAndSealFlags,
		PlumeTemperingStats,
		RuntimeConditioningPresence,
		TimeDependentExpirationAndDyeValues,
	}

	private sealed record PetFeedUnusualStorageJavaVectorArtifact(
		int SchemaVersion,
		string Scenario,
		IReadOnlyList<string> JavaSources,
		PetFeedUnusualStorageFacts Storage,
		PetFeedUnusualStorageTiming Timing,
		PetFeedUnusualStorageConstructionSnapshot ConstructionSnapshot,
		PetFeedUnusualStorageEncodeSnapshot EncodeSnapshot,
		IReadOnlyList<PetFeedUnusualStoragePacket> Packets,
		IReadOnlyList<string> Notes);

	private sealed record PetFeedUnusualStorageFacts(
		int StorageId,
		string StorageTypeName,
		int StorageTypeOrdinal,
		string ExpectedReachability,
		bool NormalUiFlow);

	private sealed record PetFeedUnusualStorageTiming(
		string FeedItemLookupPhase,
		string UnlockDecisionPhase,
		string PacketConstructionPhase,
		string PacketSerializationPhase,
		bool ItemReferenceIsMutable);

	private sealed record PetFeedUnusualStorageConstructionSnapshot(
		int WarehouseType,
		string AddType,
		int AddTypeMask,
		IReadOnlyList<string> PacketOrder);

	private sealed record PetFeedUnusualStorageEncodeSnapshot(
		PetFeedUnusualStorageItem Item,
		PetFeedUnusualStorageItemBlob ItemBlob);

	private sealed record PetFeedUnusualStorageItem(
		int ObjectId,
		int ItemId,
		long Count,
		int ItemLocation,
		int EquipmentSlot,
		int ItemTemplateId,
		string LocalizedName,
		int PackCount,
		int ExpireTime,
		int TemporaryExchangeTime,
		int Charge,
		int EnchantLevel,
		int ItemMask,
		int? Color);

	private sealed record PetFeedUnusualStorageItemBlob(
		string Hex,
		int Size,
		string PacketBodyVerification,
		IReadOnlyList<int> EntryIds,
		IReadOnlyList<JsonElement> DecodedEntries,
		PetFeedUnusualStorageTemplateDerivedInputs TemplateDerivedInputs,
		PetFeedUnusualStorageDynamicInputs DynamicInputs,
		PetFeedUnusualStorageTimeNormalization TimeNormalization);

	private sealed record PetFeedUnusualStorageTemplateDerivedInputs(
		int ItemMask,
		string? SlotGroup,
		bool PolishEligible,
		bool Conditionable,
		IReadOnlyList<JsonElement> BonusStatModifiers);

	private sealed record PetFeedUnusualStorageDynamicInputs(
		int FusionRandomBonusStatsId,
		int TemporaryExchangeTime,
		int CleanupSealFlag,
		int AccountLegionWarehouseRestrictionFlag,
		int UnsealTime,
		bool ConditioningInfoPresent,
		IReadOnlyList<JsonElement> PlumeTemperingStats);

	private sealed record PetFeedUnusualStorageTimeNormalization(
		long CapturedAtEpochSeconds,
		int ExpirationRemainingSeconds,
		int DyeRemainingSeconds);

	private sealed record PetFeedUnusualStoragePacket(
		string JavaClass,
		int Opcode,
		string? BodyHex,
		string? CanonicalPayloadHex,
		PetFeedUnusualStoragePacketDecoded Decoded);

	private sealed record PetFeedUnusualStoragePacketDecoded(
		int? WarehouseType,
		int? AddTypeMask,
		int? ItemCount,
		int? Action,
		int? ActionValue,
		int? ItemsCount,
		int? NpcExpands,
		int? QuestExpands,
		int? ItemExpands);
}
