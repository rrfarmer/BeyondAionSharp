using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aion.GameServer.Cache;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Model.Templates.Item;
using Aion.GameServer.Model.Templates.Survey;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Item;
using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>Java parity: services/SurveyService (KID).</summary>
public class SurveyService
{
    private static readonly ILogger log = NullLogger.Instance;
    // Java parity: LinkedHashMap (insertion-ordered) — Dictionary preserves insertion order until removal.
    private readonly Dictionary<int, SurveyItem> activeItems = new Dictionary<int, SurveyItem>();

    private SurveyService()
    {
        TaskUpdate task = new TaskUpdate(this);
        ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(ct => { task.Run(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(2000), TimeSpan.FromMilliseconds(SecurityConfig.SURVEY_DELAY * 60000));
    }

    public bool IsActive(Player player, int survId)
    {
        bool avail = activeItems.ContainsKey(survId);
        if (avail)
            RequestSurvey(player, survId);

        return avail;
    }

    public void RequestSurvey(Player player, int survId)
    {
        activeItems.TryGetValue(survId, out SurveyItem item);
        if (item == null || item.ownerId != player.GetObjectId())
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.CannotFindPoll());
            return;
        }

        ItemTemplate template = DataManager.ITEM_DATA.GetItemTemplate(item.itemId);
        if (template == null)
        {
            return;
        }
        if (player.GetInventory().IsFull(template.GetExtraInventoryId()))
        {
            PacketSendUtility.SendPacket(player, SmSystemMessage.FullInventory());
            log.LogWarning("[SurveyController] player " + player.GetName() + " tried to receive item with full inventory.");
            return;
        }
        if (SurveyControllerDAO.UseItem(item.uniqueId))
        {
            ItemService.AddItem(player, item.itemId, item.count);
            if (item.itemId == ItemId.KINAH)
                PacketSendUtility.SendPacket(player, SmSystemMessage.GetPollRewardMoney(item.count));
            else if (item.count == 1)
                PacketSendUtility.SendPacket(player, SmSystemMessage.GetPollRewardItem(template.GetL10n()));
            else
                PacketSendUtility.SendPacket(player, SmSystemMessage.GetPollRewardItemMulti(item.count, template.GetL10n()));

            activeItems.Remove(survId);
        }
    }

    public void TaskUpdateImpl()
    {
        List<SurveyItem> newList = SurveyControllerDAO.GetAllUnused();
        if (newList.Count == 0)
            return;

        List<int> players = new List<int>();
        int cnt = 0;
        foreach (SurveyItem survey in newList)
        {
            if (activeItems.TryAdd(survey.uniqueId, survey))
            {
                cnt++;
                if (!players.Contains(survey.ownerId))
                    players.Add(survey.ownerId);
            }
        }
        log.LogInformation("[SurveyController] found new " + cnt + " items for " + players.Count + " players.");
        foreach (int ownerId in players)
        {
            Player player = Aion.GameServer.World.World.GetInstance().GetPlayer(ownerId);
            if (player != null)
            {
                ShowAvailable(player);
            }
        }
    }

    public void ShowAvailable(Player player)
    {
        foreach (SurveyItem item in this.activeItems.Values)
        {
            if (item.ownerId != player.GetObjectId())
                continue;

            string context = HTMLCache.GetInstance().GetHTML("surveyTemplate.xhtml");
            context = context.Replace("%itemid%", item.itemId + "");
            context = context.Replace("%itemcount%", item.count + "");
            context = context.Replace("%html%", item.html);
            context = context.Replace("%radio%", item.radio);

            HTMLService.SendData(player, item.uniqueId, context);
        }
    }

    public class TaskUpdate
    {
        private readonly SurveyService outer;

        internal TaskUpdate(SurveyService outer)
        {
            this.outer = outer;
        }

        public void Run()
        {
            log.LogInformation("[SurveyController] update task start.");
            outer.TaskUpdateImpl();
        }
    }

    private static class SingletonHolder
    {
        internal static readonly SurveyService instance = new SurveyService();
    }

    public static SurveyService GetInstance()
    {
        return SingletonHolder.instance;
    }
}
