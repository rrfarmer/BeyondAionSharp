namespace Aion.GameServer.Model.Siege;

/// <summary>
/// Java parity: model/siege/ArtifactStatus (MrPoke). Per-instance id matches declaration ordinal → GetValue()=(int)t.
/// </summary>
public enum ArtifactStatus
{
    IDLE,
    ACTIVATION,
    CASTING,
    ACTIVATED
}

public static class ArtifactStatusExtensions
{
    public static int GetValue(this ArtifactStatus s) => (int) s;
}
