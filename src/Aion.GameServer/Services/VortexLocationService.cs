using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public sealed class VortexLocationService
{
	private const int TheobomosWorldId = 210060000;
	private const int BrusthoninWorldId = 220050000;
	private const int KaisinelVortexMasterNpcId = 831141;
	private readonly GameServerRuntimeContext _runtimeContext;

	public VortexLocationService(GameServerRuntimeContext runtimeContext)
	{
		_runtimeContext = runtimeContext;
	}

	public VortexLocationSummary? GetLocationByRift(int npcId)
	{
		// Java parity: services/VortexService.getLocationByRift maps Kaisinel's master NPC to Brusthonin, all other vortex rifts to Theobomos.
		var worldId = npcId == KaisinelVortexMasterNpcId ? BrusthoninWorldId : TheobomosWorldId;
		return GetLocationByWorld(worldId);
	}

	public VortexLocationSummary? GetLocationByWorld(int worldId)
	{
		// Java parity: services/VortexService.getLocationByWorld returns id 0 for Theobomos and id 1 for Brusthonin.
		var table = _runtimeContext.DataManager?.StaticData.VortexLocations;
		return worldId switch
		{
			TheobomosWorldId => table?.GetLocation(0),
			BrusthoninWorldId => table?.GetLocation(1),
			_ => null,
		};
	}

	public WorldPosition? GetStartPointByRift(int npcId)
	{
		return GetLocationByRift(npcId)?.StartPoint;
	}
}
