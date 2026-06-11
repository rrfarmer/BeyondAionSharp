namespace Aion.GameServer.Model.Siege;

/// <summary>Java parity: model/siege/ArtifactStatus (MrPoke). Explicit ids match declaration order, so
/// getValue() == (int) cast; exposed via GetValue() for faithful call sites.</summary>
public enum ArtifactStatus
{
    IDLE = 0,
    ACTIVATION = 1,
    CASTING = 2,
    ACTIVATED = 3,
}

public static class ArtifactStatusExtensions
{
    public static int GetValue(this ArtifactStatus status) => (int)status;
}
