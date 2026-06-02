using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.World;

public interface IInstanceLifecycleHandler
{
	void OnInstanceCreate(WorldMapInstanceRuntimeState instance);

	void OnLeaveInstance(Player player)
	{
	}

	void OnInstanceDestroy(WorldMapInstanceRuntimeState instance)
	{
	}
}

public sealed class GeneralInstanceLifecycleHandler : IInstanceLifecycleHandler
{
	public static readonly GeneralInstanceLifecycleHandler Instance = new();

	private GeneralInstanceLifecycleHandler()
	{
	}

	public void OnInstanceCreate(WorldMapInstanceRuntimeState instance)
	{
		// Java parity: instance/handlers/GeneralInstanceHandler.onInstanceCreate is a no-op.
	}

	public void OnInstanceDestroy(WorldMapInstanceRuntimeState instance)
	{
		// Java parity: instance/handlers/GeneralInstanceHandler.onInstanceDestroy is a no-op.
	}

	public void OnLeaveInstance(Player player)
	{
		// Java parity: instance/handlers/GeneralInstanceHandler.onLeaveInstance is a no-op.
	}
}
