using System.Collections.Generic;
using System.Text;
using Aion.GameServer.Cache;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Event;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates.Rewards;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Mail;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Utils.ChatHandlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Handlers.AdminCommands;

/// <summary>
/// A command that handles analysis and rewarding for seasonal head hunting events.
/// Take care when using the reward parameter, it will clean all references including the database tables.
/// Java parity: data/handlers/admincommands/Headhunting (Estrayl).
/// </summary>
public class Headhunting : AdminCommand
{
    private static readonly ILogger log = NullLogger.Instance;

    /// <summary>Contains the rewards for different rankings (key value).</summary>
    private readonly SortedDictionary<int, List<RewardItem>> rewards = new();
    private readonly List<RewardItem> consolationRewards = new();

    /// <summary>Contains a sorted descending list of Headhunter by kills per PlayerClass.</summary>
    private SortedDictionary<Race, SortedDictionary<PlayerClass, List<Headhunter>>> results;

    public Headhunting()
        : base("headhunting")
    {
        SetSyntaxInfo(
            "<analyze> - Analyzes the season.",
            "<show> <rewards|results> - Shows the registered rewards or analyzed results",
            "<clear> - Clears the analayzed results",
            "<addKills> <playerId> <kills> - Add headhunting kills of specified player.",
            "<finalize> <true|false> - Finalizes the season (clears all references) and rewards all participants if requested"
        );

        // Initialize seasonal headhunting rewards
        rewards[1] = new List<RewardItem>();
        rewards[2] = new List<RewardItem>();
        rewards[3] = new List<RewardItem>();

        rewards[1].Add(new RewardItem(164002276, 5)); // Eternal War Battle Scroll
        rewards[1].Add(new RewardItem(188950017, 3)); // Special Courier Pass (Abyss Eternal/Lv. 61-65)
        rewards[1].Add(new RewardItem(186000051, 15)); // Major Ancient Crown

        rewards[2].Add(new RewardItem(164002276, 3)); // Eternal War Battle Scroll
        rewards[2].Add(new RewardItem(188950017, 3)); // Special Courier Pass (Abyss Eternal/Lv. 61-65)
        rewards[2].Add(new RewardItem(186000051, 10)); // Major Ancient Crown

        rewards[3].Add(new RewardItem(164002276, 1)); // Eternal War Battle Scroll
        rewards[3].Add(new RewardItem(188950017, 2)); // Special Courier Pass (Abyss Eternal/Lv. 61-65)
        rewards[3].Add(new RewardItem(186000051, 5)); // Major Ancient Crown

        consolationRewards.Add(new RewardItem(186000051, 1)); // Major Ancient Crown
    }

    public override void Execute(Player admin, params string[] paramsArr)
    {
        if (paramsArr.Length == 0)
        {
            SendInfo(admin);
            return;
        }
        switch (paramsArr[0])
        {
            case "analyze":
                if (AnalyzeSeason(admin))
                    SendInfo(admin, "Season successfully analyzed.");
                break;
            case "clear":
                if (results != null)
                {
                    results.Clear();
                    results = null;
                    SendInfo(admin, "Results successfully cleared.");
                }
                break;
            case "show":
                if (paramsArr.Length == 1)
                    SendInfo(admin);
                else if (paramsArr[1].Equals("rewards", System.StringComparison.OrdinalIgnoreCase))
                    ShowRewards(admin);
                else if (paramsArr[1].Equals("results", System.StringComparison.OrdinalIgnoreCase))
                    ShowResults(admin);
                break;
            case "addKills":
                if (paramsArr.Length < 3)
                {
                    SendInfo(admin);
                    return;
                }
                if (TryParseInt(paramsArr[1], out int playerId) && TryParseInt(paramsArr[2], out int kills))
                    AddKills(admin, playerId, kills);
                else
                    SendInfo(admin, "playerId and kills should be numbers.");
                break;
            case "finalize":
                if (paramsArr.Length < 2)
                {
                    SendInfo(admin);
                    return;
                }
                bool shouldReward = bool.TryParse(paramsArr[1], out bool b) && b;
                FinalizeSeason(admin, shouldReward);
                break;
            default:
                SendInfo(admin);
                break;
        }
    }

    private void AddKills(Player admin, int playerId, int kills)
    {
        if (!EventsConfig.ENABLE_HEADHUNTING)
        {
            SendInfo(admin, "Season is not active.");
            return;
        }
        Headhunter hunter = PvpService.GetInstance().GetHeadhunterById(playerId);
        if (hunter != null)
        {
            int newKills = hunter.GetKills() + kills;
            if (newKills < 0)
                newKills = 0;

            hunter.SetKills(newKills);
            hunter.SetPersistentState(IPersistable.PersistentState.UPDATE_REQUIRED);
            SendInfo(admin, "Successfully added " + kills + " kills for player [ID=" + playerId + ", name=" + PlayerService.GetPlayerName(playerId)
                + ", currentKills=" + newKills + "].");
        }
        else
        {
            SendInfo(admin, "The player could not be found. Wrong player ID.");
        }
    }

    private bool AnalyzeSeason(Player admin)
    {
        if (results != null)
        {
            SendInfo(admin, "Season is still ascertained. Use <show> paramater to get a survey with the final information "
                + "or use <reward> to execute the reward progress.");
            return false;
        }
        IDictionary<int, Headhunter> headhunters = PvpService.GetInstance().GetAllHeadhunters();
        results = new SortedDictionary<Race, SortedDictionary<PlayerClass, List<Headhunter>>>();
        results[Race.ASMODIANS] = new SortedDictionary<PlayerClass, List<Headhunter>>();
        results[Race.ELYOS] = new SortedDictionary<PlayerClass, List<Headhunter>>();
        foreach (Headhunter hunter in headhunters.Values)
        {
            PlayerCommonData pcd = PlayerService.GetOrLoadPlayerCommonData(hunter.GetHunterId());
            if (pcd == null)
            {
                log.LogWarning("PlayerID: " + hunter.GetHunterId() + " did not exist anymore.");
                continue;
            }
            PlayerClass pc = pcd.GetPlayerClass();
            Race race = pcd.GetRace();
            if (!results[race].ContainsKey(pc))
                results[race][pc] = new List<Headhunter>();
            List<Headhunter> hunterz = results[race][pc];
            hunterz.Add(hunter);
            hunterz.Sort();
        }
        return true;
    }

    private void ShowResults(Player admin)
    {
        if (results == null)
        {
            SendInfo(admin, "There are no results available! The headhunting season needs to be analyzed before rewarding.");
            return;
        }
        else if (results.Count == 0)
        {
            results = null;
            SendInfo(admin, "No players participated, yet. Empty reward map was cleared.");
            return;
        }
        StringBuilder builder = new StringBuilder();
        foreach (Race race in results.Keys)
        {
            SortedDictionary<PlayerClass, List<Headhunter>> raceMap = results[race];
            builder.Append("<hr><center>").Append(race).Append("</center>");
            foreach (PlayerClass pc in raceMap.Keys)
            {
                builder.Append("<br><br>").Append(pc.ToString()).Append("<br>");
                List<Headhunter> hunterz = raceMap[pc];
                for (int pos = 0; pos < hunterz.Count; pos++)
                {
                    Headhunter hunter = hunterz[pos];
                    if (pos + 1 > rewards.Count && hunter.GetKills() < EventsConfig.HEADHUNTING_CONSOLATION_PRIZE_KILLS)
                        break;

                    string name = PlayerService.GetPlayerName(hunter.GetHunterId());
                    builder.Append("<br>").Append(pos + 1).Append(". ").Append(name).Append(" Kills: ").Append(hunter.GetKills());
                }
            }
        }
        HTMLService.ShowHTML(admin, HTMLCache.GetInstance().GetHTML("headhunting.xhtml").Replace("HUNTERZ", builder.ToString()));
    }

    private void ShowRewards(Player admin)
    {
        if (rewards.Count == 0)
        {
            SendInfo(admin, "There are no rewards available!");
            return;
        }
        StringBuilder builder = new StringBuilder();
        builder.Append("<hr><center>Rewards</center><br><hr>");
        foreach (int rank in rewards.Keys)
        {
            builder.Append("<br><br><br>Rank: ").Append(rank).Append("<br>");
            List<RewardItem> items = rewards[rank];
            foreach (RewardItem item in items)
                builder.Append("<br>").Append(item.GetCount()).Append("x ").Append(DataManager.ITEM_DATA.GetItemTemplate(item.GetId()).GetName());
        }
        if (consolationRewards.Count != 0)
        {
            builder.Append("<br><br>Consolation prize for >= ").Append(EventsConfig.HEADHUNTING_CONSOLATION_PRIZE_KILLS).Append(" kills<br>");
            foreach (RewardItem item in consolationRewards)
                builder.Append("<br>").Append(item.GetCount()).Append("x ").Append(DataManager.ITEM_DATA.GetItemTemplate(item.GetId()).GetName());
        }
        HTMLService.ShowHTML(admin, HTMLCache.GetInstance().GetHTML("headhunting.xhtml").Replace("HUNTERZ", builder.ToString()));
    }

    private void RewardPlayers(Player admin)
    {
        int rewardedPlayers = 0;
        int sentMails = 0;
        foreach (Race race in results.Keys)
        {
            SortedDictionary<PlayerClass, List<Headhunter>> raceMap = results[race];
            foreach (PlayerClass pc in raceMap.Keys)
            {
                List<Headhunter> hunterz = raceMap[pc];
                for (int pos = 0; pos < hunterz.Count; pos++)
                {
                    Headhunter hunter = hunterz[pos];
                    List<RewardItem> items = null;
                    if (pos < 3)
                        items = rewards.TryGetValue(pos + 1, out var r) ? r : null;
                    else if (hunter.GetKills() >= EventsConfig.HEADHUNTING_CONSOLATION_PRIZE_KILLS)
                        items = consolationRewards;

                    if (items == null || items.Count == 0)
                        continue;

                    string name = PlayerService.GetPlayerName(hunter.GetHunterId());
                    string rank;
                    switch (pos)
                    {
                        case 0:
                            rank = "1st";
                            break;
                        case 1:
                            rank = "2nd";
                            break;
                        case 2:
                            rank = "3rd";
                            break;
                        default:
                            rank = "consolation";
                            break;
                    }
                    foreach (RewardItem item in items)
                    {
                        if (SystemMailService.SendMail("Headhunting Corp", name, "Rewards",
                            "We congratulate you for reaching the " + rank + " rank in this season with a total of " + hunter.GetKills() + " kills.", item.GetId(),
                            item.GetCount(), 0, LetterType.BLACKCLOUD))
                        {
                            sentMails++;
                        }
                        else
                        {
                            log.LogError("Failed to send reward mail to player " + name + " for rank " + rank + ". (ItemId: " + item.GetId() + " Count: "
                                + item.GetCount() + ").");
                        }
                    }
                    log.LogInformation(
                        "[Race: " + race + "] [PlayerClass: " + pc + "] [Rank: " + rank + "] [RewardedPlayer: " + name + "] [Kills: " + hunter.GetKills() + "]");
                    rewardedPlayers++;
                }
            }
        }
        SendInfo(admin, "Successfully rewarded " + rewardedPlayers + " Players and sent " + sentMails + " Mails.");
    }

    private void FinalizeSeason(Player admin, bool shouldReward)
    {
        if (EventsConfig.ENABLE_HEADHUNTING)
        {
            SendInfo(admin, "Season is still active! You have to disable headhunting to reward participants.");
            return;
        }
        else if (shouldReward && results == null)
        {
            SendInfo(admin, "No results available! The headhunting season needs to be analyzed before rewarding.");
            return;
        }
        else if (shouldReward && results.Count == 0)
        {
            results = null;
            SendInfo(admin, "No participation in this season. Empty reward map was cleared.");
            return;
        }

        if (shouldReward)
            RewardPlayers(admin);

        PvpService.GetInstance().FinalizeHeadhuntingSeason();
        HeadhuntingDAO.ClearTables();
        results.Clear();
        results = null;
        SendInfo(admin, "Successfully cleared all references for this season and finished archiving.");
    }
}
