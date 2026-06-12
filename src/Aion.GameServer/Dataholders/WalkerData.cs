using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Aion.GameServer.Model.Templates.Walker;
using Aion.GameServer.Utils.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Dataholders;

/// <summary>Java parity: dataholders/WalkerData (KKnD, Rolandas). @XmlRootElement(npc_walker)→[XmlRoot]; @XmlTransient Map→[XmlIgnore] Dictionary built in AfterUnmarshal (putIfAbsent→!TryAdd, source list cleared+nulled); saveData JAXB marshal transcribed literally (JAXBContext/Marshaller/JAXBException/XmlUtil red-tolerated — JAXB→XmlSerializer pillar). WalkerTemplate red-tolerated.</summary>
[XmlRoot("npc_walker")]
public class WalkerData
{
    private static readonly ILogger log = NullLogger.Instance;

    [XmlElement("walker_template")]
    public List<WalkerTemplate> walkerlist;

    [XmlIgnore]
    private Dictionary<string, WalkerTemplate> walkerlistData = new();

    public void AfterUnmarshal(object parent)
    {
        foreach (WalkerTemplate route in walkerlist)
        {
            if (!walkerlistData.TryAdd(route.GetRouteId(), route))
                log.LogWarning("Duplicate route ID: " + route.GetRouteId());
        }
        walkerlist.Clear();
        walkerlist = null;
    }

    public int Size()
    {
        return walkerlistData.Count;
    }

    public WalkerTemplate GetWalkerTemplate(string routeId)
    {
        return walkerlistData.GetValueOrDefault(routeId);
    }

    public void AddTemplate(WalkerTemplate newTemplate)
    {
        if (walkerlist == null)
            walkerlist = new List<WalkerTemplate>();
        walkerlist.Add(newTemplate);
    }

    public void SaveData(string routeId)
    {
        // Java parity: dataholders/WalkerData.saveData — JAXB marshal → idiomatic C# XmlSerializer (infra).
        string xmlPath = "./data/static_data/npc_walker/generated_npc_walker_" + routeId + ".xml";
        try
        {
            var serializer = new XmlSerializer(typeof(WalkerData));
            using var writer = new StreamWriter(xmlPath);
            serializer.Serialize(writer, this);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error while saving data: " + e.Message);
            return;
        }
        finally
        {
            if (walkerlist != null)
            {
                walkerlist.Clear();
                walkerlist = null;
            }
        }
    }

    public ICollection<WalkerTemplate> GetTemplates()
    {
        return walkerlistData.Values;
    }
}
