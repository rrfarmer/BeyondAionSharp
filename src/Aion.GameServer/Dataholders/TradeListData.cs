using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Tradelist;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/TradeListData (Luno). @XmlRootElement(npc_trade_list); afterUnmarshal→AfterUnmarshal(object).</summary>
[XmlRoot("npc_trade_list")]
public class TradeListData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("tradelist_template")] private List<TradeListTemplate> tlist;
    [XmlElement("trade_in_list_template")] private List<TradeListTemplate> tInlist;
    [XmlElement("purchase_template")] private List<TradeListTemplate> plist;

    [XmlIgnore] private readonly Dictionary<int, TradeListTemplate> npctlistData = new();
    [XmlIgnore] private readonly Dictionary<int, TradeListTemplate> npcTradeInlistData = new();
    [XmlIgnore] private readonly Dictionary<int, TradeListTemplate> npcPurchaseTemplateData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (TradeListTemplate npc in tlist)
        {
            npctlistData[npc.GetNpcId()] = npc;
        }
        foreach (TradeListTemplate npc in tInlist)
        {
            npcTradeInlistData[npc.GetNpcId()] = npc;
        }
        foreach (TradeListTemplate npc in plist)
        {
            npcPurchaseTemplateData[npc.GetNpcId()] = npc;
        }
        tlist = tInlist = plist = null;
    }

    public int Size()
    {
        return npctlistData.Count;
    }

    public TradeListTemplate GetTradeListTemplate(int id)
    {
        return npctlistData.TryGetValue(id, out var v) ? v : null;
    }

    public TradeListTemplate GetTradeInListTemplate(int id)
    {
        return npcTradeInlistData.TryGetValue(id, out var v) ? v : null;
    }

    public TradeListTemplate GetPurchaseTemplate(int id)
    {
        return npcPurchaseTemplateData.TryGetValue(id, out var v) ? v : null;
    }

    public Dictionary<int, TradeListTemplate> GetTradeListTemplate()
    {
        return npctlistData;
    }

    public void ValidateBuyLists(ICollection<NpcTemplate> npcTemplates)
    {
        List<int> missingNpcIds = npcTemplates
            .Where(npc => npc.SupportsAction(DialogAction.BUY) && GetTradeListTemplate(npc.GetTemplateId()) == null)
            .Select(npc => npc.GetTemplateId()).OrderBy(x => x)
            .ToList();
        if (missingNpcIds.Count != 0)
            log.LogWarning("Missing trade lists for these npcs: " + string.Join(", ", missingNpcIds));
        missingNpcIds = npcTemplates
            .Where(npc => npc.SupportsAction(DialogAction.TRADE_IN) && GetTradeInListTemplate(npc.GetTemplateId()) == null)
            .Select(npc => npc.GetTemplateId()).OrderBy(x => x)
            .ToList();
        if (missingNpcIds.Count != 0)
            log.LogWarning("Missing trade-in lists for these npcs: " + string.Join(", ", missingNpcIds));
    }
}
