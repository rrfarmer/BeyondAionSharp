using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class PlayerKnownListAttackSpeedFactPlanRequestAdapterService
{
	private readonly PlayerKnownListAttackSpeedFactResolverService _resolver;

	public PlayerKnownListAttackSpeedFactPlanRequestAdapterService(
		PlayerKnownListAttackSpeedFactResolverService? resolver = null)
	{
		_resolver = resolver ?? new PlayerKnownListAttackSpeedFactResolverService();
	}

	public PlayerKnownListPacketConstructionFactPlanRequest AttachRideAttackSpeedResolution(
		PlayerKnownListPacketConstructionFactPlanRequest request,
		ItemTemplateTable? itemTemplates)
	{
		// Java parity breadcrumb: PlayerController.sendPlayerInfoPackets reaches
		// PlayerGameStats.getAttackSpeed() through live Player state. This adapter
		// only attaches the disabled C# resolver result to supplied snapshot input.
		if (!request.DirectionFacts.SubjectIsInRideMode
			|| request.RideAttackSpeedFacts is not null
			|| request.RideAttackSpeedResolution is not null)
		{
			return request;
		}

		return request with
		{
			RideAttackSpeedResolution = _resolver.Resolve(request.SubjectPlayer, itemTemplates),
		};
	}
}
