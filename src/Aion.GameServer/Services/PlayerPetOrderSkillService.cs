using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerPetOrderSkillService
{
	public PlayerPetOrderSkillResult ApplyUltraSkillOrder(
		Player effector,
		PlayerPetOrderSkillRequest request,
		PetSkillTable petSkills,
		SkillTemplateTable skillTemplates)
	{
		// Java parity: skillengine/effect/PetOrderUseUltraSkillEffect.applyEffect.
		if (!(effector.GetSummon() != null) || (effector.GetSummon()?.GetObjectId() ?? 0) == 0 || (effector.GetSummon()?.GetNpcId() ?? 0) == 0)
			return PlayerPetOrderSkillResult.MissingSummon(request.OrderSkillId);

		if (request.EffectedObjectId == 0)
			return PlayerPetOrderSkillResult.MissingEffected(request.OrderSkillId, (effector.GetSummon()?.GetNpcId() ?? 0));

		var petUseSkillId = petSkills.GetPetOrderSkill(request.OrderSkillId, (effector.GetSummon()?.GetNpcId() ?? 0));
		if (petUseSkillId is null)
			return PlayerPetOrderSkillResult.MissingPetSkillMapping(request.OrderSkillId, (effector.GetSummon()?.GetNpcId() ?? 0));

		var skillTemplate = skillTemplates.GetSkillTemplate(petUseSkillId.Value);
		if (skillTemplate is null)
			return PlayerPetOrderSkillResult.MissingSkillTemplate(request.OrderSkillId, (effector.GetSummon()?.GetNpcId() ?? 0), petUseSkillId.Value);

		var hate = request.EffectHate > 1 ? request.EffectHate : 0;
		var order = new PlayerPetSkillOrder(
			petUseSkillId.Value,
			skillTemplate.Level,
			request.EffectedObjectId,
			hate,
			request.Release);
		effector.AddPetSkillOrder(order);

		return PlayerPetOrderSkillResult.Applied(
			request.OrderSkillId,
			(effector.GetSummon()?.GetObjectId() ?? 0),
			(effector.GetSummon()?.GetNpcId() ?? 0),
			petUseSkillId.Value,
			skillTemplate.Level,
			request.EffectedObjectId,
			hate,
			request.Release,
			new SmSummonUseSkill((effector.GetSummon()?.GetObjectId() ?? 0), petUseSkillId.Value, skillTemplate.Level, request.EffectedObjectId));
	}
}

public sealed record PlayerPetOrderSkillRequest(
	int OrderSkillId,
	int EffectedObjectId,
	int EffectHate,
	bool Release);

public sealed record PlayerPetOrderSkillResult(
	PlayerPetOrderSkillStatus Status,
	int OrderSkillId,
	int PetSummonObjectId,
	int PetSummonNpcId,
	int? PetUseSkillId = null,
	int? PetUseSkillLevel = null,
	int? TargetObjectId = null,
	int Hate = 0,
	bool Release = false,
	SmSummonUseSkill? Packet = null)
{
	public static PlayerPetOrderSkillResult MissingSummon(int orderSkillId)
	{
		return new PlayerPetOrderSkillResult(PlayerPetOrderSkillStatus.MissingSummon, orderSkillId, 0, 0);
	}

	public static PlayerPetOrderSkillResult MissingEffected(int orderSkillId, int petSummonNpcId)
	{
		return new PlayerPetOrderSkillResult(PlayerPetOrderSkillStatus.MissingEffected, orderSkillId, 0, petSummonNpcId);
	}

	public static PlayerPetOrderSkillResult MissingPetSkillMapping(int orderSkillId, int petSummonNpcId)
	{
		return new PlayerPetOrderSkillResult(PlayerPetOrderSkillStatus.MissingPetSkillMapping, orderSkillId, 0, petSummonNpcId);
	}

	public static PlayerPetOrderSkillResult MissingSkillTemplate(int orderSkillId, int petSummonNpcId, int petUseSkillId)
	{
		return new PlayerPetOrderSkillResult(
			PlayerPetOrderSkillStatus.MissingSkillTemplate,
			orderSkillId,
			0,
			petSummonNpcId,
			PetUseSkillId: petUseSkillId);
	}

	public static PlayerPetOrderSkillResult Applied(
		int orderSkillId,
		int petSummonObjectId,
		int petSummonNpcId,
		int petUseSkillId,
		int petUseSkillLevel,
		int targetObjectId,
		int hate,
		bool release,
		SmSummonUseSkill packet)
	{
		return new PlayerPetOrderSkillResult(
			PlayerPetOrderSkillStatus.Applied,
			orderSkillId,
			petSummonObjectId,
			petSummonNpcId,
			petUseSkillId,
			petUseSkillLevel,
			targetObjectId,
			hate,
			release,
			packet);
	}
}

public enum PlayerPetOrderSkillStatus
{
	MissingSummon,
	MissingEffected,
	MissingPetSkillMapping,
	MissingSkillTemplate,
	Applied,
}
