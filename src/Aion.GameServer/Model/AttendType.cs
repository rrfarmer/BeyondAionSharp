namespace Aion.GameServer.Model;

/// <summary>Attendance reward type. Java parity: model/AttendType (@XmlEnum).</summary>
public enum AttendType
{
    DAILY = 0,
    ANNIVERSARY = 1,
    CUMULATIVE = 2,
}

public static class AttendTypeExtensions
{
    // Java parity: getId()
    public static int GetId(this AttendType type) => (int)type;
}
