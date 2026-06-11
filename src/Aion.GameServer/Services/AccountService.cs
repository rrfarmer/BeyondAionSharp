using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Account;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items.Storage;
using Aion.GameServer.Services.Players;

namespace Aion.GameServer.Services;

/// <summary>
/// Java parity: services/AccountService (Luno, cura). Front-end for DAOs to retrieve Account objects.
/// Iterator.remove() during iteration→snapshot GetPlayerAccDataList() + Account.Remove(pad); Date.getTime()→DateTimeOffset.ToUnixTimeMilliseconds; currentTimeMillis→UtcNow. DAOs/GameServer red-tolerated.
/// </summary>
public class AccountService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AccountService));

    /// <summary>Returns Account object that has given id.</summary>
    public static Account GetAccount(int accountId, string accountName, long creationDate, AccountTime accountTime, byte accessLevel, byte membership,
        long toll, string allowedHddSerial)
    {
        log.LogDebug("[AS] request for account: " + accountId);

        Account account = LoadAccount(accountId);
        account.SetName(accountName);
        account.SetCreationDate(creationDate);
        account.SetAccountTime(accountTime);
        account.SetAccessLevel(accessLevel);
        account.SetMembership(membership);
        account.SetToll(toll);
        account.SetAllowedHddSerial(allowedHddSerial);
        RemoveDeletedCharacters(account);
        return account;
    }

    /// <summary>Removes from db characters that should be deleted (their deletion time has passed).</summary>
    public static void RemoveDeletedCharacters(Account account)
    {
        /* Removes chars that should be removed */
        foreach (PlayerAccountData pad in account.GetPlayerAccDataList())
        {
            Race race = pad.GetPlayerCommonData().GetRace();
            long deletionTime = pad.GetDeletionDate() == null ? 0 : pad.GetDeletionDate().Value.ToUnixTimeMilliseconds();
            if (deletionTime != 0 && deletionTime <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            {
                account.Remove(pad);
                account.DecrementCountOf(race);
                PlayerService.DeletePlayerFromDB(pad.GetPlayerCommonData().GetPlayerObjId());
                if (GSConfig.ENABLE_RATIO_LIMITATION && pad.GetPlayerCommonData().GetLevel() >= GSConfig.RATIO_MIN_REQUIRED_LEVEL)
                {
                    if (account.GetNumberOf(race) == 0)
                    {
                        GameServer.UpdateRatio(pad.GetPlayerCommonData().GetRace(), -1);
                    }
                }
                if (account.IsEmpty())
                {
                    InventoryDAO.DeleteAccountWH(account.GetId());
                    account.SetAccountWarehouse(LoadAccountWarehouse(account));
                    break;
                }
            }
        }
    }

    public static Account LoadAccount(int accountId)
    {
        Account account = new Account(accountId);
        List<int> playerIdList = PlayerDAO.GetPlayerOidsOnAccount(accountId);
        foreach (int playerId in playerIdList)
            account.AddPlayerAccountData(LoadPlayerAccountData(playerId));
        account.SetAccountWarehouse(LoadAccountWarehouse(account));
        return account;
    }

    public static PlayerAccountData LoadPlayerAccountData(int playerId)
    {
        PlayerCommonData playerCommonData = PlayerDAO.LoadPlayerCommonData(playerId);
        CharacterBanInfo cbi = PlayerPunishmentsDAO.GetCharBanInfo(playerId);
        PlayerAppearance appereance = PlayerAppearanceDAO.Load(playerId);
        // Load only equipment and its stones to display on character selection screen
        List<PlayerAccountData.VisibleItem> equipment = InventoryDAO.LoadVisibleEquipment(playerId);
        PlayerAccountData playerAccData = new PlayerAccountData(playerCommonData, appereance, cbi, equipment);
        PlayerDAO.SetCreationDeletionTime(playerAccData);
        return playerAccData;
    }

    public static Storage LoadAccountWarehouse(Account account)
    {
        Storage wh = new PlayerStorage(null, StorageType.ACCOUNT_WAREHOUSE);
        InventoryDAO.LoadStorage(account.GetId(), wh);
        ItemStoneListDAO.Load(wh.GetItems());
        return wh;
    }
}
