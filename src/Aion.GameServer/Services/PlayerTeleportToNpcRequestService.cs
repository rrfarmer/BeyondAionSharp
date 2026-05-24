using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class PlayerTeleportToNpcRequestService
{
	public TeleportToNpcRequestResult SendTeleportRequest(
		Player player,
		int npcId,
		NpcTemplateTable npcTemplates)
	{
		// Java parity: services/teleport/TeleportService.sendTeleportRequest(Player,int).
		var template = npcTemplates.GetNpcTemplate(npcId);
		var request = new PendingTeleportToNpcRequest(npcId, template?.Name ?? npcId.ToString());

		if (!player.ResponseRequester.PutRequest(
			SmQuestionWindow.TeleportToNpcConfirm,
			new QuestionResponseRequest(player.ObjectId, QuestionResponseRequestKind.TeleportToNpc, request)))
		{
			return TeleportToNpcRequestResult.DuplicateRequest(npcId);
		}

		return TeleportToNpcRequestResult.Requested(
			request,
			new SmQuestionWindow(SmQuestionWindow.TeleportToNpcConfirm, 0, 0, request.NpcName));
	}

	public TeleportToNpcResponseResult HandleResponse(
		Player responder,
		int questionId,
		int response,
		NpcSpawnTable npcSpawns,
		NpcTemplateTable npcTemplates)
	{
		// Java parity: CM_QUESTION_RESPONSE delegates to ResponseRequester.respond; accepting runs
		// TeleportService.teleportToNpc(responder,npcId), while denial only consumes the handler.
		if (questionId != SmQuestionWindow.TeleportToNpcConfirm)
			return TeleportToNpcResponseResult.Ignored();

		var dispatch = responder.ResponseRequester.Respond(questionId, response);
		if (dispatch?.Request.Kind != QuestionResponseRequestKind.TeleportToNpc)
			return TeleportToNpcResponseResult.MissingRequest();

		var request = dispatch.Request.Payload as PendingTeleportToNpcRequest;
		if (request == null)
			return TeleportToNpcResponseResult.MissingRequest();

		if (!dispatch.Accepted)
			return TeleportToNpcResponseResult.Denied(request);

		var destination = CreateDestination(responder, request.NpcId, npcSpawns, npcTemplates);
		if (destination == null)
			return TeleportToNpcResponseResult.NoSpawnFound(request);

		var teleport = PlayerTeleportService.TeleportWithinSameInstance(responder, destination.Destination);
		return TeleportToNpcResponseResult.Accepted(request, destination, teleport);
	}

	private static ResolvedTeleportToNpcDestination? CreateDestination(
		Player player,
		int npcId,
		NpcSpawnTable npcSpawns,
		NpcTemplateTable npcTemplates)
	{
		// Java parity: TeleportService.teleportToNpc resolves the spawn lazily after accept.
		var spawn = npcSpawns.GetFirstSpawnByNpcId(player.Position.WorldId, npcId);
		if (spawn == null)
			return null;

		var template = npcTemplates.GetNpcTemplate(npcId);
		var npcRadius = template?.BoundRadius > 0 ? template.BoundRadius : 1f;
		var angleRadians = Math.PI / 180d * ConvertHeadingToAngle(spawn.Heading);
		var x = spawn.X + (float)Math.Cos(angleRadians) * (1f + npcRadius);
		var y = spawn.Y + (float)Math.Sin(angleRadians) * (1f + npcRadius);
		var z = spawn.Z + 0.5f;
		var heading = (byte)(spawn.Heading >= 60 ? spawn.Heading - 60 : spawn.Heading + 60);
		var destination = new WorldPosition(
			spawn.MapId,
			x,
			y,
			z,
			heading,
			spawn.MapId == player.Position.WorldId ? player.Position.InstanceId : 1);

		return new ResolvedTeleportToNpcDestination(
			new WorldPosition(spawn.MapId, spawn.X, spawn.Y, spawn.Z, spawn.Heading),
			npcRadius,
			destination);
	}

	private static float ConvertHeadingToAngle(byte heading)
	{
		// Java parity: utils/PositionUtil.convertHeadingToAngle(byte) returns normalizeAngle(heading * 3f).
		var angle = heading * 3f % 360f;
		return angle < 0 ? angle + 360f : angle;
	}
}

public sealed record PendingTeleportToNpcRequest(
	int NpcId,
	string NpcName);

public sealed record ResolvedTeleportToNpcDestination(
	WorldPosition SpawnPosition,
	float NpcRadius,
	WorldPosition Destination);

public sealed record TeleportToNpcRequestResult(
	TeleportToNpcRequestStatus Status,
	PendingTeleportToNpcRequest? Request,
	SmQuestionWindow? QuestionWindow,
	int NpcId)
{
	public static TeleportToNpcRequestResult Requested(
		PendingTeleportToNpcRequest request,
		SmQuestionWindow questionWindow)
	{
		return new TeleportToNpcRequestResult(
			TeleportToNpcRequestStatus.Requested,
			request,
			questionWindow,
			request.NpcId);
	}

	public static TeleportToNpcRequestResult DuplicateRequest(int npcId)
	{
		return new TeleportToNpcRequestResult(
			TeleportToNpcRequestStatus.DuplicateRequest,
			null,
			null,
			npcId);
	}

}

public enum TeleportToNpcRequestStatus
{
	Requested,
	DuplicateRequest,
}

public sealed record TeleportToNpcResponseResult(
	TeleportToNpcResponseStatus Status,
	PendingTeleportToNpcRequest? Request,
	ResolvedTeleportToNpcDestination? ResolvedDestination,
	PlayerTeleportResult? Teleport)
{
	public static TeleportToNpcResponseResult Ignored()
	{
		return new TeleportToNpcResponseResult(TeleportToNpcResponseStatus.Ignored, null, null, null);
	}

	public static TeleportToNpcResponseResult MissingRequest()
	{
		return new TeleportToNpcResponseResult(TeleportToNpcResponseStatus.MissingRequest, null, null, null);
	}

	public static TeleportToNpcResponseResult Denied(PendingTeleportToNpcRequest request)
	{
		return new TeleportToNpcResponseResult(TeleportToNpcResponseStatus.Denied, request, null, null);
	}

	public static TeleportToNpcResponseResult NoSpawnFound(PendingTeleportToNpcRequest request)
	{
		return new TeleportToNpcResponseResult(TeleportToNpcResponseStatus.NoSpawnFound, request, null, null);
	}

	public static TeleportToNpcResponseResult Accepted(
		PendingTeleportToNpcRequest request,
		ResolvedTeleportToNpcDestination destination,
		PlayerTeleportResult teleport)
	{
		return new TeleportToNpcResponseResult(
			TeleportToNpcResponseStatus.Accepted,
			request,
			destination,
			teleport);
	}
}

public enum TeleportToNpcResponseStatus
{
	Ignored,
	MissingRequest,
	Denied,
	NoSpawnFound,
	Accepted,
}
