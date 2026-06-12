using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class WorldVisibility
{
	public const float DefaultVisibleDistance = 95f;

	public static bool IsVisibleTo(Player player, WorldPosition sourcePosition)
	{
		// Java parity: world/knownlist/KnownList.isInRange using VisibleObject.getVisibleDistance default 95m.
		var playerPosition = player.GetPosition();
		if (playerPosition.WorldId != sourcePosition.WorldId)
			return false;

		var deltaX = playerPosition.X - sourcePosition.X;
		var deltaY = playerPosition.Y - sourcePosition.Y;
		var deltaZ = playerPosition.Z - sourcePosition.Z;
		return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= DefaultVisibleDistance * DefaultVisibleDistance;
	}
}
