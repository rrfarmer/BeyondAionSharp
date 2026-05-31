namespace Aion.GameServer.Services;

public enum CmCraftRuntimePlanStatus
{
	NoPlayerOrNotSpawned,
	ShuttingDownSoon,
	InvalidNonMorphTarget,
	StartCrafting,
}

public sealed record CmCraftStartIntent(
	int RecipeId,
	int TargetObjectId,
	int CraftType,
	IReadOnlyDictionary<int, long> MaterialsData,
	bool UsesMorphTargetBypass);

public sealed record CmCraftRuntimePlan(
	CmCraftRuntimePlanStatus Status,
	CmCraftStartIntent? StartIntent,
	bool RequiresStaticTargetValidation,
	string JavaSource,
	bool IsLive);

public static class CmCraftRuntimePlanService
{
	public const int MorphSubstancesMarker = 129;

	public static CmCraftRuntimePlan CreatePlan(
		bool hasPlayer,
		bool isPlayerSpawned,
		bool isShuttingDownSoon,
		int unknownByte,
		int recipeId,
		int targetObjectId,
		int craftType,
		IReadOnlyDictionary<int, long>? materialsData,
		bool targetExists,
		bool targetIsInRange,
		bool targetTemplateMatches)
	{
		// Java parity: network/aion/clientpackets/CM_CRAFT.runImpl.
		if (!hasPlayer || !isPlayerSpawned)
		{
			return new CmCraftRuntimePlan(
				CmCraftRuntimePlanStatus.NoPlayerOrNotSpawned,
				StartIntent: null,
				RequiresStaticTargetValidation: false,
				"CM_CRAFT.runImpl -> if (player == null || !player.isSpawned()) return",
				IsLive: false);
		}

		if (isShuttingDownSoon)
		{
			return new CmCraftRuntimePlan(
				CmCraftRuntimePlanStatus.ShuttingDownSoon,
				StartIntent: null,
				RequiresStaticTargetValidation: false,
				"CM_CRAFT.runImpl -> if (GameServer.isShuttingDownSoon()) return",
				IsLive: false);
		}

		var usesMorphTargetBypass = unknownByte == MorphSubstancesMarker;
		if (!usesMorphTargetBypass && (!targetExists || !targetIsInRange || !targetTemplateMatches))
		{
			return new CmCraftRuntimePlan(
				CmCraftRuntimePlanStatus.InvalidNonMorphTarget,
				StartIntent: null,
				RequiresStaticTargetValidation: true,
				"CM_CRAFT.runImpl -> if (unk != 129 && (staticObject == null || !PositionUtil.isInRange(player, staticObject, 10) || templateId mismatch)) return",
				IsLive: false);
		}

		return new CmCraftRuntimePlan(
			CmCraftRuntimePlanStatus.StartCrafting,
			new CmCraftStartIntent(
				recipeId,
				targetObjectId,
				craftType,
				materialsData ?? new Dictionary<int, long>(),
				usesMorphTargetBypass),
			RequiresStaticTargetValidation: !usesMorphTargetBypass,
			usesMorphTargetBypass
				? "CM_CRAFT.runImpl morph -> skip static-object range/template validation and call CraftService.startCrafting(...)"
				: "CM_CRAFT.runImpl -> validate static-object target, then call CraftService.startCrafting(...)",
			IsLive: false);
	}
}
