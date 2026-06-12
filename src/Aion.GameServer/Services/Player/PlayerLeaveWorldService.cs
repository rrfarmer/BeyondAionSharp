using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Summons;
using Aion.GameServer.Model.Team.Alliance;
using Aion.GameServer.Model.Team.Group;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.ChatServer;
using Aion.GameServer.QuestEngine;
using Aion.GameServer.QuestEngine.Model;
using Aion.GameServer.Services;
using Aion.GameServer.Services.ConquerorAndProtectorSystem;
using Aion.GameServer.Services.Findgroup;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Summons;
using Aion.GameServer.Taskmanager.Tasks;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;
using LocationData = Aion.GameServer.Dataholders.PlayerInitialData.LocationData;

namespace Aion.GameServer.Services.Players;

/// <summary>Java parity: services/player/PlayerLeaveWorldService (ATracer, Neon). leaveWorldDelayed (schedule disconnect cleanup as DESPAWN task) and leaveWorld (full logout: safe-position fallback, service onLogout hooks, dead->revive, store effects/cooldowns/lifestats, group/alliance/legion logout, release summon/pet/postman, quest onLogout, persist common data + last-online, chat server logout). Future->ScheduledTask; schedule(Runnable,ms)->Schedule(ct-lambda); new Timestamp(currentTimeMillis)->DateTimeOffset.FromUnixTimeMilliseconds(UtcNow...). Many service/DAO/model types red-tolerated.</summary>
public class PlayerLeaveWorldService
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(PlayerLeaveWorldService));

    /// <summary>
    /// Called when a player loses client connection. NOTICE: must only be called from AionConnection.OnDisconnect().
    /// </summary>
    public static void LeaveWorldDelayed(Player player, long delayInMillis)
    {
        ScheduledTask leaveWorldTask = ThreadPoolManager.GetInstance().Schedule(ct => { LeaveWorld(player); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(delayInMillis));
        player.GetController().AddTask(TaskId.DESPAWN, leaveWorldTask);
    }

    /// <summary>
    /// Saves a player and removes him from the world (char selection or client close). NOTICE: called only from CM_QUIT.
    /// </summary>
    public static void LeaveWorld(Player player)
    {
        AionConnection con = player.GetClientConnection();
        player.SetClientConnection(null); // this sets the player semi-offline, PacketSendUtility will not send packets anymore

        WorldPosition pos = player.GetPosition();
        if (pos == null || pos.GetMapRegion() == null)
        { // ensure safe logout
            log.LogWarning(player + " had invalid position: " + pos + " so he was reset to bind point");
            BindPointPosition bp = player.GetBindPoint();
            if (bp != null)
                pos = Aion.GameServer.World.World.GetInstance().CreatePosition(bp.GetMapId(), bp.GetX(), bp.GetY(), bp.GetZ(), bp.GetHeading(), 1);
            else
            {
                LocationData ld = DataManager.PLAYER_INITIAL_DATA.GetSpawnLocation(player.GetRace().ToString());
                pos = Aion.GameServer.World.World.GetInstance().CreatePosition(ld.GetMapId(), ld.GetX(), ld.GetY(), ld.GetZ(), ld.GetHeading(), 1);
            }
            player.SetPosition(pos);
        }

        FindGroupService.GetInstance().OnLogout(player);
        player.GetResponseRequester().DenyAll();
        player.GetFriendList().SetStatus(FriendList.Status.OFFLINE, player.GetCommonData());
        BrokerService.GetInstance().RemovePlayerCache(player);
        ExchangeService.GetInstance().CancelExchange(player);
        RepurchaseService.GetInstance().RemoveRepurchaseItems(player);
        if (AutoGroupConfig.AUTO_GROUP_ENABLE)
            AutoGroupService.GetInstance().OnLogout(player);
        ConquerorAndProtectorService.GetInstance().OnLeaveMap(player);
        MultiClientingService.OnLeaveWorld(player);
        InstanceService.OnLogout(player);
        GMService.GetInstance().OnPlayerLogout(player);
        KiskService.GetInstance().OnLogout(player);

        if (player.IsDead())
        {
            if (player.IsInInstance() || player.GetWorldId() == 400030000)
                PlayerReviveService.InstanceRevive(player);
            else
                PlayerReviveService.BindRevive(player);
        }
        else if (DuelService.GetInstance().IsDueling(player))
        {
            DuelService.GetInstance().LoseDuel(player);
        }
        player.GetEffectController().RemoveNonStorableEffectsForLogout();
        PlayerEffectsDAO.StorePlayerEffects(player);
        PlayerCooldownsDAO.StorePlayerCooldowns(player);
        ItemCooldownsDAO.StoreItemCooldowns(player);
        PlayerLifeStatsDAO.UpdatePlayerLifeStat(player);

        PlayerGroupService.OnPlayerLogout(player);
        PlayerAllianceService.OnPlayerLogout(player);
        // fix legion warehouse exploits
        LegionService.GetInstance().LegionWhUpdate(player);
        player.GetEffectController().RemoveAllEffects(true);
        player.GetLifeStats().CancelAllTasks();

        Summon summon = player.GetSummon();
        if (summon != null)
            SummonsService.DoMode(SummonMode.RELEASE, summon, UnsummonType.LOGOUT);
        if (player.GetPet() != null)
            player.GetPet().GetController().Delete();
        if (player.GetPostman() != null)
            player.GetPostman().GetController().Delete();

        ExpireTimerTask.GetInstance().UnregisterExpirables(player);
        if (player.GetCraftingTask() != null)
            player.GetCraftingTask().Stop();

        Aion.GameServer.QuestEngine.QuestEngine.GetInstance().OnLogOut(new QuestEnv(null, player, 0));
        DateTime lastOnline = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).UtcDateTime;
        player.GetController().Delete();
        player.GetCommonData().SetOnline(false);
        player.GetCommonData().SetLastOnline(lastOnline);
        if (player.IsLegionMember()) // must be called after setOnline and setLastOnline
            LegionService.GetInstance().OnLogout(player);
        player.GetCommonData().SetX(player.GetX());
        player.GetCommonData().SetY(player.GetY());
        player.GetCommonData().SetZ(player.GetZ());
        player.GetCommonData().SetHeading(player.GetHeading());

        ChatServer.GetInstance().SendPlayerLogout(player);

        PlayerService.StorePlayer(player);

        player.GetInventory().SetOwner(null);
        player.GetWarehouse().SetOwner(null);
        player.GetAccount().GetAccountWarehouse().SetOwner(null);

        PlayerDAO.StoreOldCharacterLevel(player.GetObjectId(), player.GetLevel());
        PlayerDAO.StoreLastOnlineTime(player.GetObjectId(), lastOnline);
        PlayerDAO.OnlinePlayer(player, false); // marks that player was fully saved and may enter world again

        con.SetActivePlayer(null);
    }
}
