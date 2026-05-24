using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class PlayerSummonSkillExecutionService
{
	public PlayerSummonSkillExecutionResult ValidateExecution(
		Player player,
		PlayerPetSkillOrder order,
		PetSkillTable petSkills)
	{
		// Java parity: controllers/SummonController.useSkill(SkillOrder) petHasSkill guard before SkillEngine invocation.
		if (!player.HasPetSummon || player.PetSummonNpcId == 0)
			return PlayerSummonSkillExecutionResult.MissingSummon(order);

		if (!petSkills.PetHasSkill(player.PetSummonNpcId, order.SkillId))
			return PlayerSummonSkillExecutionResult.InvalidPetSkill(player.PetSummonNpcId, order);

		return PlayerSummonSkillExecutionResult.WouldInvokeSkillEngine(player.PetSummonNpcId, order);
	}
}

public sealed record PlayerSummonSkillExecutionResult(
	PlayerSummonSkillExecutionStatus Status,
	int PetSummonNpcId,
	PlayerPetSkillOrder Order)
{
	public static PlayerSummonSkillExecutionResult MissingSummon(PlayerPetSkillOrder order)
	{
		return new PlayerSummonSkillExecutionResult(PlayerSummonSkillExecutionStatus.MissingSummon, 0, order);
	}

	public static PlayerSummonSkillExecutionResult InvalidPetSkill(int petSummonNpcId, PlayerPetSkillOrder order)
	{
		return new PlayerSummonSkillExecutionResult(PlayerSummonSkillExecutionStatus.InvalidPetSkill, petSummonNpcId, order);
	}

	public static PlayerSummonSkillExecutionResult WouldInvokeSkillEngine(int petSummonNpcId, PlayerPetSkillOrder order)
	{
		return new PlayerSummonSkillExecutionResult(PlayerSummonSkillExecutionStatus.WouldInvokeSkillEngine, petSummonNpcId, order);
	}
}

public enum PlayerSummonSkillExecutionStatus
{
	MissingSummon,
	InvalidPetSkill,
	WouldInvokeSkillEngine,
}
