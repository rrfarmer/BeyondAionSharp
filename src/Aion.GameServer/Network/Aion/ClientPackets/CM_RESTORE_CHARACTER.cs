using System.Collections.Generic;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_RESTORE_CHARACTER (-Nemesiss-). Client requests cancellation of a pending character deletion. PlayerService/SM_RESTORE_CHARACTER red-tolerated.</summary>
public class CM_RESTORE_CHARACTER : AionClientPacket
{
    /// <summary>PlayOk2 - we dont care...</summary>
    private int playOk2;

    /// <summary>ObjectId of character that deletion should be canceled</summary>
    private int chaOid;

    public CM_RESTORE_CHARACTER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        playOk2 = ReadD();
        chaOid = ReadD();
    }

    protected override void RunImpl()
    {
        Account account = GetConnection().GetAccount();
        PlayerAccountData pad = account.GetPlayerAccountData(chaOid);

        bool success = pad != null && PlayerService.CancelPlayerDeletion(pad);
        SendPacket(new SM_RESTORE_CHARACTER(chaOid, success));
    }
}
