using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RENAME (Rhys2002). Notifies the client of a character or legion name change (renames everywhere). Player/Legion red-tolerated.</summary>
public class SM_RENAME : AionServerPacket
{
    private readonly bool isLegion;
    private readonly int playerOrLegionId;
    private readonly string oldName;
    private readonly string newName;

    public SM_RENAME(Player player, string oldName)
        : this(false, player.GetObjectId(), oldName, player.GetName())
    {
    }

    public SM_RENAME(Legion legion, string oldName)
        : this(true, legion.GetObjectId(), oldName, legion.GetName())
    {
    }

    private SM_RENAME(bool isLegion, int playerOrLegionId, string oldName, string newName)
    {
        this.isLegion = isLegion;
        this.playerOrLegionId = playerOrLegionId;
        this.oldName = oldName;
        this.newName = newName;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(isLegion ? 1 : 0);
        WriteD(0); // error code 3: name in use, 4: invalid name, 6: legion name in use, 7: invalid legion name, 8: legion holds keep, 9: legion disbanding
        WriteD(playerOrLegionId);
        WriteS(oldName);
        WriteS(newName);
    }
}
