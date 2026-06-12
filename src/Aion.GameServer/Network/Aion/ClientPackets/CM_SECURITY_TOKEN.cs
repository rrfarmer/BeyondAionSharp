using System.Collections.Generic;
using System.Text;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_SECURITY_TOKEN (ginho1). Returns (and lazily generates) the account security token. SecurityTokenService red-tolerated.</summary>
public class CM_SECURITY_TOKEN : AionClientPacket
{
    public CM_SECURITY_TOKEN(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {

    }

    protected override void RunImpl()
    {
        Account account = GetConnection().GetAccount();
        if (account == null)
            return;
        if (account.GetSecurityToken().Equals(""))
            SecurityTokenService.GenerateToken(account);
        SendPacket(new SM_SECURITY_TOKEN(Encoding.UTF8.GetBytes(account.GetSecurityToken())));
    }
}
