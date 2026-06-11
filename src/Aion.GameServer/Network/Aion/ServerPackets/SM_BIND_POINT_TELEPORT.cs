using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_BIND_POINT_TELEPORT (ginho1). Bind-point teleport action (action/playerId + locId/cooldown by action).</summary>
public class SM_BIND_POINT_TELEPORT : AionServerPacket
{
    private int action, playerId, locId, cooldown;

    public SM_BIND_POINT_TELEPORT(int action, int playerId, int locId, int cooldown)
    {
        this.action = action;
        this.playerId = playerId;
        this.locId = locId;
        this.cooldown = cooldown;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(action);
        WriteD(playerId);
        switch (action)
        {
            case 1:
                WriteD(locId);
                break;
            case 3:
                WriteD(locId);
                WriteD(cooldown);
                break;
        }
    }
}
