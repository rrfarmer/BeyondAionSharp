using System.Text.Json;
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
		}
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
		IReadOnlyList<int> TradeTabIds,
		IReadOnlyList<TradeListJavaVectorLimitedItem> LimitedItems);

	private sealed record TradeListJavaVectorLimitedItem(
		int ItemId,
		int BuyCount,
		int SellLimit);
}
