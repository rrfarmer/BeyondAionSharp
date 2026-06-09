namespace Aion.GameServer.Model.Team.Legion;

/// <summary>Java parity: model/team/legion/LegionEmblemType.</summary>
public enum LegionEmblemType : byte
{
    DEFAULT = 0x00,
    CUSTOM = 0x80,
}

public static class LegionEmblemTypeExtensions
{
    public static byte GetValue(this LegionEmblemType type)
    {
        return (byte)type;
    }
}
