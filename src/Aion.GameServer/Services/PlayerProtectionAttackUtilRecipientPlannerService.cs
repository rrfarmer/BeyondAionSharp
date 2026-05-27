namespace Aion.GameServer.Services;

public enum PlayerProtectionAttackUtilKnownObjectKind
{
	Other,
	Creature,
	Player,
}

public enum PlayerProtectionAttackUtilCandidateStatus
{
	Eligible,
	SkippedNotCreature,
	SkippedTargetMismatch,
	SkippedNotCasting,
	SkippedCastingFirstTargetMismatch,
	SkippedNotPlayer,
	SkippedCanSeeWhenValidateSee,
	DuplicateKnownObjectIdCollapsed,
}

public sealed record PlayerProtectionAttackUtilKnownObjectFact(
	int KnownObjectId,
	PlayerProtectionAttackUtilKnownObjectKind Kind,
	int? TargetObjectId,
	bool IsCasting,
	int? CastingSkillFirstTargetObjectId,
	bool CanSeeProtectedTarget = false,
	string JavaSource = "com.aionemu.gameserver.world.knownlist.KnownList.forEachObject");

public sealed record PlayerProtectionAttackUtilRecipientProjection(
	int KnownObjectId,
	PlayerProtectionAttackUtilKnownObjectKind Kind,
	PlayerProtectionAttackUtilCandidateStatus Status,
	bool WouldCancelCast,
	bool WouldClearTarget,
	bool IsLive,
	string JavaOperation,
	string JavaSource,
	string Notes);

public sealed record PlayerProtectionAttackUtilRecipientPlan(
	int ProtectedPlayerObjectId,
	bool ValidateSeeForTargetRemoval,
	IReadOnlyList<PlayerProtectionAttackUtilRecipientProjection> CastCancellationProjections,
	IReadOnlyList<PlayerProtectionAttackUtilRecipientProjection> TargetClearProjections,
	bool UsesKnownListForEachObject,
	bool UsesKnownListForEachPlayer,
	bool DuplicateKnownObjectIdsCollapsed,
	string JavaSource,
	bool IsLive)
{
	public IReadOnlyList<int> CastCancellationObjectIds { get; } =
		CastCancellationProjections
			.Where(projection => projection.WouldCancelCast)
			.Select(projection => projection.KnownObjectId)
			.ToArray();

	public IReadOnlyList<int> TargetClearPlayerObjectIds { get; } =
		TargetClearProjections
			.Where(projection => projection.WouldClearTarget)
			.Select(projection => projection.KnownObjectId)
			.ToArray();
}

public static class PlayerProtectionAttackUtilRecipientPlannerService
{
	public static PlayerProtectionAttackUtilRecipientPlan CreatePlan(
		int protectedPlayerObjectId,
		IEnumerable<PlayerProtectionAttackUtilKnownObjectFact>? knownObjectFacts,
		bool validateSeeForTargetRemoval = false)
	{
		var facts = CollapseDuplicateKnownObjectIds(knownObjectFacts);

		return new PlayerProtectionAttackUtilRecipientPlan(
			protectedPlayerObjectId,
			validateSeeForTargetRemoval,
			facts.Select(fact => ProjectCastCancellation(protectedPlayerObjectId, fact)).ToArray(),
			facts.Select(fact => ProjectTargetClear(protectedPlayerObjectId, fact, validateSeeForTargetRemoval)).ToArray(),
			UsesKnownListForEachObject: true,
			UsesKnownListForEachPlayer: true,
			DuplicateKnownObjectIdsCollapsed: true,
			"AttackUtil.cancelCastOn(target) / AttackUtil.removeTargetFrom(target, validateSee)",
			IsLive: false);
	}

	private static IReadOnlyList<PlayerProtectionAttackUtilKnownObjectFact> CollapseDuplicateKnownObjectIds(
		IEnumerable<PlayerProtectionAttackUtilKnownObjectFact>? knownObjectFacts)
	{
		var factsByObjectId = new Dictionary<int, PlayerProtectionAttackUtilKnownObjectFact>();
		foreach (var fact in knownObjectFacts ?? Array.Empty<PlayerProtectionAttackUtilKnownObjectFact>())
			factsByObjectId[fact.KnownObjectId] = fact;

		return factsByObjectId.Values.ToArray();
	}

	private static PlayerProtectionAttackUtilRecipientProjection ProjectCastCancellation(
		int protectedPlayerObjectId,
		PlayerProtectionAttackUtilKnownObjectFact fact)
	{
		if (fact.Kind == PlayerProtectionAttackUtilKnownObjectKind.Other)
			return CastProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedNotCreature, wouldCancelCast: false, "Known object is not a Creature.");
		if (fact.TargetObjectId != protectedPlayerObjectId)
			return CastProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedTargetMismatch, wouldCancelCast: false, "Creature target is not the protected player.");
		if (!fact.IsCasting)
			return CastProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedNotCasting, wouldCancelCast: false, "Creature has no current casting skill.");
		if (fact.CastingSkillFirstTargetObjectId != protectedPlayerObjectId)
			return CastProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedCastingFirstTargetMismatch, wouldCancelCast: false, "Casting skill first target is not the protected player.");

		return CastProjection(fact, PlayerProtectionAttackUtilCandidateStatus.Eligible, wouldCancelCast: true, "Java would call creature.getController().cancelCurrentSkill(null).");
	}

	private static PlayerProtectionAttackUtilRecipientProjection ProjectTargetClear(
		int protectedPlayerObjectId,
		PlayerProtectionAttackUtilKnownObjectFact fact,
		bool validateSee)
	{
		if (fact.Kind != PlayerProtectionAttackUtilKnownObjectKind.Player)
			return TargetProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedNotPlayer, wouldClearTarget: false, "Known object is not a Player.");
		if (fact.TargetObjectId != protectedPlayerObjectId)
			return TargetProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedTargetMismatch, wouldClearTarget: false, "Known player target is not the protected player.");
		if (validateSee && fact.CanSeeProtectedTarget)
			return TargetProjection(fact, PlayerProtectionAttackUtilCandidateStatus.SkippedCanSeeWhenValidateSee, wouldClearTarget: false, "Java validateSee=true clears only players that cannot see the target.");

		return TargetProjection(fact, PlayerProtectionAttackUtilCandidateStatus.Eligible, wouldClearTarget: true, validateSee
			? "Java validateSee=true and player cannot see the protected target, so player.setTarget(null) would run."
			: "Protection start calls removeTargetFrom(target) with validateSee=false, so player.setTarget(null) would run.");
	}

	private static PlayerProtectionAttackUtilRecipientProjection CastProjection(
		PlayerProtectionAttackUtilKnownObjectFact fact,
		PlayerProtectionAttackUtilCandidateStatus status,
		bool wouldCancelCast,
		string notes) =>
		new(
			fact.KnownObjectId,
			fact.Kind,
			status,
			wouldCancelCast,
			WouldClearTarget: false,
			IsLive: false,
			"AttackUtil.cancelCastOn(target)",
			"target.getKnownList().forEachObject(visibleObject -> visibleObject instanceof Creature && creature.getTarget() == target && creature.getCastingSkill().getFirstTarget().equals(target))",
			notes);

	private static PlayerProtectionAttackUtilRecipientProjection TargetProjection(
		PlayerProtectionAttackUtilKnownObjectFact fact,
		PlayerProtectionAttackUtilCandidateStatus status,
		bool wouldClearTarget,
		string notes) =>
		new(
			fact.KnownObjectId,
			fact.Kind,
			status,
			WouldCancelCast: false,
			wouldClearTarget,
			IsLive: false,
			"AttackUtil.removeTargetFrom(target, validateSee)",
			"object.getKnownList().forEachPlayer(player -> player.getTarget() == object && (!validateSee || !player.canSee(object)))",
			notes);
}
