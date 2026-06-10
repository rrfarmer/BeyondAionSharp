using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Player;
using Aion.GameServer.Utils;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHECK_NICKNAME (-Nemesiss-, cura). Client asks whether a given nickname is free/valid. PlayerService/NameRestrictionService/SM_NICKNAME_CHECK_RESPONSE red-tolerated.</summary>
public class CM_CHECK_NICKNAME : AionClientPacket
{
    private string nick;

    public CM_CHECK_NICKNAME(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        nick = ReadS();
    }

    protected override void RunImpl()
    {
        AionConnection client = GetConnection();

        nick = Util.ConvertName(nick);

        if (PlayerService.IsNameUsedOrReserved(null, nick))
        {
            if (GSConfig.CHARACTER_CREATION_MODE == 2)
                client.SendPacket(new SM_NICKNAME_CHECK_RESPONSE(SM_CREATE_CHARACTER.RESPONSE_NAME_RESERVED));
            else
                client.SendPacket(new SM_NICKNAME_CHECK_RESPONSE(SM_CREATE_CHARACTER.RESPONSE_NAME_ALREADY_USED));
        }
        else if (!NameRestrictionService.IsValidName(nick))
        {
            client.SendPacket(new SM_NICKNAME_CHECK_RESPONSE(SM_CREATE_CHARACTER.RESPONSE_INVALID_NAME));
        }
        else if (NameRestrictionService.IsForbidden(nick))
        {
            client.SendPacket(new SM_NICKNAME_CHECK_RESPONSE(SM_CREATE_CHARACTER.RESPONSE_FORBIDDEN_CHAR_NAME));
        }
        else
        {
            client.SendPacket(new SM_NICKNAME_CHECK_RESPONSE(SM_CREATE_CHARACTER.RESPONSE_OK));
        }
    }
}
