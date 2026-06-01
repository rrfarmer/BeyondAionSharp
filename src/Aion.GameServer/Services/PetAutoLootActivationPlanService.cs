using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum PetAutoLootActivationPlanStatus
{
	MissingPet,
	BlockedMissingLootFunction,
	BlockedFreeForAllLoot,
	DisabledNoSideEffects
}

public enum CmPetAutoLootActivationCompositionPlanStatus
{
	NotFoodAction,
	NotAutoLootAction,
	ActivationPlanCreated
}

public enum PetAutoLootActivationStepKind
{
	ValidateLootFunction,
	ValidateLootRule,
	WriteAuditLog,
	SendFreeForAllMessage,
	SendEnableMessage,
	SetLootingState,
	SendAutoLootPacket
}

public sealed record PetAutoLootActivationInput(
	bool PetPresent,
	bool Activate,
	bool PetHasLootFunction,
	bool IsFreeForAllLootRule,
	int? PetObjectId,
	int MasterObjectId,
	string? PetName);

public sealed record CmPetAutoLootActivationCompositionContext(
	bool PetPresent,
	bool PetHasLootFunction,
	bool IsFreeForAllLootRule,
	int? PetObjectId,
	int MasterObjectId,
	string? PetName);

public sealed record PetAutoLootActivationStepPlan(
	PetAutoLootActivationStepKind Kind,
	bool WouldRun,
	bool DidRun,
	string JavaSource);

public sealed record PetAutoLootActivationPlan(
	PetAutoLootActivationPlanStatus Status,
	PetAutoLootActivationInput Input,
	IReadOnlyList<PetAutoLootActivationStepPlan> Steps,
	bool WouldSetLootingState,
	bool DidSetLootingState,
	bool TargetLootingState,
	bool WouldSendPacket,
	bool DidSendPacket,
	SmPet? PacketIntent,
	bool WouldWriteAuditLog,
	bool DidWriteAuditLog,
	string? AuditMessage,
	bool WouldSendSystemMessage,
	bool DidSendSystemMessage,
	int? SystemMessageId,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public sealed record CmPetAutoLootActivationCompositionPlan(
	CmPet Packet,
	CmPetAutoLootActivationCompositionPlanStatus Status,
	CmPetAutoLootActivationCompositionContext Context,
	bool ParsedActivationFlag,
	PetAutoLootActivationPlan? ActivationPlan,
	string JavaSource,
	bool IsLive);

public static class CmPetAutoLootActivationCompositionPlanService
{
	public static CmPetAutoLootActivationCompositionPlan CreateDisabledPlan(
		CmPet packet,
		CmPetAutoLootActivationCompositionContext context)
	{
		// Java parity: CM_PET.runImpl routes FOOD actionType 3 to PetService.activateLoot.
		if (packet.Action != PetAction.Food)
			return Terminal(packet, context, CmPetAutoLootActivationCompositionPlanStatus.NotFoodAction,
				"CM_PET.runImpl only reaches auto-loot activation inside the FOOD action branch");

		if (packet.ActionType != 3)
			return Terminal(packet, context, CmPetAutoLootActivationCompositionPlanStatus.NotAutoLootAction,
				"CM_PET.runImpl FOOD actionType 3 is the auto-loot activation branch; other FOOD action types route elsewhere");

		var activationInput = new PetAutoLootActivationInput(
			context.PetPresent,
			Activate: packet.ActivateSpecialFunction != 0,
			context.PetHasLootFunction,
			context.IsFreeForAllLootRule,
			context.PetObjectId,
			context.MasterObjectId,
			context.PetName);
		var activationPlan = PetAutoLootActivationPlanService.CreateDisabledPlan(activationInput);
		return new CmPetAutoLootActivationCompositionPlan(
			packet,
			CmPetAutoLootActivationCompositionPlanStatus.ActivationPlanCreated,
			context,
			activationInput.Activate,
			activationPlan,
			"CM_PET.runImpl FOOD actionType 3 -> PetService.activateLoot(pet, activateSpecialFunction != 0), with live side effects disabled",
			IsLive: false);
	}

	private static CmPetAutoLootActivationCompositionPlan Terminal(
		CmPet packet,
		CmPetAutoLootActivationCompositionContext context,
		CmPetAutoLootActivationCompositionPlanStatus status,
		string javaSource) =>
		new(packet, status, context, ParsedActivationFlag: false, ActivationPlan: null, javaSource, IsLive: false);
}

public static class PetAutoLootActivationPlanService
{
	public const int AutoLootEnabledMessageId = 1400876;
	public const int FreeForAllLootRuleMessageId = 1400878;

	public static PetAutoLootActivationPlan CreateDisabledPlan(PetAutoLootActivationInput input)
	{
		// Java parity: services/toypet/PetService.activateLoot.
		// CM_PET.runImpl already returns before this service when player.getPet() is null.
		if (!input.PetPresent)
			return Terminal(PetAutoLootActivationPlanStatus.MissingPet, input,
				"CM_PET.runImpl FOOD actionType 3 -> if (pet == null) return before PetService.activateLoot");

		if (input.Activate && !input.PetHasLootFunction)
		{
			var auditPetName = input.PetName ?? input.PetObjectId?.ToString() ?? "<unknown>";
			var auditMessage = $"tried to enable auto-loot on non-looting {auditPetName}";
			return new PetAutoLootActivationPlan(
				PetAutoLootActivationPlanStatus.BlockedMissingLootFunction,
				input,
				[Disabled(PetAutoLootActivationStepKind.WriteAuditLog, "PetService.activateLoot -> activate && !containsFunction(LOOT) -> AuditLogger.log and return")],
				WouldSetLootingState: false,
				DidSetLootingState: false,
				TargetLootingState: input.Activate,
				WouldSendPacket: false,
				DidSendPacket: false,
				PacketIntent: null,
				WouldWriteAuditLog: true,
				DidWriteAuditLog: false,
				auditMessage,
				WouldSendSystemMessage: false,
				DidSendSystemMessage: false,
				SystemMessageId: null,
				ShouldDispatchLiveSideEffects: false,
				"PetService.activateLoot blocked by missing LOOT function; audit is recorded without live logging",
				IsLive: false);
		}

		if (input.Activate && input.IsFreeForAllLootRule)
		{
			return new PetAutoLootActivationPlan(
				PetAutoLootActivationPlanStatus.BlockedFreeForAllLoot,
				input,
				[Disabled(PetAutoLootActivationStepKind.SendFreeForAllMessage, "PetService.activateLoot -> team loot rule FREEFORALL -> STR_MSG_LOOTING_PET_MESSAGE03 and return")],
				WouldSetLootingState: false,
				DidSetLootingState: false,
				TargetLootingState: input.Activate,
				WouldSendPacket: false,
				DidSendPacket: false,
				PacketIntent: null,
				WouldWriteAuditLog: false,
				DidWriteAuditLog: false,
				AuditMessage: null,
				WouldSendSystemMessage: true,
				DidSendSystemMessage: false,
				SystemMessageId: FreeForAllLootRuleMessageId,
				ShouldDispatchLiveSideEffects: false,
				"PetService.activateLoot blocked by FREEFORALL loot rule; message is recorded without live send",
				IsLive: false);
		}

		var packet = SmPet.SpecialFunction(new SmPetSpecialFunctionSnapshot(PetSpecialFunction.AutoLoot, input.Activate));
		var steps = input.Activate
			? new[]
			{
				Disabled(PetAutoLootActivationStepKind.ValidateLootFunction, "PetService.activateLoot -> activate true and containsFunction(LOOT) guard passed"),
				Disabled(PetAutoLootActivationStepKind.ValidateLootRule, "PetService.activateLoot -> non-FREEFORALL team loot rule guard passed"),
				Disabled(PetAutoLootActivationStepKind.SendEnableMessage, "PetService.activateLoot -> STR_MSG_LOOTING_PET_MESSAGE01"),
				Disabled(PetAutoLootActivationStepKind.SetLootingState, "PetService.activateLoot -> pet.getCommonData().setIsLooting(activate)"),
				Disabled(PetAutoLootActivationStepKind.SendAutoLootPacket, "PetService.activateLoot -> send SM_PET(PetSpecialFunction.AUTOLOOT, activate)"),
			}
			:
			[
				Disabled(PetAutoLootActivationStepKind.SetLootingState, "PetService.activateLoot -> deactivate skips LOOT/FREEFORALL guards and sets isLooting false"),
				Disabled(PetAutoLootActivationStepKind.SendAutoLootPacket, "PetService.activateLoot -> send SM_PET(PetSpecialFunction.AUTOLOOT, false)"),
			];

		return new PetAutoLootActivationPlan(
			PetAutoLootActivationPlanStatus.DisabledNoSideEffects,
			input,
			steps,
			WouldSetLootingState: true,
			DidSetLootingState: false,
			TargetLootingState: input.Activate,
			WouldSendPacket: true,
			DidSendPacket: false,
			packet,
			WouldWriteAuditLog: false,
			DidWriteAuditLog: false,
			AuditMessage: null,
			WouldSendSystemMessage: input.Activate,
			DidSendSystemMessage: false,
			SystemMessageId: input.Activate ? AutoLootEnabledMessageId : null,
			ShouldDispatchLiveSideEffects: false,
			"PetService.activateLoot live mutation/send sequence recorded with side effects disabled",
			IsLive: false);
	}

	private static PetAutoLootActivationPlan Terminal(
		PetAutoLootActivationPlanStatus status,
		PetAutoLootActivationInput input,
		string javaSource) =>
		new(
			status,
			input,
			Steps: Array.Empty<PetAutoLootActivationStepPlan>(),
			WouldSetLootingState: false,
			DidSetLootingState: false,
			TargetLootingState: input.Activate,
			WouldSendPacket: false,
			DidSendPacket: false,
			PacketIntent: null,
			WouldWriteAuditLog: false,
			DidWriteAuditLog: false,
			AuditMessage: null,
			WouldSendSystemMessage: false,
			DidSendSystemMessage: false,
			SystemMessageId: null,
			ShouldDispatchLiveSideEffects: false,
			javaSource,
			IsLive: false);

	private static PetAutoLootActivationStepPlan Disabled(PetAutoLootActivationStepKind kind, string javaSource) =>
		new(kind, WouldRun: true, DidRun: false, javaSource);
}
