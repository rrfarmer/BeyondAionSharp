namespace Aion.GameServer.World;

public interface IInstanceLifecycleHandler
{
	void OnInstanceCreate(WorldMapInstanceRuntimeState instance);
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
}
