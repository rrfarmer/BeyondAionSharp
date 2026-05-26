using Aion.Commons.Network;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class SmPricesPacketPlanServiceTests
{
	[Fact]
	public void CreatePlan_DefaultInfluenceMatchesJavaDefaultSmPricesPayload()
	{
		var plan = SmPricesPacketPlanService.CreatePlan(
			"ELYOS",
			new GameServerPriceOptions(),
			new PriceInfluenceRates());

		Assert.False(plan.IsLive);
		Assert.Equal(new PriceSnapshot(100, 100, 100, 100, 20), plan.Snapshot);
		Assert.Contains("SM_PRICES.writeImpl", plan.JavaSource);
		Assert.Equal(Convert.FromHexString("646464"), SerializeUnencryptedPayload(plan.ToPacket()));
	}

	[Fact]
	public void CreatePlan_AsymmetricInfluenceSerializesJavaCalculatedPriceBytes()
	{
		var options = new GameServerPriceOptions
		{
			DefaultPrices = 100,
			DefaultModifier = 95,
			DefaultTaxes = 100,
			VendorBuyModifier = 125,
			VendorSellModifier = 22,
		};
		var influence = new PriceInfluenceRates(Elyos: 0.7f, Asmodians: 0.3f);

		var elyos = SmPricesPacketPlanService.CreatePlan("ELYOS", options, influence);
		var asmodians = SmPricesPacketPlanService.CreatePlan("ASMODIANS", options, influence);

		Assert.Equal(new PriceSnapshot(90, 95, 100, 125, 22), elyos.Snapshot);
		Assert.Equal(new PriceSnapshot(110, 95, 105, 125, 22), asmodians.Snapshot);
		Assert.Equal(Convert.FromHexString("5A5F64"), SerializeUnencryptedPayload(elyos.ToPacket()));
		Assert.Equal(Convert.FromHexString("6E5F69"), SerializeUnencryptedPayload(asmodians.ToPacket()));
	}

	private static byte[] SerializeUnencryptedPayload(GameServerPacket packet)
	{
		var crypt = new GameCrypt(() => 0x01020304);
		crypt.EnableKey();
		var frame = packet.SerializeFrame(crypt);
		return frame[7..];
	}
}
