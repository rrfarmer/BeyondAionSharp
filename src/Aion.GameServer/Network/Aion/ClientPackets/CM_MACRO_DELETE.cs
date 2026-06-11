using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Players;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MACRO_DELETE (SoulKeeper). Removes a macro by list position (subsequent macros shift down) and replies SM_MACRO_DELETED. PlayerService/SM_MACRO_RESULT red-tolerated.</summary>
public class CM_MACRO_DELETE : AionClientPacket
{
    /// <summary>Macro id that has to be deleted</summary>
    private int macroPosition;

    public CM_MACRO_DELETE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        macroPosition = ReadUC();
    }

    protected override void RunImpl()
    {
        PlayerService.RemoveMacro(GetConnection().GetActivePlayer(), macroPosition);
        SendPacket(SM_MACRO_RESULT.SM_MACRO_DELETED);
    }
}
