using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.LoginServer;
using Aion.GameServer.Network.LoginServer.Serverpackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;

namespace Aion.GameServer.Services.Transfers;

/// <summary>Java parity: services/transfers/PlayerTransferService (KID). Singleton; cross-server character transfer. startTransfer (validation: account ownership, no legion, offline, reuse cooldown, kinah cap, no broker items; then send SM_PTRANSFER_CONTROL info packets), cloneCharacter (name dedupe, slot check, CMT_CHARACTER_INFORMATION.readInfo, name-change ticket, last-transfer-time), onOk/onError, put/getTransfer. LinkedHashMap/HashMap->Dictionary; map.remove->Remove(out); currentTimeMillis->DateTimeOffset.UtcNow.ToUnixTimeMilliseconds; split(",")->Split(','); REUSE_HOURS*3600000 int-overflow parity preserved. CMT_CHARACTER_INFORMATION/SM_PTRANSFER_CONTROL/DAO red-tolerated.</summary>
public class PlayerTransferService
{
    private readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerTransferService));
    private readonly ILogger textLog = NullLoggerFactory.Instance.CreateLogger("PLAYERTRANSFER");

    private static readonly PlayerTransferService instance = new PlayerTransferService();

    public static PlayerTransferService GetInstance()
    {
        return instance;
    }

    private Dictionary<int, TransferablePlayer> transfers = new Dictionary<int, TransferablePlayer>();
    private Dictionary<int, PlayerTransfer> playerTransfers = new Dictionary<int, PlayerTransfer>();
    private List<int> rsList = new List<int>();

    public PlayerTransferService()
    {
        if (!PlayerTransferConfig.REMOVE_SKILL_LIST.Equals("*"))
        {
            foreach (string skillId in PlayerTransferConfig.REMOVE_SKILL_LIST.Split(','))
                rsList.Add(int.Parse(skillId));
        }
        log.LogInformation("PlayerTransferService loaded. With " + rsList.Count + " restricted skills.");
    }

    public void StartTransfer(int accountId, int targetAccountId, int playerId, sbyte targetServerId, int taskId)
    {
        bool exist = false;
        foreach (int id in PlayerDAO.GetPlayerOidsOnAccount(accountId))
            if (id == playerId)
            {
                exist = true;
                break;
            }

        if (!exist)
        {
            log.LogWarning("transfer #" + taskId + " player " + playerId + " is not present on account " + accountId + ".");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.TASK_STOP, taskId, "player " + playerId + " is not present on account " + accountId));
            return;
        }

        if (LegionMemberDAO.IsIdUsed(playerId))
        {
            log.LogWarning("cannot transfer #" + taskId + " player with existing legion " + playerId + ".");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.TASK_STOP, taskId, "cannot transfer player with existing legion " + playerId));
            return;
        }

        PlayerCommonData common = PlayerService.GetOrLoadPlayerCommonData(playerId);
        if (common.IsOnline())
        {
            log.LogWarning("cannot transfer #" + taskId + " online players " + playerId + ".");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.TASK_STOP, taskId, "cannot transfer online players " + playerId));
            return;
        }

        if (PlayerTransferConfig.REUSE_HOURS > 0
            && common.GetLastTransferTime() + PlayerTransferConfig.REUSE_HOURS * 3600000 > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
            log.LogWarning("cannot transfer #" + taskId + " that player so often " + playerId + ".");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.TASK_STOP, taskId, "cannot transfer that player so often " + playerId));
            return;
        }

        Player player = PlayerService.GetPlayer(playerId, AccountService.LoadAccount(accountId));
        long kinah = player.GetInventory().GetKinah() + player.GetWarehouse().GetKinah();
        if (PlayerTransferConfig.MAX_KINAH > 0 && kinah >= PlayerTransferConfig.MAX_KINAH)
        {
            log.LogWarning("cannot transfer #" + taskId + " players with " + kinah + " kinah in inventory/wh.");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.TASK_STOP, taskId, "cannot transfer players with " + kinah + " kinah in inventory/wh."));
            return;
        }

        if (BrokerService.GetInstance().HasRegisteredItems(player))
        {
            log.LogWarning("cannot transfer #" + taskId + " player while he own some items in broker.");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.TASK_STOP, taskId, "cannot transfer player while he own some items in broker."));
            return;
        }

        TransferablePlayer tp = new TransferablePlayer(playerId, accountId, targetAccountId);
        tp.player = player;
        tp.targetServerId = targetServerId;
        tp.accountId = accountId;
        tp.targetAccountId = targetAccountId;
        tp.taskId = taskId;
        transfers[taskId] = tp;

        textLog.LogInformation("taskId:" + taskId + "; [StartTransfer]");
        LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.CHARACTER_INFORMATION, tp));
        LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.ITEMS_INFORMATION, tp));
        LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.DATA_INFORMATION, tp));
        LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.SKILL_INFORMATION, tp));
        LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.RECIPE_INFORMATION, tp));
        LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.QUEST_INFORMATION, tp));
    }

    /// <summary>
    /// sent from login to target server with character information from source server
    /// </summary>
    public void CloneCharacter(int taskId, PlayerTransfer transfer)
    {
        playerTransfers.Remove(taskId);
        string name = transfer.GetName();
        string account = transfer.GetAccount();
        int targetAccountId = transfer.GetTargetAccount();
        if (PlayerService.IsNameUsedOrReserved(null, name))
        {
            if (PlayerTransferConfig.BLOCK_SAMENAME)
            {
                LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.ERROR, taskId, "Name is already in use"));
                return;
            }

            log.LogInformation("Name is already in use `" + name + "`");
            textLog.LogInformation("taskId:" + taskId + "; [CloneCharacter:!isFreeName]");
            string newName = name;

            int i = 0;
            while (PlayerService.IsNameUsedOrReserved(null, newName))
            {
                newName = name + "_" + ++i;
            }
            name = newName;
        }
        if (AccountService.LoadAccount(targetAccountId).Size() >= GSConfig.CHARACTER_LIMIT_COUNT)
        {
            LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.ERROR, taskId, "No free character slots"));
            return;
        }

        Player cha = new CMT_CHARACTER_INFORMATION(transfer.GetDB()).ReadInfo(name, targetAccountId, account, rsList, textLog);

        if (cha == null)
        { // something went wrong!
            log.LogError("clone failed #" + taskId + " `" + name + "`");
            LoginServer.GetInstance().SendPacket(
                new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.ERROR, taskId, "unexpected sql error while creating a clone"));
        }
        else
        {
            if (!transfer.GetName().Equals(cha.GetName()))
                InventoryDAO.Store(ItemFactory.NewItem(169670001), cha); // [Event] Name Change Ticket
            PlayerDAO.SetPlayerLastTransferTime(cha.GetObjectId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            LoginServer.GetInstance().SendPacket(new SM_PTRANSFER_CONTROL(SM_PTRANSFER_CONTROL.OK, taskId));
            log.LogInformation("clone successful #" + taskId + " `" + name + "`");
            textLog.LogInformation("taskId:" + taskId + "; [CloneCharacter:Done]");
        }
    }

    /// <summary>
    /// from login server to source, after response from target server
    /// </summary>
    public void OnOk(int taskId)
    {
        this.transfers.Remove(taskId, out TransferablePlayer tplayer);
        textLog.LogInformation("taskId:" + taskId + "; [TransferComplete]");
        PlayerService.DeletePlayerFromDB(tplayer.playerId);
    }

    /// <summary>
    /// from login server to source, after response from target server
    /// </summary>
    public void OnError(int taskId, string reason)
    {
        this.transfers.Remove(taskId);
        textLog.LogInformation("taskId:" + taskId + "; [Error. Transfer failed] " + reason);
    }

    public void PutTransfer(int taskId, PlayerTransfer playerTransfer)
    {
        playerTransfers[taskId] = playerTransfer;
    }

    public PlayerTransfer GetTransfer(int taskId)
    {
        return playerTransfers.GetValueOrDefault(taskId);
    }
}
