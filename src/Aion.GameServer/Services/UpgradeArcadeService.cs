using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Aion.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Event;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Event.Upgradearcade;
using Aion.GameServer.Network.Aion.Serverpackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.Audit;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/UpgradeArcadeService (ginho1, Estrayl, Neon). Upgrade-arcade event progression. ConcurrentHashMap→ConcurrentDictionary; putIfAbsent→GetOrAdd(key, eager value) (matches Java eager-alloc); schedule(lambda, ms)→Schedule(ct=>{...;ValueTask}, TimeSpan.FromMilliseconds); stream filter/findFirst/orElse(null)→Where().FirstOrDefault(); currentTimeMillis→UtcNow.ToUnixTimeMilliseconds; Rnd.chance/get. ArcadeProgress ported; templates/SM_*/ItemService red-tolerated.</summary>
public class UpgradeArcadeService
{
    private const int FRENZY_POINTS_PER_TOKEN = 8;

    /// <summary>
    /// A map containing all player arcade progresses to avoid data loss caused by disconnects.
    /// Consider, each progress will be lost after restarting the server.
    /// </summary>
    private readonly ConcurrentDictionary<int, ArcadeProgress> cachedProgress = new ConcurrentDictionary<int, ArcadeProgress>();

    private ArcadeProgress GetProgress(int objId)
    {
        return cachedProgress.GetOrAdd(objId, new ArcadeProgress(objId));
    }

    public void Start(Player player, int sessionId)
    {
        ArcadeProgress progress = GetProgress(player.GetObjectId());
        PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(progress, sessionId));
        if (progress.GetCurrentLevel() > 1)
            PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(progress));
        if (progress.GetFrenzyEndTimeMillis() > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            SendRemainingFrenzyModeTime(player, progress);
    }

    private void SendRemainingFrenzyModeTime(Player player, ArcadeProgress progress)
    {
        int remainingFrenzyModeSeconds = (int)((progress.GetFrenzyEndTimeMillis() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000);
        PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(Math.Max(0, remainingFrenzyModeSeconds)));
    }

    public void Open(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE());
    }

    public void ShowRewardList(Player player)
    {
        PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(UpgradeArcadeService.GetInstance().GetRewards()));
    }

    public List<ArcadeRewards> GetRewards()
    {
        return DataManager.UPGRADE_ARCADE_DATA.GetRewards();
    }

    public ArcadeRewards GetRewardsForLevel(int level)
    {
        List<ArcadeRewards> arcadeRewards = DataManager.UPGRADE_ARCADE_DATA.GetRewards();
        for (int i = arcadeRewards.Count - 1; i >= 0; i--)
        {
            ArcadeRewards rewards = arcadeRewards[i];
            if (level >= rewards.GetMinLevel())
                return rewards;
        }
        return null;
    }

    public void StartTry(Player player)
    {
        ArcadeProgress progress = GetProgress(player.GetObjectId());
        long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMillis < progress.GetNextTryTimeMillis())
        {
            AuditLogger.Log(player, "tried to start next arcade try while the button was still greyed out");
            return;
        }
        if (progress.GetCurrentLevel() >= DataManager.UPGRADE_ARCADE_DATA.GetMaxUpgradeLevel().GetLevel())
        {
            return;
        }
        else if (progress.GetCurrentLevel() == 0)
        {
            if (!player.GetInventory().DecreaseByItemId(186000389, 1))
                return;

            progress.SetCurrentLevel(1);
            IncreaseFrenzyPoints(player, progress, FRENZY_POINTS_PER_TOKEN);
        }
        else if (progress.GetCurrentLevel() == progress.GetResumeLevel()) // start after paying the tokens to resume
        {
            IncreaseFrenzyPoints(player, progress, FRENZY_POINTS_PER_TOKEN * EventsConfig.ARCADE_RESUME_TOKEN);
        }
        int delayMillis = 3000;
        progress.SetTimeNextTry(nowMillis + delayMillis);
        bool success = Rnd.Chance() < GetUpgradeChance(progress.GetCurrentLevel());
        PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(success, progress));
        if (success)
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                progress.SetCurrentLevel(progress.GetCurrentLevel() + 1);
                PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(progress));
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(delayMillis));
        }
        else
        {
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                bool canResume = progress.GetResumeLevel() == 0 && progress.GetCurrentLevel() >= DataManager.UPGRADE_ARCADE_DATA.GetMinResumableLevel();
                progress.SetResumeLevel(canResume ? progress.GetCurrentLevel() : 0);
                progress.SetCurrentLevel(1);
                PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(progress, canResume));
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(delayMillis));
        }
    }

    private void IncreaseFrenzyPoints(Player player, ArcadeProgress progress, int frenzyPoints)
    {
        int frenzyModeThreshold = 100;
        progress.SetFrenzyPoints(progress.GetFrenzyPoints() + frenzyPoints);
        if (progress.GetFrenzyPoints() >= frenzyModeThreshold)
        {
            progress.SetFrenzyPoints(progress.GetFrenzyPoints() % frenzyModeThreshold);
            int frenzyDurationSeconds = 90;
            long frenzyDurationMillis = frenzyDurationSeconds * 1000;
            progress.SetFrenzyEndTimeMillis(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + frenzyDurationMillis);
            PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(frenzyDurationSeconds));
            int playerId = player.GetObjectId();
            ThreadPoolManager.GetInstance().Schedule(ct =>
            {
                Player p = World.GetInstance().GetPlayer(playerId);
                if (p != null)
                    SendRemainingFrenzyModeTime(p, progress);
                return ValueTask.CompletedTask;
            }, System.TimeSpan.FromMilliseconds(frenzyDurationMillis));
        }
    }

    private float GetUpgradeChance(int currentLevel)
    {
        ArcadeLevel lv = DataManager.UPGRADE_ARCADE_DATA.GetUpgradeLevels().Where(level => level.GetLevel() == currentLevel).FirstOrDefault();
        return lv == null ? DataManager.UPGRADE_ARCADE_DATA.GetMaxUpgradeLevel().GetUpgradeChance() : lv.GetUpgradeChance();
    }

    public void Resume(Player player)
    {
        ArcadeProgress progress = GetProgress(player.GetObjectId());
        if (progress.GetResumeLevel() == 0)
        {
            AuditLogger.Log(player, "illegally tried to resume arcade");
            return;
        }
        if (!player.GetInventory().DecreaseByItemId(186000389, EventsConfig.ARCADE_RESUME_TOKEN))
        {
            PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(8, true));
            return;
        }
        progress.SetCurrentLevel(progress.GetResumeLevel());
        PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(progress));
    }

    public void GetReward(Player player)
    {
        ArcadeProgress progress = GetProgress(player.GetObjectId());
        if (progress.GetCurrentLevel() == 0)
        {
            AuditLogger.Log(player, "tried to get arcade rewards without spending token");
            return;
        }
        List<ArcadeRewardItem> rewardList = new List<ArcadeRewardItem>();

        ArcadeRewards rewards = GetRewardsForLevel(progress.GetCurrentLevel());
        if (rewards == null)
            return;
        bool isFrenzyActive = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < progress.GetFrenzyEndTimeMillis();
        foreach (ArcadeRewardItem arcadeTabItem in rewards.GetArcadeRewardItems())
        {
            if (isFrenzyActive)
            {
                if (arcadeTabItem.GetFrenzyCount() > 0)
                    rewardList.Add(arcadeTabItem);
            }
            else if (arcadeTabItem.GetNormalCount() > 0)
            {
                rewardList.Add(arcadeTabItem);
            }
        }

        ArcadeRewardItem item = Rnd.Get(rewardList);
        if (item != null)
        {
            long itemCount = isFrenzyActive ? item.GetFrenzyCount() : item.GetNormalCount();
            ItemService.AddItem(player, item.GetItemId(), itemCount, true);
            PacketSendUtility.SendPacket(player, new SM_UPGRADE_ARCADE(item.GetItemId(), itemCount));
            progress.SetResumeLevel(0);
            progress.SetCurrentLevel(0);
        }
    }

    public static UpgradeArcadeService GetInstance()
    {
        return SingletonHolder.instance;
    }

    private static class SingletonHolder
    {
        internal static readonly UpgradeArcadeService instance = new UpgradeArcadeService();
    }
}
