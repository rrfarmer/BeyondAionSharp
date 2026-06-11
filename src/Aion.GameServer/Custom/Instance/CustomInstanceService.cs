using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using Aion.GameServer.Cache;
using Aion.GameServer.Custom.Instance.Neuralnetwork;
using Aion.GameServer.Dao;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Instance;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Aion.GameServer.Utils.Time;
using Aion.GameServer.World;

namespace Aion.GameServer.Custom.Instance;

/// <summary>Java parity: custom/instance/CustomInstanceService (Jo, Estrayl). Arrays.asList→List initializer; ConcurrentHashMap→ConcurrentDictionary (computeIfAbsent→GetOrAdd, remove→TryRemove); ServerTime.now() ZonedDateTime→DateTimeOffset (with(LocalTime.of(h,0))→new DateTimeOffset(date,h,0,0,Offset); isBefore→&lt;; minusDays/minusSeconds→AddDays/AddSeconds(neg); toEpochSecond()*1000→ToUnixTimeSeconds()*1000); Persistable.NEW→IPersistable.New; instanceof Creature→is Creature; RoahCustomInstanceHandler::new→()=>new(...); Java text block→C# raw string literal (HTML whitespace insignificant; trailing newline after &lt;/tr&gt; preserved via blank line). DAO/HTML/Teleport/Skill red-tolerated.</summary>
public class CustomInstanceService
{
    private static readonly List<int> restrictedSkills = new List<int> { 0, 243, 244, 277, 282, 302, 912, 1178, 1327, 1346, 1347, 1757, 2106, 2167, 2400,
        2425, 2565, 2778, 3331, 3643, 3663, 3683, 3705, 3729, 3788, 3789, 3833, 3835, 3837, 3839, 3904, 3991, 4407, 8291, 10164, 11011, 13010, 13234,
        13231 };

    public const int REWARD_COIN_ID = 186000409;
    private const int CUSTOM_INSTANCE_WORLD_ID = 300070000; // roah chamber
    private static readonly int LEADERBOARD_WINDOW_OBJECT_ID = IDFactory.GetInstance().NextId();
    private const int RESET_HOUR = 9;

    // Neural network related
    private readonly ConcurrentDictionary<int, List<PlayerModelEntry>> playerModelEntriesCache = new ConcurrentDictionary<int, List<PlayerModelEntry>>();

    private CustomInstanceService()
    {
    }

    public bool CanEnter(int playerId)
    {
        CustomInstanceRank playerRankObject = CustomInstanceDAO.LoadPlayerRankObject(playerId);
        if (playerRankObject == null)
            return true;
        DateTimeOffset now = ServerTime.Now();
        DateTimeOffset reUseTime = new DateTimeOffset(now.Year, now.Month, now.Day, RESET_HOUR, 0, 0, now.Offset);
        if (now < reUseTime)
            reUseTime = reUseTime.AddDays(-1);
        return playerRankObject.GetLastEntry() < reUseTime.ToUnixTimeSeconds() * 1000;
    }

    public void OnEnter(Player player)
    {
        if (!UpdateLastEntry(player.GetObjectId(), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
        {
            PacketSendUtility.SendMessage(player, "Sorry. Some shugo broke our database, please report this in our bugtracker :(");
            return;
        }
        playerModelEntriesCache[player.GetObjectId()] = LoadPlayerModelEntries(player.GetObjectId());
        WorldMapInstance wmi = InstanceService.GetNextAvailableInstance(CUSTOM_INSTANCE_WORLD_ID, 0, (byte)1, () => new RoahCustomInstanceHandler(), 1, true);
        wmi.Register(player.GetObjectId());
        TeleportService.TeleportTo(player, wmi.GetMapId(), wmi.GetInstanceId(), 504.0f, 396.0f, 94.0f, (byte)30, TeleportAnimation.FADE_OUT_BEAM);
    }

    public CustomInstanceRank LoadOrCreateRank(int playerId)
    {
        CustomInstanceRank customInstanceRank = CustomInstanceDAO.LoadPlayerRankObject(playerId);
        if (customInstanceRank == null)
            customInstanceRank = new CustomInstanceRank(playerId, 0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0, 0);
        return customInstanceRank;
    }

    public bool ResetEntryCooldown(int playerId)
    {
        CustomInstanceRank rankObj = CustomInstanceDAO.LoadPlayerRankObject(playerId);
        if (rankObj == null)
            return false;
        DateTimeOffset now = ServerTime.Now();
        DateTimeOffset reUseTime = new DateTimeOffset(now.Year, now.Month, now.Day, RESET_HOUR, 0, 0, now.Offset);
        if (now < reUseTime)
            reUseTime = reUseTime.AddDays(-1);
        if (rankObj.GetLastEntry() < reUseTime.ToUnixTimeSeconds() * 1000)
        {
            return false;
        }
        else
        {
            reUseTime = reUseTime.AddSeconds(-1);
            rankObj.SetLastEntry(reUseTime.ToUnixTimeSeconds() * 1000);
            return CustomInstanceDAO.StorePlayer(rankObj);
        }
    }

    public bool UpdateLastEntry(int playerId, long newEntryTime)
    {
        CustomInstanceRank rankObj = LoadOrCreateRank(playerId);
        rankObj.SetLastEntry(newEntryTime);
        return CustomInstanceDAO.StorePlayer(rankObj);
    }

    public bool ChangePlayerRank(int playerId, int newRank, int achievedDps)
    {
        CustomInstanceRank rankObj = LoadOrCreateRank(playerId);
        ChangeRank(rankObj, newRank);
        rankObj.SetDps(achievedDps);
        return StoreNewRankData(rankObj);
    }

    private bool StoreNewRankData(CustomInstanceRank rankObj)
    {
        return CustomInstanceDAO.StorePlayer(rankObj);
    }

    private void ChangeRank(CustomInstanceRank rankObj, int newRank)
    {
        rankObj.SetRank(newRank);
        if (newRank > rankObj.GetMaxRank())
            rankObj.SetMaxRank(newRank);
    }

    public void RecordPlayerModelEntry(Player player, Skill skill, VisibleObject target)
    {
        // FILTER: Only record roah custom instance skills for the moment
        if (restrictedSkills.Contains(skill.GetSkillId()))
            return;

        List<PlayerModelEntry> entries = GetPlayerModelEntries(player.GetObjectId());
        entries.Add(new PlayerModelEntry(player, skill.GetSkillId(), target is Creature creature ? creature : null));
    }

    private List<PlayerModelEntry> LoadPlayerModelEntries(int playerId)
    {
        return CustomInstancePlayerModelEntryDAO.LoadPlayerModelEntries(playerId);
    }

    public void SaveNewPlayerModelEntries(int playerId)
    {
        if (!playerModelEntriesCache.TryRemove(playerId, out List<PlayerModelEntry> pmes))
            return;
        ICollection<PlayerModelEntry> filteredEntries = pmes.Where(e => IPersistable.New(e)).ToList();
        CustomInstancePlayerModelEntryDAO.InsertNewRecords(filteredEntries);
    }

    public List<PlayerModelEntry> GetPlayerModelEntries(int playerId)
    {
        return playerModelEntriesCache.GetOrAdd(playerId, k => new List<PlayerModelEntry>());
    }

    public void OpenLeaderboard(Player player, Race race)
    {
        List<CustomInstanceRankedPlayer> rankedPlayers = CustomInstanceDAO.LoadTop10(race);
        StringBuilder content = new StringBuilder(
"""
<br><br><br>
<font color='3E2601' size='4'>Eternal Challenge Leaderboard</font><br>
<br>
<img src='textures/ui/basic_sep1.dds' width='300' height='2'><br>
<br><br>
<table>
	<tr>
		<th align='right'><font color='3E2601'>#</font></th>
		<th>&nbsp;&nbsp;</th>
		<th colspan='2' align='center'><font color='3E2601'>Name</font></th>
		<th>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;</th>
		<th align='center'><font color='3E2601'>Rank</font></th>
	</tr>

""");
        int rank = 1;
        foreach (CustomInstanceRankedPlayer p in rankedPlayers)
        {
            content.Append("<tr>");
            content.Append("  <td align='right'><font color='3E2601'>").Append(rank++).Append("</font></td>");
            content.Append("  <td></td>");
            content.Append("  <td background='textures/black_smoke2.DDS'><img src='").Append(p.GetPlayerClass().GetIconImage()).Append("' width='24'></td>");
            content.Append("  <td><font color='3E2601'>").Append(p.GetName()).Append("</font></td>");
            content.Append("  <td></td>");
            content.Append("  <td><font color='3E2601'>").Append(CustomInstanceRankEnumExtensions.GetRankDescription(p.GetRank())).Append("</font></td>");
            content.Append("</tr>");
        }
        content.Append("</table>");
        string page = HTMLCache.GetInstance().GetHTML("simplePageTemplate.xhtml");
        HTMLService.SendData(player, LEADERBOARD_WINDOW_OBJECT_ID, page.Replace("%content%", content.ToString()));
    }

    private static class SingletonHolder
    {
        internal static readonly CustomInstanceService instance = new CustomInstanceService();
    }

    public static CustomInstanceService GetInstance()
    {
        return SingletonHolder.instance;
    }
}
