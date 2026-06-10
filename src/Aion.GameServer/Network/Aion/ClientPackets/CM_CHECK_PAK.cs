using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Player;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.Audit;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHECK_PAK (ginho1). Reports data-pak integrity status; audits modified paks. AuditLogger red-tolerated.</summary>
public class CM_CHECK_PAK : AionClientPacket
{
    private byte unk;
    private string pakStatus;

    public CM_CHECK_PAK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        unk = ReadC(); // 2
        pakStatus = ReadS();
    }

    protected override void RunImpl()
    {
        Player player = GetConnection().GetActivePlayer();
        if (pakStatus.Length != 0 && !pakStatus.EndsWith("[1:OK]") && !pakStatus.Contains("File not found"))
            AuditLogger.Log(player, "using modified data pak: " + pakStatus);
    }
}
