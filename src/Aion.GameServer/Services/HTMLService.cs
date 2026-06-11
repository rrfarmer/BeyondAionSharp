using System;
using System.Collections.Generic;
using System.Text;
using Aion.GameServer.Cache;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dao;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Guide;
using Aion.GameServer.Model.Templates.Guides;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Utils;
using Aion.GameServer.Utils.IdFactory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Services;

/// <summary>
/// Use this service to send raw html to the client.
/// Java parity: services/HTMLService (lhw, xTz).
/// </summary>
public class HTMLService
{
    private static readonly ILogger log = NullLogger.Instance; // Java logger "ITEM_HTML_LOG"

    private const int SHORT_MAX_VALUE = 32767; // Java Short.MAX_VALUE

    public static string GetHTMLTemplate(GuideTemplate template)
    {
        string context = HTMLCache.GetInstance().GetHTML("guideTemplate.xhtml");

        StringBuilder sb = new StringBuilder();
        sb.Append("<reward_items multi_count='").Append(template.GetRewardCount()).Append("'>\n");
        foreach (SurveyTemplate survey in template.GetSurveys())
        {
            sb.Append("<item_id count='").Append(survey.GetCount()).Append("'>").Append(survey.GetItemId()).Append("</item_id>\n");
        }
        sb.Append("</reward_items>\n");
        context = context.Replace("%reward%", sb.ToString());
        context = context.Replace("%radio%", template.GetSelect().Length == 0 ? " " : template.GetSelect());
        context = context.Replace("%html%", template.GetMessage().Length == 0 ? " " : template.GetMessage());
        context = context.Replace("%rewardInfo%", template.GetRewardInfo().Length == 0 ? " " : template.GetRewardInfo());
        return context;
    }

    public static void PushSurvey(string html)
    {
        int messageId = IDFactory.GetInstance().NextId();
        Aion.GameServer.World.World.GetInstance().ForEachPlayer(player => SendData(player, messageId, html));
    }

    public static void ShowHTML(Player player, string html)
    {
        SendData(player, IDFactory.GetInstance().NextId(), html);
    }

    public static void SendData(Player player, int messageId, string html)
    {
        int packetCount = (int) (html.Length / (SHORT_MAX_VALUE - 8f)) + 1;
        if (packetCount > 255) // max byte number (0xFF)
        {
            log.LogWarning(new Exception(), "HTML message could not be sent to client, since its content is too long"); // attach throwable for stacktrace
            return;
        }
        for (int partNo = 0; partNo < packetCount; partNo++)
        {
            try
            {
                int from = Math.Max(0, partNo * (SHORT_MAX_VALUE - 8));
                int to = Math.Min(html.Length, (partNo + 1) * (SHORT_MAX_VALUE - 8));
                PacketSendUtility.SendPacket(player, new SmQuestionnaire(messageId, (byte) partNo, (byte) packetCount, html.Substring(from, to - from)));
            }
            catch (Exception e)
            {
                log.LogError(e, "htmlservice.sendData");
            }
        }
    }

    public static void SendGuideHtml(Player player, int fromLevel, int toLevel)
    {
        for (int level = fromLevel; level <= toLevel; level++)
        {
            GuideTemplate[] surveyTemplate = DataManager.GUIDE_HTML_DATA.GetTemplatesFor(player.GetPlayerClass(), player.GetRace(), level);

            foreach (GuideTemplate template in surveyTemplate)
            {
                if (!template.IsActivated())
                    continue;
                int id = IDFactory.GetInstance().NextId();
                SendData(player, id, GetHTMLTemplate(template));
                GuideDAO.SaveGuide(id, player, template.GetTitle());
            }
        }
    }

    public static void OnPlayerLogin(Player player)
    {
        List<Guide> guides = GuideDAO.LoadGuides(player.GetObjectId());

        foreach (Guide guide in guides)
        {
            GuideTemplate template = DataManager.GUIDE_HTML_DATA.GetTemplateByTitle(guide.GetTitle());
            if (template != null)
            {
                if (template.IsActivated())
                    SendData(player, guide.GetGuideId(), GetHTMLTemplate(template));
            }
            else
            {
                log.LogWarning("Null guide template for title: {Title}", guide.GetTitle());
            }
        }
    }

    public static void GetReward(Player player, int messageId, List<int> items)
    {
        if (player == null || messageId < 1)
        {
            return;
        }

        if (SurveyService.GetInstance().IsActive(player, messageId))
        {
            return;
        }

        Guide guide = GuideDAO.LoadGuide(player.GetObjectId(), messageId);

        if (guide != null)
        {
            GuideTemplate template = DataManager.GUIDE_HTML_DATA.GetTemplateByTitle(guide.GetTitle());
            if (template == null)
            {
                return;
            }

            if (items.Count > template.GetRewardCount())
            {
                return;
            }

            if (items.Count > player.GetInventory().GetFreeSlots())
            {
                PacketSendUtility.SendPacket(player, SmSystemMessage.DiceInventoryError());
                return;
            }
            List<SurveyTemplate> templates = null;
            if (template.GetSurveys().Count != template.GetRewardCount())
            {
                templates = GetSurveyTemplates(template.GetSurveys(), items);
            }
            else
            {
                templates = template.GetSurveys();
            }
            if (templates.Count == 0)
            {
                return;
            }
            foreach (SurveyTemplate item in templates)
            {
                ItemService.AddItem(player, item.GetItemId(), item.GetCount());
                if (LoggingConfig.LOG_ITEM)
                {
                    log.LogInformation(string.Format("[ITEM] Item Guide ID/Count - {0}/{1} to player {2}.", item.GetItemId(), item.GetCount(), player.GetName()));
                }
            }
            GuideDAO.DeleteGuide(guide.GetGuideId());
            IDFactory.GetInstance().ReleaseId(guide.GetGuideId());
            items.Clear();
        }
    }

    private static List<SurveyTemplate> GetSurveyTemplates(List<SurveyTemplate> surveys, List<int> items)
    {
        List<SurveyTemplate> templates = new List<SurveyTemplate>();
        foreach (SurveyTemplate survey in surveys)
        {
            if (items.Contains(survey.GetItemId()))
            {
                templates.Add(survey);
            }
        }
        return templates;
    }
}
