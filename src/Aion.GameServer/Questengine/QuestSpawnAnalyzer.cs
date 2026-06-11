using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Model.Templates.Factions;
using Aion.GameServer.Model.Templates.Quest;
using Aion.GameServer.QuestEngine.Handlers;
using Aion.GameServer.QuestEngine.Handlers.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.QuestEngine;

/// <summary>Java parity: questEngine/QuestSpawnAnalyzer. Streams→LINQ; Map&lt;Set&lt;Integer&gt;,List&lt;Integer&gt;&gt;→Dictionary with HashSet&lt;int&gt;.CreateSetComparer() for value-semantics keys; computeIfAbsent→TryGetValue+init; Files.walk→Directory.EnumerateFiles recursive; DataManager/config dirs red-tolerated.</summary>
public class QuestSpawnAnalyzer
{
    private static readonly ILogger log = NullLogger.Instance;

    private QuestSpawnAnalyzer()
    {
    }

    internal static void Run(ICollection<AbstractQuestHandler> questHandlers, ICollection<QuestNpc> questNpcs, bool ignoreEventQuests)
    {
        log.LogInformation("Analyzing quest handlers (ignoreEventQuests=" + ignoreEventQuests + ")...");
        long timeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        HashSet<int> unobtainableQuests = new();
        HashSet<int> factionIds = new();
        HashSet<int> allSpawns = LoadNpcIdsSpawnedByHandlers();
        DataManager.SPAWNS_DATA.AddAllNpcIdsToSet(allSpawns);
        DataManager.TOWN_SPAWNS_DATA.AddAllNpcIdsToSet(allSpawns);
        DataManager.EVENT_DATA.AddAllNpcIdsToSet(allSpawns);
        foreach (NpcFactionTemplate nft in DataManager.NPC_FACTIONS_DATA.GetNpcFactionsData())
        {
            if (nft.GetNpcIds() == null || nft.GetNpcIds().Any(allSpawns.Contains))
                factionIds.Add(nft.GetId());
        }
        foreach (AbstractQuestHandler qh in questHandlers)
        {
            QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(qh.GetQuestId());
            if (qt.GetMinlevelPermitted() == 99 || qt.GetNpcFactionId() > 0 && !factionIds.Contains(qt.GetNpcFactionId()))
                unobtainableQuests.Add(qh.GetQuestId()); // players can still have these quests from before an update
        }
        Dictionary<HashSet<int>, List<int>> missingSpawnsByQuests = new(HashSet<int>.CreateSetComparer());
        foreach (QuestNpc npc in questNpcs)
        {
            if (allSpawns.Contains(npc.GetNpcId()))
                continue;
            HashSet<int> questIds = npc.FindAllRegisteredQuestIds(id => (!ignoreEventQuests || id < 80000) && !IsUnobtainable(id, unobtainableQuests) && !ExistsSpawnDataForAnyAlternativeNpc(id, npc.GetNpcId(), allSpawns));
            if (questIds.Count == 0)
                continue;
            if (!missingSpawnsByQuests.TryGetValue(questIds, out List<int> list))
            {
                list = new List<int>();
                missingSpawnsByQuests[questIds] = list;
            }
            list.Add(npc.GetNpcId());
        }
        timeMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timeMillis;
        if (missingSpawnsByQuests.Count == 0)
        {
            log.LogInformation("Quest handler analysis finished in {Time} ms without errors", timeMillis);
        }
        else
        {
            string missingSpawns = string.Concat(missingSpawnsByQuests
                .Select(e => "\n\tNpc " + string.Join("/", e.Value.OrderBy(v => v).Select(v => v.ToString())) + " (quests: " + string.Join(", ", e.Key.OrderBy(v => v).Select(v => v.ToString())) + ")")
                .OrderBy(s => s, StringComparer.Ordinal));
            log.LogWarning("Quest handler analysis finished in {Time} ms. Found {Count} missing quest npc spawns:{Spawns}", timeMillis, missingSpawnsByQuests.Count, missingSpawns);
        }
    }

    private static bool IsUnobtainable(int questId, HashSet<int> unobtainableQuests)
    {
        if (unobtainableQuests.Contains(questId))
            return true;
        QuestTemplate qt = DataManager.QUEST_DATA.GetQuestById(questId);
        foreach (XMLStartCondition startCondition in qt.GetXMLStartConditions())
        {
            if (startCondition.GetFinishedPreconditions() == null)
                continue;
            if (startCondition.GetFinishedPreconditions().All(fpc => IsUnobtainable(fpc.GetQuestId(), unobtainableQuests)))
                return true;
        }
        return false;
    }

    /// <summary>
    /// True, if alternative npc ids, which are valid for this quest, appear in spawn templates (e.g. mobs for quest kills or talk npcs)
    /// </summary>
    private static bool ExistsSpawnDataForAnyAlternativeNpc(int questId, int npcId, HashSet<int> allSpawns)
    {
        XMLQuest quest = DataManager.XML_QUESTS.GetQuest(questId);
        if (quest == null)
            return true; // no way to get alternative npcs from non-xml based handlers, so assume the quest spawns work (lol)
        HashSet<int> alternativeNpcs = quest.GetAlternativeNpcs(npcId);
        if (alternativeNpcs == null)
            return false;
        return alternativeNpcs.Any(allSpawns.Contains);
    }

    public static HashSet<int> LoadNpcIdsSpawnedByHandlers()
    {
        HashSet<int> npcIds = new();
        Regex pattern = new(@"\bsp(?:awn)?\([^,\d]*(\d{6})(?: : (\d{6}))?");
        ParseSpawnNpcIds(InstanceConfig.HANDLER_DIRECTORY, pattern, npcIds);
        ParseSpawnNpcIds(GSConfig.QUEST_HANDLER_DIRECTORY, pattern, npcIds);
        ParseSpawnNpcIds(AIConfig.HANDLER_DIRECTORY, pattern, npcIds);
        return npcIds;
    }

    private static void ParseSpawnNpcIds(FileInfo sourceDir, Regex pattern, HashSet<int> npcIds)
    {
        foreach (string path in Directory.EnumerateFiles(sourceDir.FullName, "*.java", SearchOption.AllDirectories))
        {
            Match matcher = pattern.Match(File.ReadAllText(path));
            while (matcher.Success)
            {
                for (int i = 1; i <= matcher.Groups.Count - 1; i++)
                {
                    Group group = matcher.Groups[i];
                    if (group.Success)
                        npcIds.Add(int.Parse(group.Value));
                }
                matcher = matcher.NextMatch();
            }
        }
    }
}
