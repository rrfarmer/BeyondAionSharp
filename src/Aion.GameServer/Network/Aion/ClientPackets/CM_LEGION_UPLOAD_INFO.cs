using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_UPLOAD_INFO (Simple, cura). Announces an incoming custom-emblem upload (size + argb). LegionService/LegionEmblemType red-tolerated.</summary>
public class CM_LEGION_UPLOAD_INFO : AionClientPacket
{
    /// <summary>Emblem related information</summary>
    private int totalSize;
    private int alpha;
    private int red;
    private int green;
    private int blue;

    public CM_LEGION_UPLOAD_INFO(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        totalSize = ReadD();
        alpha = ReadUC();
        red = ReadUC();
        green = ReadUC();
        blue = ReadUC();
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        LegionService.GetInstance().UploadEmblemInfo(activePlayer, totalSize, alpha, red, green, blue, LegionEmblemType.CUSTOM);
    }
}
