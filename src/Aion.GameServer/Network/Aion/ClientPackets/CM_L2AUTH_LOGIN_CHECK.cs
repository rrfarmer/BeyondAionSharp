using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.LoginServer;
using State = global::Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.ClientPackets;

/// <summary>Java parity: network/aion/clientpackets/CM_L2AUTH_LOGIN_CHECK (-Nemesiss-). Client authenticates with accountId + session-key parts; validated against the login server. LoginServer red-tolerated.</summary>
// TODO: L2AUTH? Really? :O
public class CM_L2AUTH_LOGIN_CHECK : AionClientPacket
{
    /// <summary>playOk2 is part of session key - its used for security purposes we will check if this is the key what login server sends.</summary>
    private int playOk2;
    /// <summary>playOk1 is part of session key - its used for security purposes we will check if this is the key what login server sends.</summary>
    private int playOk1;
    /// <summary>accountId is part of session key - its used for authentication we will check if this accountId is matching any waiting account login server side and check if rest of session key is ok.</summary>
    private int accountId;
    /// <summary>loginOk is part of session key - its used for security purposes we will check if this is the key what login server sends.</summary>
    private int loginOk;

    private int unk1;
    private int unk2;

    public CM_L2AUTH_LOGIN_CHECK(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        playOk2 = ReadD();
        playOk1 = ReadD();
        accountId = ReadD();
        loginOk = ReadD();
        unk1 = ReadD();
        unk2 = ReadD();
    }

    protected override void RunImpl()
    {
        LoginServer.GetInstance().RegisterLoginRequest(accountId, GetConnection(), loginOk, playOk1, playOk2);
    }
}
