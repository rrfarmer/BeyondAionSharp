using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Players;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_MACRO_CREATE (SoulKeeper). Stores a macro at a position and replies SM_MACRO_CREATED. PlayerService/SM_MACRO_RESULT red-tolerated.</summary>
public class CM_MACRO_CREATE : AionClientPacket
{
    /// <summary>Macro number. Fist is 1, second is 2. Starting from 1, not from 0</summary>
    private int macroPosition;

    /// <summary>XML that represents the macro</summary>
    private string macroXML;

    public CM_MACRO_CREATE(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        macroPosition = ReadUC();
        macroXML = ReadS();
    }

    protected override void RunImpl()
    {
        PlayerService.AddMacro(GetConnection().GetActivePlayer(), macroPosition, macroXML);
        SendPacket(SM_MACRO_RESULT.SM_MACRO_CREATED);
    }
}
