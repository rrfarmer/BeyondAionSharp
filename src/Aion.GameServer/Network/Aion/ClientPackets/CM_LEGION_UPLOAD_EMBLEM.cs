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
    private byte[] data;

    public CM_LEGION_UPLOAD_EMBLEM(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        data = ReadB(ReadD());
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        // Upstream dropped this null check when it added the membership one. Kept: this port reaches the packet layer
        // from in-process transports too, and IsLegionMember would throw on a null player instead of refusing.
        if (player == null)
            return;
        if (!player.IsLegionMember())
        {
            global::Aion.GameServer.Utils.Audit.AuditLogger.Log(player, "uploaded a legion emblem without being member of a legion");
            return;
        }

        if (data != null && data.Length > 0)
        {
            LegionService.GetInstance().UploadEmblemData(player, data);
        }
    }
}
