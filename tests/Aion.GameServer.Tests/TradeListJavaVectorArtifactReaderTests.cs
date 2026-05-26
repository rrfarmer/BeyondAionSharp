using System.Text.Json;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class TradeListJavaVectorArtifactReaderTests(ITestOutputHelper output)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Fact]
	public void ParseTradeListArtifact_ReadsSchemaV1PacketFields()
	{
		// Java parity breadcrumb: docs/TradeList-Java-Golden-Vector-Design.md schema for
		// future DialogService BUY -> SM_TRADELIST runtime vectors.
		const string json = """
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "buy-sellable-normal",
			  "input": {
			    "dialogActionId": 2,
			    "targetObjectId": 9001,
			    "playerObjectId": 1001,
			    "npcId": 203060,
			    "questId": 0,
			    "lastPage": 0,
			    "extendedRewardIndex": 0
			  },
			  "runtimeFacts": {
			    "playerLegionLevel": 0,
			    "vendorBuyModifier": 125,
			    "tradeSellPriceRate": 80,
			    "buyPriceModifier": 100,
			    "npcCanSell": true,
			    "npcCanBuy": true
			  },
			  "packets": [
			    {
			      "sequence": 0,
			      "packetClass": "SM_TRADELIST",
			      "opcode": 149,
			      "semanticKey": "trade-list",
			      "canonicalPayloadHex": "9500",
			      "bodyHex": "29230000",
			      "wireFrameHex": null,
			      "decoded": {
			        "targetObjId": 9001,
			        "tradeNpcTypeIndex": 1,
			        "buyPriceModifier": 100,
			        "fixedClientModifier": 100,
			        "showBuyTab": true,
			        "showSellTab": true,
			        "tradeTabIds": [129],
			        "limitedItems": [
			          { "itemId": 186000001, "buyCount": 0, "sellLimit": 5 }
			        ]
			      }
			    }
			  ],
			  "notes": []
			}
			""";

		var artifact = JsonSerializer.Deserialize<TradeListJavaVectorArtifact>(json, JsonOptions);

		Assert.NotNull(artifact);
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.Equal("buy-sellable-normal", artifact.Scenario);
		Assert.Equal(2, artifact.Input.DialogActionId);
		Assert.Equal(203060, artifact.Input.NpcId);
		Assert.Equal(125, artifact.RuntimeFacts.VendorBuyModifier);
		Assert.Equal(100, artifact.RuntimeFacts.BuyPriceModifier);
		var packet = Assert.Single(artifact.Packets);
		Assert.Equal("SM_TRADELIST", packet.PacketClass);
		Assert.Equal(149, packet.Opcode);
		Assert.Equal("trade-list", packet.SemanticKey);
		Assert.Equal("9500", packet.CanonicalPayloadHex);
		Assert.Equal([129], packet.Decoded.TradeTabIds);
		var limitedItem = Assert.Single(packet.Decoded.LimitedItems);
		Assert.Equal(186000001, limitedItem.ItemId);
		Assert.Equal(0, limitedItem.BuyCount);
		Assert.Equal(5, limitedItem.SellLimit);
		AssertArtifactPacketSemantics(artifact);
	}

	[Fact]
	public void ParseTradeInArtifact_ReadsSchemaV1PacketFields()
	{
		// Java parity breadcrumb: DialogService TRADE_IN -> SM_TRADE_IN_LIST with fixed modifier 100.
		const string json = """
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "trade-in-sellable",
			  "input": {
			    "dialogActionId": 78,
			    "targetObjectId": 9006,
			    "playerObjectId": 1001,
			    "npcId": 205315,
			    "questId": 0,
			    "lastPage": 0,
			    "extendedRewardIndex": 0
			  },
			  "runtimeFacts": {
			    "playerLegionLevel": 0,
			    "vendorBuyModifier": 100,
			    "tradeSellPriceRate": 100,
			    "buyPriceModifier": 100,
			    "npcCanSell": true,
			    "npcCanBuy": true
			  },
			  "packets": [
			    {
			      "sequence": 0,
			      "packetClass": "SM_TRADE_IN_LIST",
			      "opcode": 151,
			      "semanticKey": "trade-in-list",
			      "canonicalPayloadHex": "9700",
			      "bodyHex": "2E230000",
			      "wireFrameHex": null,
			      "decoded": {
			        "targetObjId": 9006,
			        "tradeNpcTypeIndex": 1,
			        "buyPriceModifier": 100,
			        "fixedClientModifier": 100,
			        "showBuyTab": null,
			        "showSellTab": null,
			        "tradeTabIds": [39],
			        "limitedItems": []
			      }
			    }
			  ],
			  "notes": []
			}
			""";

		var artifact = JsonSerializer.Deserialize<TradeListJavaVectorArtifact>(json, JsonOptions);

		Assert.NotNull(artifact);
		Assert.Equal("trade-in-sellable", artifact.Scenario);
		Assert.Equal(78, artifact.Input.DialogActionId);
		var packet = Assert.Single(artifact.Packets);
		Assert.Equal("SM_TRADE_IN_LIST", packet.PacketClass);
		Assert.Equal(151, packet.Opcode);
		Assert.Equal("trade-in-list", packet.SemanticKey);
		Assert.Equal(100, packet.Decoded.BuyPriceModifier);
		Assert.Equal([39], packet.Decoded.TradeTabIds);
		Assert.Empty(packet.Decoded.LimitedItems);
		AssertArtifactPacketSemantics(artifact);
	}

	[Fact]
	public void ParseNoSellArtifact_ReadsSystemMessageFields()
	{
		// Java parity breadcrumb: DialogService BUY/TRADE_IN no-sell fallback ->
		// SM_SYSTEM_MESSAGE.STR_BUY_SELL_HE_DOES_NOT_SELL_ITEM.
		const string json = """
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "buy-no-template",
			  "input": {
			    "dialogActionId": 2,
			    "targetObjectId": 9002,
			    "playerObjectId": 1001,
			    "npcId": 203061,
			    "questId": 0,
			    "lastPage": 0,
			    "extendedRewardIndex": 0
			  },
			  "runtimeFacts": {
			    "playerLegionLevel": 0,
			    "vendorBuyModifier": 100,
			    "tradeSellPriceRate": 0,
			    "buyPriceModifier": 0,
			    "npcCanSell": true,
			    "npcCanBuy": true
			  },
			  "packets": [
			    {
			      "sequence": 0,
			      "packetClass": "SM_SYSTEM_MESSAGE",
			      "opcode": 13,
			      "semanticKey": "buy-no-trade-list",
			      "canonicalPayloadHex": "0D00",
			      "bodyHex": "19000000",
			      "wireFrameHex": null,
			      "decoded": {
			        "messageId": 1300336,
			        "npcNameParam": "Merchant",
			        "messageParams": ["Merchant"],
			        "tradeTabIds": [],
			        "limitedItems": []
			      }
			    }
			  ],
			  "notes": []
			}
			""";

		var artifact = JsonSerializer.Deserialize<TradeListJavaVectorArtifact>(json, JsonOptions);

		Assert.NotNull(artifact);
		Assert.Equal("buy-no-template", artifact.Scenario);
		var packet = Assert.Single(artifact.Packets);
		Assert.Equal("SM_SYSTEM_MESSAGE", packet.PacketClass);
		Assert.Equal("buy-no-trade-list", packet.SemanticKey);
		Assert.Equal(1300336, packet.Decoded.MessageId);
		Assert.Equal("Merchant", packet.Decoded.NpcNameParam);
		Assert.Equal(["Merchant"], packet.Decoded.MessageParams);
		Assert.Empty(packet.Decoded.TradeTabIds);
		Assert.Empty(packet.Decoded.LimitedItems);
		AssertArtifactPacketSemantics(artifact);
	}

	[Fact]
	public async Task FindTradeListJavaArtifacts_IsGuardedUntilGeneratorOutputExists()
	{
		var artifactRoot = GetTradeListArtifactRoot();
		var artifacts = Directory.Exists(artifactRoot)
			? Directory.GetFiles(artifactRoot, "*.json").Order(StringComparer.Ordinal).ToArray()
			: [];

		if (artifacts.Length == 0)
		{
			output.WriteLine("Needs Verification: Java trade-list vector artifacts are not present yet.");
			return;
		}

		foreach (var artifactPath in artifacts)
		{
			var json = await File.ReadAllTextAsync(artifactPath);
			var artifact = JsonSerializer.Deserialize<TradeListJavaVectorArtifact>(json, JsonOptions);
			Assert.NotNull(artifact);
			Assert.Equal(1, artifact.SchemaVersion);
			Assert.NotEmpty(artifact.Scenario);
			Assert.NotEmpty(artifact.Packets);
			AssertArtifactPacketSemantics(artifact);
			AssertGeneratedTradeListBodyMatchesCSharpWhenPresent(artifact);
			AssertGeneratedTradeInListBodyMatchesCSharpWhenPresent(artifact);
			AssertGeneratedSystemMessageBodyMatchesCSharpWhenPresent(artifact);
		}
	}

	private static void AssertArtifactPacketSemantics(TradeListJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets)
		{
			Assert.True(packet.Sequence >= 0, $"{packet.PacketClass} sequence must be non-negative.");
			Assert.NotEmpty(packet.PacketClass);
			Assert.NotEmpty(packet.SemanticKey);
			Assert.NotNull(packet.Decoded);

			switch (packet.PacketClass)
			{
				case "SM_TRADELIST":
					Assert.Equal("trade-list", packet.SemanticKey);
					AssertTradePacketDecodedFields(packet, expectsTabFlags: true);
					break;
				case "SM_TRADE_IN_LIST":
					Assert.Equal("trade-in-list", packet.SemanticKey);
					AssertTradePacketDecodedFields(packet, expectsTabFlags: false);
					break;
				case "SM_SYSTEM_MESSAGE":
					Assert.True(IsNoSellSemanticKey(packet.SemanticKey), $"Unsupported no-sell semantic key: {packet.SemanticKey}");
					Assert.True(packet.Decoded.MessageId.HasValue, $"{packet.PacketClass} missing decoded messageId.");
					Assert.False(string.IsNullOrWhiteSpace(packet.Decoded.NpcNameParam));
					var messageParams = Assert.IsAssignableFrom<IReadOnlyList<string>>(packet.Decoded.MessageParams);
					Assert.NotEmpty(messageParams);
					Assert.Contains(packet.Decoded.NpcNameParam, messageParams);
					Assert.Empty(packet.Decoded.TradeTabIds);
					Assert.Empty(packet.Decoded.LimitedItems);
					break;
				default:
					Assert.Fail($"Unsupported packet class in trade-list vector artifact: {packet.PacketClass}");
					break;
			}
		}
	}

	private static void AssertTradePacketDecodedFields(TradeListJavaVectorPacket packet, bool expectsTabFlags)
	{
		Assert.True(packet.Decoded.TargetObjId.HasValue, $"{packet.PacketClass} missing decoded targetObjId.");
		Assert.True(packet.Decoded.TradeNpcTypeIndex.HasValue, $"{packet.PacketClass} missing decoded tradeNpcTypeIndex.");
		Assert.True(packet.Decoded.BuyPriceModifier.HasValue, $"{packet.PacketClass} missing decoded buyPriceModifier.");
		Assert.True(packet.Decoded.FixedClientModifier.HasValue, $"{packet.PacketClass} missing decoded fixedClientModifier.");
		Assert.NotNull(packet.Decoded.TradeTabIds);
		Assert.NotNull(packet.Decoded.LimitedItems);

		if (expectsTabFlags)
		{
			Assert.True(packet.Decoded.ShowBuyTab.HasValue, $"{packet.PacketClass} missing decoded showBuyTab.");
			Assert.True(packet.Decoded.ShowSellTab.HasValue, $"{packet.PacketClass} missing decoded showSellTab.");
		}

		foreach (var limitedItem in packet.Decoded.LimitedItems)
		{
			Assert.True(limitedItem.ItemId > 0, $"{packet.PacketClass} limited item id must be positive.");
			Assert.True(limitedItem.BuyCount >= 0, $"{packet.PacketClass} limited item buy count must be non-negative.");
			Assert.True(limitedItem.SellLimit >= 0, $"{packet.PacketClass} limited item sell limit must be non-negative.");
		}
	}

	private static void AssertGeneratedTradeListBodyMatchesCSharpWhenPresent(TradeListJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet =>
			packet.PacketClass == "SM_TRADELIST" && !string.IsNullOrWhiteSpace(packet.BodyHex)))
		{
			var expectedBodyHex = packet.BodyHex!;
			if (!TryGetTradeNpcTypeName(packet.Decoded.TradeNpcTypeIndex.GetValueOrDefault(), out var npcType))
				Assert.Fail($"Unsupported SM_TRADELIST tradeNpcTypeIndex in generated artifact: {packet.Decoded.TradeNpcTypeIndex}");

			var plan = SmTradeListPacketPlanService.CreatePlan(
				new SmTradeListPacketPlanInput(
					TargetObjectId: packet.Decoded.TargetObjId.GetValueOrDefault(),
					PlayerObjectId: artifact.Input.PlayerObjectId,
					TradeList: new TradeListTemplateSummary(
						artifact.Input.NpcId,
						packet.Decoded.TradeTabIds,
						NpcType: npcType),
					GoodsLists: new GoodsListTable(
						packet.Decoded.TradeTabIds.Select(id => new GoodsListSummary(id)).ToArray(),
						Array.Empty<GoodsListSummary>(),
						Array.Empty<GoodsListSummary>()),
					PlayerLegionLevel: artifact.RuntimeFacts.PlayerLegionLevel,
					NpcCanSell: packet.Decoded.ShowBuyTab.GetValueOrDefault(),
					NpcCanBuy: packet.Decoded.ShowSellTab.GetValueOrDefault(),
					BuyPriceModifier: packet.Decoded.BuyPriceModifier.GetValueOrDefault(),
					LimitedItems: packet.Decoded.LimitedItems
						.Select(item => new SmTradeListLimitedItemSummary(item.ItemId, item.BuyCount, item.SellLimit))
						.ToArray()));

			Assert.Equal(SmTradeListPacketPlanStatus.Ready, plan.Status);
			Assert.Equal(
				NormalizeHex(expectedBodyHex),
				Convert.ToHexString(SerializeUnencryptedBody(new SmTradeList(plan))));
		}
	}

	private static void AssertGeneratedTradeInListBodyMatchesCSharpWhenPresent(TradeListJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet =>
			packet.PacketClass == "SM_TRADE_IN_LIST" && !string.IsNullOrWhiteSpace(packet.BodyHex)))
		{
			var expectedBodyHex = packet.BodyHex!;
			if (!TryGetTradeNpcTypeName(packet.Decoded.TradeNpcTypeIndex.GetValueOrDefault(), out var npcType))
				Assert.Fail($"Unsupported SM_TRADE_IN_LIST tradeNpcTypeIndex in generated artifact: {packet.Decoded.TradeNpcTypeIndex}");

			var plan = SmTradeInListPacketPlanService.CreatePlan(
				new SmTradeInListPacketPlanInput(
					TargetObjectId: packet.Decoded.TargetObjId.GetValueOrDefault(),
					TradeInList: new TradeListTemplateSummary(
						artifact.Input.NpcId,
						packet.Decoded.TradeTabIds,
						NpcType: npcType),
					BuyPriceModifier: packet.Decoded.BuyPriceModifier.GetValueOrDefault()));

			Assert.Equal(SmTradeInListPacketPlanStatus.Ready, plan.Status);
			Assert.Equal(
				NormalizeHex(expectedBodyHex),
				Convert.ToHexString(SerializeUnencryptedBody(new SmTradeInList(plan))));
		}
	}

	private static void AssertGeneratedSystemMessageBodyMatchesCSharpWhenPresent(TradeListJavaVectorArtifact artifact)
	{
		foreach (var packet in artifact.Packets.Where(packet =>
			packet.PacketClass == "SM_SYSTEM_MESSAGE" && !string.IsNullOrWhiteSpace(packet.BodyHex)))
		{
			var expectedBodyHex = packet.BodyHex!;
			var messageId = Assert.IsType<int>(packet.Decoded.MessageId);
			var messageParams = Assert.IsAssignableFrom<IReadOnlyList<string>>(packet.Decoded.MessageParams);
			Assert.True(IsNoSellSemanticKey(packet.SemanticKey), $"Unsupported no-sell semantic key: {packet.SemanticKey}");

			Assert.Equal(
				NormalizeHex(expectedBodyHex),
				Convert.ToHexString(SerializeUnencryptedBody(new SmSystemMessage(messageId, messageParams.ToArray()))));
		}
	}

	private static bool IsNoSellSemanticKey(string semanticKey)
	{
		return semanticKey is "buy-no-trade-list" or "buy-no-sellable-goods" or "buy-restricted-goods" or "trade-in-no-template";
	}

	private static bool TryGetTradeNpcTypeName(int index, out string npcType)
	{
		npcType = index switch
		{
			1 => "NORMAL",
			2 => "ABYSS",
			3 => "LEGION_COIN",
			4 => "REWARD",
			5 => "ABYSS_KINAH",
			_ => string.Empty,
		};

		return npcType.Length > 0;
	}

	private static string NormalizeHex(string hex)
	{
		return hex.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
	}

	private static byte[] SerializeUnencryptedBody(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}

	private static string GetTradeListArtifactRoot()
	{
		return Path.Combine(FindRepositoryRoot(), "parity-artifacts", "trade-list", "java");
	}

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

	private sealed record TradeListJavaVectorArtifact(
		int SchemaVersion,
		string JavaCommit,
		string Scenario,
		TradeListJavaVectorInput Input,
		TradeListJavaVectorRuntimeFacts RuntimeFacts,
		IReadOnlyList<TradeListJavaVectorPacket> Packets,
		IReadOnlyList<string> Notes);

	private sealed record TradeListJavaVectorInput(
		int DialogActionId,
		int TargetObjectId,
		int PlayerObjectId,
		int NpcId,
		int QuestId,
		int LastPage,
		int ExtendedRewardIndex);

	private sealed record TradeListJavaVectorRuntimeFacts(
		int PlayerLegionLevel,
		int VendorBuyModifier,
		int TradeSellPriceRate,
		int BuyPriceModifier,
		bool NpcCanSell,
		bool NpcCanBuy);

	private sealed record TradeListJavaVectorPacket(
		int Sequence,
		string PacketClass,
		int Opcode,
		string SemanticKey,
		string? CanonicalPayloadHex,
		string? BodyHex,
		string? WireFrameHex,
		TradeListJavaVectorDecodedFields Decoded);

	private sealed record TradeListJavaVectorDecodedFields(
		int? TargetObjId,
		int? TradeNpcTypeIndex,
		int? BuyPriceModifier,
		int? FixedClientModifier,
		bool? ShowBuyTab,
		bool? ShowSellTab,
		int? MessageId,
		string? NpcNameParam,
		IReadOnlyList<string>? MessageParams,
		IReadOnlyList<int> TradeTabIds,
		IReadOnlyList<TradeListJavaVectorLimitedItem> LimitedItems);

	private sealed record TradeListJavaVectorLimitedItem(
		int ItemId,
		int BuyCount,
		int SellLimit);
}
