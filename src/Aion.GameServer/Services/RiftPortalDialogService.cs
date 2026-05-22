using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class RiftPortalDialogService
{
	public RiftPortalDialogResult CreateDialogRequest(Player player, RiftPortalState portal)
	{
		// Java parity: controllers/RVController.onDialogRequest blocks invasion rifts unless the player's opposite race matches the destination.
		if (portal.IsInvasion && !IsOppositeRaceDestination(player.Race, portal.DestinationRace))
			return RiftPortalDialogResult.NotRequested(RiftPortalDialogStatus.InvasionRaceMismatch);

		// Java parity: controllers/RVController.onRequest asks with 904304 for vortexes and STR_ASK_PASS_BY_DIRECT_PORTAL for ordinary rifts.
		var question = portal.IsVortex
			? new SmQuestionWindow(SmQuestionWindow.VortexPortalPassConfirm, portal.MasterNpc.ObjectId, 5)
			: new SmQuestionWindow(SmQuestionWindow.DirectPortalPassConfirm, 0, 0);

		return RiftPortalDialogResult.CreateRequested(question);
	}

	private static bool IsOppositeRaceDestination(string playerRace, string destinationRace)
	{
		var oppositeRace = GetOppositeRace(playerRace);
		return oppositeRace != null
			&& string.Equals(oppositeRace, destinationRace, StringComparison.OrdinalIgnoreCase);
	}

	private static string? GetOppositeRace(string race)
	{
		if (string.Equals(race, "ELYOS", StringComparison.OrdinalIgnoreCase))
			return "ASMODIANS";
		if (string.Equals(race, "ASMODIANS", StringComparison.OrdinalIgnoreCase))
			return "ELYOS";
		return null;
	}
}

public sealed record RiftPortalDialogResult(
	bool Requested,
	RiftPortalDialogStatus Status,
	SmQuestionWindow? QuestionWindow)
{
	public static RiftPortalDialogResult CreateRequested(SmQuestionWindow questionWindow)
	{
		return new RiftPortalDialogResult(true, RiftPortalDialogStatus.Requested, questionWindow);
	}

	public static RiftPortalDialogResult NotRequested(RiftPortalDialogStatus status)
	{
		return new RiftPortalDialogResult(false, status, null);
	}
}

public enum RiftPortalDialogStatus
{
	Requested,
	UnknownPortal,
	InvasionRaceMismatch,
}
