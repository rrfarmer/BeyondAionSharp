using System.Collections.Generic;
using System.Text;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Services.Player;
using ConnectType = Aion.GameServer.Model.Account.CharacterPasskey.ConnectType;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CHARACTER_PASSKEY (ginho1). Handles character passkey new/update/input (type 0/2/3) with wrong-count block. PlayerPasskeyDAO/CharacterPasskey/LoginServer red-tolerated.</summary>
public class CM_CHARACTER_PASSKEY : AionClientPacket
{
    private short type;
    private string passkey;
    private string newPasskey;

    public CM_CHARACTER_PASSKEY(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        type = ReadH(); // 0:new, 2:update, 3:input
        passkey = Encoding.Unicode.GetString(ReadB(48));
        if (type == 2)
            newPasskey = Encoding.Unicode.GetString(ReadB(48));
    }

    protected override void RunImpl()
    {
        AionConnection client = GetConnection();
        CharacterPasskey chaPasskey = client.GetAccount().GetCharacterPasskey();

        switch (type)
        {
            case 0:
                chaPasskey.SetIsPass(false);
                chaPasskey.SetWrongCount(0);
                PlayerPasskeyDAO.InsertPlayerPasskey(client.GetAccount().GetId(), passkey);
                client.SendPacket(new SM_CHARACTER_SELECT(2, type, chaPasskey.GetWrongCount()));
                break;
            case 2:
                bool isSuccess = PlayerPasskeyDAO.UpdatePlayerPasskey(client.GetAccount().GetId(), passkey, newPasskey);

                chaPasskey.SetIsPass(false);
                if (isSuccess)
                {
                    chaPasskey.SetWrongCount(0);
                    client.SendPacket(new SM_CHARACTER_SELECT(2, type, chaPasskey.GetWrongCount()));
                }
                else
                {
                    chaPasskey.SetWrongCount(chaPasskey.GetWrongCount() + 1);
                    CheckBlock(client.GetAccount().GetId(), chaPasskey.GetWrongCount());
                    client.SendPacket(new SM_CHARACTER_SELECT(2, type, chaPasskey.GetWrongCount()));
                }
                break;
            case 3:
                bool isPass = PlayerPasskeyDAO.CheckPlayerPasskey(client.GetAccount().GetId(), passkey);

                if (isPass)
                {
                    chaPasskey.SetIsPass(true);
                    chaPasskey.SetWrongCount(0);
                    client.SendPacket(new SM_CHARACTER_SELECT(2, type, chaPasskey.GetWrongCount()));

                    if (chaPasskey.GetConnectType() == ConnectType.ENTER)
                        PlayerEnterWorldService.EnterWorld(client, chaPasskey.GetObjectId());
                    else if (chaPasskey.GetConnectType() == ConnectType.DELETE)
                    {
                        PlayerAccountData playerAccData = client.GetAccount().GetPlayerAccountData(chaPasskey.GetObjectId());

                        PlayerService.DeletePlayer(playerAccData);
                        client.SendPacket(new SM_DELETE_CHARACTER(chaPasskey.GetObjectId(), playerAccData.GetDeletionTimeInSeconds()));
                    }
                }
                else
                {
                    chaPasskey.SetIsPass(false);
                    chaPasskey.SetWrongCount(chaPasskey.GetWrongCount() + 1);
                    CheckBlock(client.GetAccount().GetId(), chaPasskey.GetWrongCount());
                    client.SendPacket(new SM_CHARACTER_SELECT(2, type, chaPasskey.GetWrongCount()));
                }
                break;
        }
    }

    private void CheckBlock(int accountId, int wrongCount)
    {
        if (wrongCount >= SecurityConfig.PASSKEY_WRONG_MAXCOUNT)
        {
            // TODO : Change the account to be blocked
            LoginServer.GetInstance().SendBanPacket((byte)2, accountId, "", 60 * 8, 0);
        }
    }
}
