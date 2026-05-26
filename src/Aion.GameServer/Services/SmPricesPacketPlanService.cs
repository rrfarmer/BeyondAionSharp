using Aion.GameServer.Configuration;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record SmPricesPacketPlan(
	PriceSnapshot Snapshot,
	string JavaSource,
	string Notes,
	bool IsLive = false)
{
	public SmPrices ToPacket()
	{
		return new SmPrices(Snapshot.GlobalPrices, Snapshot.GlobalPricesModifier, Snapshot.Taxes);
	}
}

public static class SmPricesPacketPlanService
{
	public static SmPricesPacketPlan CreatePlan(
		string playerRace,
		GameServerPriceOptions options,
		PriceInfluenceRates influenceRates)
	{
		// Java parity breadcrumbs:
		// - services/player/PlayerEnterWorldService.sendPacketsAfterPlayerEnterWorld sends new SM_PRICES().
		// - network/aion/serverpackets/SM_PRICES.writeImpl reads PricesService global prices/modifier/taxes.
		var snapshot = PricesService.CreateSnapshot(playerRace, options, influenceRates);
		return new SmPricesPacketPlan(
			snapshot,
			"PlayerEnterWorldService.sendPacketsAfterPlayerEnterWorld -> new SM_PRICES(); SM_PRICES.writeImpl -> PricesService.getGlobalPrices/getGlobalPricesModifier/getTaxes",
			"Non-live packet plan only. Influence rates are injected until Java Influence/siege state has a C# runtime source.",
			IsLive: false);
	}
}
