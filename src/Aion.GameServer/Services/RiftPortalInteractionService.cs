using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.World;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class RiftPortalInteractionService
{
	private readonly RiftService _riftService;
	private readonly RiftPortalDialogService _dialogService;
	private readonly RiftPortalUseService _useService;
	private readonly RiftInformerService? _informerService;
	private readonly VortexLocationService? _vortexLocationService;
	private readonly GameWorld? _world;
	private readonly Func<Player, int, bool>? _isKnownNpc;
	private readonly Func<RiftPortalState, WorldPosition?>? _vortexDestinationResolver;

	public RiftPortalInteractionService(
		RiftService riftService,
		RiftPortalDialogService? dialogService = null,
		RiftPortalUseService? useService = null,
		RiftInformerService? informerService = null,
		VortexLocationService? vortexLocationService = null,
		GameWorld? world = null,
		Func<Player, int, bool>? isKnownNpc = null,
		Func<RiftPortalState, WorldPosition?>? vortexDestinationResolver = null)
	{
		_riftService = riftService;
		_dialogService = dialogService ?? new RiftPortalDialogService();
		_useService = useService ?? new RiftPortalUseService();
		_informerService = informerService;
		_vortexLocationService = vortexLocationService;
		_world = world;
		_isKnownNpc = isKnownNpc;
		_vortexDestinationResolver = vortexDestinationResolver;
	}

	public RiftPortalDialogResult RequestDialog(Player player, int targetObjectId)
	{
		// Java parity: network/aion/clientpackets/CM_SHOW_DIALOG.runImpl delegates targeted NPCs to controller.onDialogRequest.
		if (!_riftService.TryGetPortalByMasterObjectId(targetObjectId, out var portal) || portal == null)
			return RiftPortalDialogResult.NotRequested(RiftPortalDialogStatus.UnknownPortal);
		if (!IsVisiblePortalMaster(player, targetObjectId, portal))
			return RiftPortalDialogResult.NotRequested(RiftPortalDialogStatus.UnknownPortal);

		var result = _dialogService.CreateDialogRequest(player, portal);
		if (result.Requested && result.QuestionWindow != null)
			player.PendingRiftPortalRequest = new PendingRiftPortalRequest(targetObjectId, result.QuestionWindow.Code);
		return result;
	}

	public async Task<RiftPortalQuestionResponseResult> RespondAsync(
		Player player,
		int questionId,
		byte response,
		Func<GameServerPacket, Task>? sendResponderPacketAsync = null)
	{
		// Java parity: CM_QUESTION_RESPONSE executes the matching ResponseRequester handler and removes it.
		var request = player.PendingRiftPortalRequest;
		if (request == null || request.QuestionId != questionId)
			return RiftPortalQuestionResponseResult.NotHandled(RiftPortalQuestionResponseStatus.NoPendingRequest);

		player.PendingRiftPortalRequest = null;
		if (response == 0)
			return RiftPortalQuestionResponseResult.Declined();

		if (!_riftService.TryGetPortalByMasterObjectId(request.PortalObjectId, out var portal) || portal == null)
			return RiftPortalQuestionResponseResult.NotHandled(RiftPortalQuestionResponseStatus.UnknownPortal);

		var useResult = _useService.AcceptPortal(player, portal, ResolveVortexDestination);
		if (!useResult.Accepted)
			return RiftPortalQuestionResponseResult.Rejected(useResult.Status);

		var removedTeamMembership = PlayerTeamMembership.None;
		var vortexOpenNoticeRequested = false;
		var vortexOpenNoticeSent = false;
		if (portal.IsVortex)
		{
			// Java parity: RVController vortex accept removes the responder from group/alliance before teleport notice and syncPassed(true).
			if (player.IsInTeam)
				removedTeamMembership = player.RemoveCurrentTeam();

			vortexOpenNoticeRequested = true;
			if (sendResponderPacketAsync != null)
			{
				await sendResponderPacketAsync(SmSystemMessage.InvasionDirectPortalOpenNotice());
				vortexOpenNoticeSent = true;
			}
		}

		var sent = _informerService == null
			? 0
			: await _informerService.SendRiftInfoAsync(GetWorldsList(portal));
		return RiftPortalQuestionResponseResult.CreateAccepted(
			useResult,
			sent,
			removedTeamMembership,
			vortexOpenNoticeRequested,
			vortexOpenNoticeSent);
	}

	private static IReadOnlyList<int> GetWorldsList(RiftPortalState portal)
	{
		// Java parity: controllers/RVController.getWorldsList returns master and slave worlds for master controllers.
		return [portal.MasterNpc.Position.WorldId, portal.SlaveNpc.SpawnLocation.WorldId];
	}

	private bool IsVisiblePortalMaster(Player player, int targetObjectId, RiftPortalState portal)
	{
		// Java parity: CM_SHOW_DIALOG only dispatches when the target object is still an Npc in the player's known list.
		if (_isKnownNpc?.Invoke(player, targetObjectId) == false)
			return false;
		if (_world == null)
			return true;
		if (!_world.TryGetObject(targetObjectId, out var target) || target == null)
			return false;
		return ReferenceEquals(target, portal.MasterNpc)
			|| target is WorldNpc npc
			&& npc.ObjectId == portal.MasterNpc.ObjectId
			&& npc.TemplateId == portal.MasterNpc.TemplateId
			&& string.Equals(npc.Anchor, portal.MasterNpc.Anchor, StringComparison.Ordinal)
			&& npc.SpawnLocation.Equals(portal.MasterNpc.SpawnLocation);
	}

	private WorldPosition? ResolveVortexDestination(RiftPortalState portal)
	{
		return _vortexDestinationResolver?.Invoke(portal)
			?? _vortexLocationService?.ResolveStartPoint(portal);
	}
}

public sealed record RiftPortalQuestionResponseResult(
	bool Handled,
	bool Accepted,
	RiftPortalQuestionResponseStatus Status,
	RiftPortalUseStatus? UseStatus,
	int RefreshPacketsSent,
	WorldPosition? Destination,
	PlayerTeamMembership RemovedTeamMembership,
	bool VortexOpenNoticeRequested,
	bool VortexOpenNoticeSent)
{
	public static RiftPortalQuestionResponseResult CreateAccepted(
		RiftPortalUseResult useResult,
		int refreshPacketsSent,
		PlayerTeamMembership removedTeamMembership,
		bool vortexOpenNoticeRequested,
		bool vortexOpenNoticeSent)
	{
		return new RiftPortalQuestionResponseResult(
			Handled: true,
			Accepted: true,
			RiftPortalQuestionResponseStatus.Accepted,
			RiftPortalUseStatus.Accepted,
			refreshPacketsSent,
			useResult.Destination,
			removedTeamMembership,
			vortexOpenNoticeRequested,
			vortexOpenNoticeSent);
	}

	public static RiftPortalQuestionResponseResult Rejected(RiftPortalUseStatus useStatus)
	{
		return new RiftPortalQuestionResponseResult(
			Handled: true,
			Accepted: false,
			RiftPortalQuestionResponseStatus.UseRejected,
			useStatus,
			RefreshPacketsSent: 0,
			Destination: null,
			RemovedTeamMembership: PlayerTeamMembership.None,
			VortexOpenNoticeRequested: false,
			VortexOpenNoticeSent: false);
	}

	public static RiftPortalQuestionResponseResult Declined()
	{
		return new RiftPortalQuestionResponseResult(
			Handled: true,
			Accepted: false,
			RiftPortalQuestionResponseStatus.Declined,
			UseStatus: null,
			RefreshPacketsSent: 0,
			Destination: null,
			RemovedTeamMembership: PlayerTeamMembership.None,
			VortexOpenNoticeRequested: false,
			VortexOpenNoticeSent: false);
	}

	public static RiftPortalQuestionResponseResult NotHandled(RiftPortalQuestionResponseStatus status)
	{
		return new RiftPortalQuestionResponseResult(
			Handled: false,
			Accepted: false,
			status,
			UseStatus: null,
			RefreshPacketsSent: 0,
			Destination: null,
			RemovedTeamMembership: PlayerTeamMembership.None,
			VortexOpenNoticeRequested: false,
			VortexOpenNoticeSent: false);
	}
}

public enum RiftPortalQuestionResponseStatus
{
	Accepted,
	NoPendingRequest,
	Declined,
	UnknownPortal,
	UseRejected,
}
