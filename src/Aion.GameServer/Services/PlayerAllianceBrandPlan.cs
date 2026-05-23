using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed record PlayerAllianceBrandUpdatePlan(
	int AllianceId,
	int BrandId,
	int TargetObjectId,
	IReadOnlyList<PlayerAllianceBrandIntent> BrandBroadcasts);

public sealed record PlayerAllianceBrandIntent(
	int RecipientObjectId,
	IReadOnlyDictionary<int, int> TargetObjectIdsByBrandId)
{
	public SmShowBrand CreatePacket()
	{
		// Java parity: model/team/TemporaryPlayerTeam.sendBrands sends SM_SHOW_BRAND(current brand map) to the target player.
		return new SmShowBrand(TargetObjectIdsByBrandId);
	}
}
