using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils.IdFactory;
using State = Aion.GameServer.Network.Aion.AionConnection.State;

namespace Aion.GameServer.Network.Aion.Clientpackets;

/// <summary>Java parity: network/aion/clientpackets/CM_CREATE_CHARACTER (-Nemesiss-, cura, Neon). Client requests character creation or to open the creation menu (type 1). PlayerService/AccountService/IDFactory/SM_CREATE_CHARACTER red-tolerated.</summary>
public class CM_CREATE_CHARACTER : AbstractCharacterEditPacket
{
    private int type;

    public CM_CREATE_CHARACTER(int opcode, ISet<State> validStates)
        : base(opcode, validStates)
    {
    }

    protected override void ReadImpl()
    {
        ReadD(); // account id
        ReadS(); // account name
        ReadBasicInfo(true);
        ReadAppearance();
        type = ReadUC();
    }

    protected override void RunImpl()
    {
        Account account = GetConnection().GetAccount();

        if (type == 1)
        { // flag to enter char creation screen
            SendPacket(new SM_CREATE_CHARACTER(null, SM_CREATE_CHARACTER.RESPONSE_OPEN_CREATION_WINDOW));
            return;
        }

        AccountService.RemoveDeletedCharacters(account);
        int responseCode = ValidateBasicInfo(account);
        if (responseCode != SM_CREATE_CHARACTER.RESPONSE_OK)
        {
            SendPacket(new SM_CREATE_CHARACTER(null, responseCode));
            return;
        }

        PlayerCommonData playerCommonData = new PlayerCommonData(IDFactory.GetInstance().NextId());
        playerCommonData.SetName(characterName);
        playerCommonData.SetGender(gender);
        playerCommonData.SetRace(race);
        playerCommonData.SetPlayerClass(playerClass.Value);
        playerCommonData.SetLevel(1); // level (exp) must be set after class
        PlayerAccountData accPlData = new PlayerAccountData(playerCommonData, playerAppearance);
        Player player = PlayerService.NewPlayer(accPlData, account);

        if (!PlayerService.StoreNewPlayer(player, account.GetName(), account.GetId()))
        {
            SendPacket(new SM_CREATE_CHARACTER(null, SM_CREATE_CHARACTER.RESPONSE_DB_ERROR));
            IDFactory.GetInstance().ReleaseId(playerCommonData.GetPlayerObjId());
        }
        else
        {
            accPlData.SetVisibleItems(player.GetEquipment().GetEquippedForAppearance());
            accPlData.SetCreationDate(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            PlayerService.StoreCreationTime(player.GetObjectId(), accPlData.GetCreationDate());

            account.AddPlayerAccountData(accPlData);
            SendPacket(new SM_CREATE_CHARACTER(accPlData, SM_CREATE_CHARACTER.RESPONSE_OK));
        }
    }

    private int ValidateBasicInfo(Account account)
    {
        int maxCharCount = account.GetMembership() >= MembershipConfig.CHARACTER_ADDITIONAL_ENABLE ? MembershipConfig.CHARACTER_ADDITIONAL_COUNT
            : GSConfig.CHARACTER_LIMIT_COUNT;
        if (account.Size() > maxCharCount)
            return SM_CREATE_CHARACTER.RESPONSE_SERVER_LIMIT_EXCEEDED;
        if (playerClass == null) // should never happen (only with type == 1 to enter char creation screen, where we won't reach this validation)
            return SM_CREATE_CHARACTER.FAILED_TO_CREATE_THE_CHARACTER;
        if (PlayerService.IsNameUsedOrReserved(null, characterName))
            return GSConfig.CHARACTER_CREATION_MODE == 2 ? SM_CREATE_CHARACTER.RESPONSE_NAME_RESERVED : SM_CREATE_CHARACTER.RESPONSE_NAME_ALREADY_USED;
        if (!NameRestrictionService.IsValidName(characterName))
            return SM_CREATE_CHARACTER.RESPONSE_INVALID_NAME;
        if (NameRestrictionService.IsForbidden(characterName))
            return SM_CREATE_CHARACTER.RESPONSE_FORBIDDEN_CHAR_NAME;
        if (!playerClass.Value.IsStartingClass())
            return SM_CREATE_CHARACTER.RESPONSE_FORBIDDEN_CLASS;
        if (GSConfig.CHARACTER_CREATION_MODE == 0 && account.GetPlayerAccDataList().Any(p => p.GetPlayerCommonData().GetRace() != race))
        {
            return SM_CREATE_CHARACTER.RESPONSE_OTHER_RACE;
        }
        return SM_CREATE_CHARACTER.RESPONSE_OK;
    }
}
