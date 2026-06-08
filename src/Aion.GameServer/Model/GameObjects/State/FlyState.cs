namespace Aion.GameServer.Model.GameObjects.State;

/// <summary>Flight state (none/flying/gliding). Java parity: model/gameobjects/state/FlyState.</summary>
public enum FlyState
{
    NONE = 0,
    FLYING = 1,
    GLIDING = 1 << 1,
}

public static class FlyStateExtensions
{
    // Java parity: getId()
    public static int GetId(this FlyState state) => (int)state;
}
