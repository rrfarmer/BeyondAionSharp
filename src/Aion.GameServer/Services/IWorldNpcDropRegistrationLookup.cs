namespace Aion.GameServer.Services;

public interface IWorldNpcDropRegistrationLookup
{
	// Java parity: services/drop/DropRegistrationService.getCurrentDropMap lookup used by RespawnService.scheduleDecayTask.
	bool HasRegisteredDrops(int npcObjectId);
}
