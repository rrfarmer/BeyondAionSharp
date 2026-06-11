using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using ConnectType = Aion.GameServer.Model.Account.CharacterPasskey.ConnectType;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_DELETE_CHARACTER (-Nemesiss-). Client requests character deletion; blocked while in a legion; passkey-gated. LegionService/PlayerService/PlayerPasskeyDAO red-tolerated.</summary>
public class CM_DELETE_CHARACTER : AionClientPacket
{
    /// <summary>PlayOk2 - we dont care...</summary>
    private int playOk2;

    /// <summary>ObjectId of character that should be deleted.</summary>
    private int chaOid;

    public CM_DELETE_CHARACTER(int opcode, ISet<State> validStates)
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
        PlayerAccountData playerAccData = account.GetPlayerAccountData(chaOid);
        if (playerAccData == null)
            return;
        if (LegionService.GetInstance().GetLegionMember(playerAccData.GetPlayerCommonData()) != null)
        {
            SendPacket(SM_SYSTEM_MESSAGE.STR_GUILD_DISPERSE_STAYMODE_CANCEL_1());
            return;
        }
        // passkey check
        if (SecurityConfig.PASSKEY_ENABLE && !account.GetCharacterPasskey().IsPass())
        {
            account.GetCharacterPasskey().SetConnectType(ConnectType.DELETE);
            account.GetCharacterPasskey().SetObjectId(chaOid);
            bool hasPasskey = PlayerPasskeyDAO.ExistCheckPlayerPasskey(account.GetId());
            SendPacket(new SM_CHARACTER_SELECT(hasPasskey ? 1 : 0));
        }
        else
        {
            PlayerService.DeletePlayer(playerAccData);
            SendPacket(new SM_DELETE_CHARACTER(chaOid, playerAccData.GetDeletionTimeInSeconds()));
        }
    }
}
