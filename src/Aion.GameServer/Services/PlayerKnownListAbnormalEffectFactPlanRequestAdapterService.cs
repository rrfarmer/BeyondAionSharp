namespace Aion.GameServer.Services;

public sealed class PlayerKnownListAbnormalEffectFactPlanRequestAdapterService
{
	private readonly PlayerKnownListAbnormalEffectFactResolverService _resolver;

	public PlayerKnownListAbnormalEffectFactPlanRequestAdapterService(
		PlayerKnownListAbnormalEffectFactResolverService? resolver = null)
	{
		_resolver = resolver ?? new PlayerKnownListAbnormalEffectFactResolverService();
	}

	public PlayerKnownListPacketConstructionFactPlanRequest AttachAbnormalEffectResolution(
		PlayerKnownListPacketConstructionFactPlanRequest request,
		IReadOnlyDictionary<int, IReadOnlyList<PlayerKnownListAbnormalEffectSnapshotEntry>>? effectSnapshotsByPlayerObjectId)
	{
		// Java parity breadcrumb: PlayerController.sendPlayerInfoPackets reaches
		// EffectController.getAbnormals/getAbnormalEffects through live Player
		// state. This adapter only attaches the disabled resolver result to
		// supplied snapshot input and never hydrates a live EffectController.
		if (!request.DirectionFacts.SubjectHasAbnormalEffects
			|| effectSnapshotsByPlayerObjectId is null
			|| request.AbnormalEffects is not null
			|| request.AbnormalEffectMask is not null
			|| request.AbnormalEffectResolution is not null)
		{
			return request;
		}

		var subjectObjectId = request.SubjectPlayer?.ObjectId ?? 0;
		effectSnapshotsByPlayerObjectId.TryGetValue(subjectObjectId, out var effects);

		return request with
		{
			AbnormalEffectResolution = _resolver.Resolve(request.SubjectPlayer, effects, request.AbnormalEffectSlots),
		};
	}
}
