namespace Aion.GameServer.Services;

public sealed record ItemPurificationHandlerPlan(
	ItemPurificationWorkflowPlan Workflow,
	ItemPurificationApplicationPlan Application,
	ItemPurificationPacketPlan PacketPlan);
