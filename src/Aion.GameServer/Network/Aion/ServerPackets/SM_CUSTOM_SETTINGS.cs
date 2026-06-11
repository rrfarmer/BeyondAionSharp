using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_CUSTOM_SETTINGS (Sweetkr). Player display/deny settings (HIDE_* bitmask). Player red-tolerated.</summary>
public class SM_CUSTOM_SETTINGS : AionServerPacket
{
    public const int HIDE_LEGION_CLOAK = 1;
    public const int HIDE_LEGION_CLOAK_BY_WEAPON_PRIORITY = 2;
    public const int HIDE_HELMET = 4;
    public const int HIDE_PLUME = 8;

    private int objectId;
    private int unk = 0;
    private int display; // bitmask of HIDE_* values
    private int deny;

    public SM_CUSTOM_SETTINGS(Player player)
        : this(player.GetObjectId(), 1, player.GetPlayerSettings().GetDisplay(), player.GetPlayerSettings().GetDeny())
    {
    }

    public SM_CUSTOM_SETTINGS(int objectId, int unk, int display, int deny)
    {
        this.objectId = objectId;
        this.display = display;
        this.deny = deny;
        this.unk = unk;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(objectId);
        WriteC(unk); // unk
        WriteH(display);
        WriteH(deny);
    }
}
