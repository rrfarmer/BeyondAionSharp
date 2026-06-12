using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_LEGION_UPLOAD_EMBLEM (Simple). Uploads a chunk of custom emblem image data. LegionService red-tolerated.</summary>
public class CM_LEGION_UPLOAD_EMBLEM : AionClientPacket
{
    /// <summary>Emblem related information</summary>
    private int size;
    private byte[] data;

    public CM_LEGION_UPLOAD_EMBLEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        size = ReadD();
        data = new byte[size];
        data = ReadB(size);
    }

    protected override void RunImpl()
    {
        Player activePlayer = GetConnection().GetActivePlayer();
        if (activePlayer == null)
            return;

        if (data != null && data.Length > 0)
        {
            LegionService.GetInstance().UploadEmblemData(GetConnection().GetActivePlayer(), size, data);
        }
    }
}
