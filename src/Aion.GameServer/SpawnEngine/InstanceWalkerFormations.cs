using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/InstanceWalkerFormations (Rolandas). slf4j logger→ILogger (warn→LogWarning); HashMap→Dictionary (Map.get→GetValueOrDefault, put→indexer); synchronized→lock(this); stream groupingBy→GroupBy.ToDictionary; filter/collect→Where/ToList; List.add(bool)→Add+return true; Rnd.get→Rnd.Get. ClusteredNpc/WalkerGroup/DataManager red-tolerated.</summary>
public class InstanceWalkerFormations
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(InstanceWalkerFormations));

    private Dictionary<string, List<ClusteredNpc>> groupedSpawnObjects;
    private Dictionary<string, WalkerGroup> walkFormations;
    private Dictionary<string, List<WalkerGroup>> formationVariants;
    private Dictionary<string, List<ClusteredNpc>> walkerVariants;

    public InstanceWalkerFormations()
    {
        groupedSpawnObjects = new Dictionary<string, List<ClusteredNpc>>();
        walkFormations = new Dictionary<string, WalkerGroup>();
        formationVariants = new Dictionary<string, List<WalkerGroup>>();
        walkerVariants = new Dictionary<string, List<ClusteredNpc>>();
    }

    public WalkerGroup GetSpawnWalkerGroup(string walkerId)
    {
        return walkFormations.GetValueOrDefault(walkerId);
    }

    protected internal bool CacheWalkerCandidate(ClusteredNpc npcWalker)
    {
        lock (this)
        {
            string walkerId = npcWalker.GetWalkTemplate().GetRouteId();
            List<ClusteredNpc> candidateList = groupedSpawnObjects.GetValueOrDefault(walkerId);
            if (candidateList == null)
            {
                candidateList = new List<ClusteredNpc>();
                groupedSpawnObjects[walkerId] = candidateList;
            }
            candidateList.Add(npcWalker);
            return true;
        }
    }

    /// <summary>
    /// Organizes spawns in all processed walker groups. Must be called only when spawning all npcs for the instance of world.
    /// </summary>
    protected internal void OrganizeAndSpawn()
    {
        foreach (List<ClusteredNpc> candidates in groupedSpawnObjects.Values)
        {
            Dictionary<int, List<ClusteredNpc>> npcsByPosition = candidates.GroupBy(cNpc => cNpc.GetPositionHash()).ToDictionary(g => g.Key, g => g.ToList());
            int maxSize = 0;
            List<ClusteredNpc> npcs = null;
            foreach (KeyValuePair<int, List<ClusteredNpc>> e in npcsByPosition)
            {
                if (e.Value.Count > maxSize)
                {
                    npcs = e.Value;
                    maxSize = npcs.Count;
                }
            }
            if (maxSize == 0 || npcs == null)
            {
                Log.LogWarning("Walkers missing for route: " + candidates[0].GetWalkTemplate().GetRouteId());
                continue;
            }
            if (maxSize == 1)
            {
                if (candidates.Count != 1)
                {
                    Log.LogWarning("Walkers not aligned for route: " + candidates[0].GetWalkTemplate().GetRouteId());
                    foreach (ClusteredNpc snpc in candidates)
                        snpc.Spawn(snpc.GetNpc().GetSpawn().GetZ());
                }
                else
                {
                    ClusteredNpc singleNpc = candidates[0];
                    if (singleNpc.GetWalkTemplate().GetVersionId() != null)
                    {
                        List<ClusteredNpc> variants = walkerVariants.GetValueOrDefault(singleNpc.GetWalkTemplate().GetVersionId());
                        if (variants == null)
                        {
                            variants = new List<ClusteredNpc>();
                            walkerVariants[singleNpc.GetWalkTemplate().GetVersionId()] = variants;
                        }
                        variants.Add(singleNpc);
                    }
                    else
                        singleNpc.Spawn(singleNpc.GetNpc().GetSpawn().GetZ());
                }
            }
            else
            {
                WalkerGroup wg = new WalkerGroup(npcs);
                if (candidates[0].GetWalkTemplate().GetPool() != candidates.Count)
                    Log.LogWarning("Incorrect pool for route: " + candidates[0].GetWalkTemplate().GetRouteId());
                walkFormations[candidates[0].GetWalkTemplate().GetRouteId()] = wg;
                wg.Form();
                if (wg.GetVersionId() == null)
                {
                    wg.Spawn();
                    // spawn the rest which didn't have the same coordinates
                    foreach (ClusteredNpc snpc in candidates)
                    {
                        if (npcs.Contains(snpc))
                            continue;
                        snpc.Spawn(snpc.GetNpc().GetZ());
                    }
                }
                else
                {
                    List<WalkerGroup> variants = formationVariants.GetValueOrDefault(wg.GetVersionId());
                    if (variants == null)
                    {
                        variants = new List<WalkerGroup>();
                        formationVariants[wg.GetVersionId()] = variants;
                    }
                    variants.Add(wg);
                }
            }
            // Now that all variants are in the map, spawn one randomly
            foreach (List<WalkerGroup> varGroups in formationVariants.Values)
            {
                WalkerGroup spawnedGroup = Rnd.Get(varGroups);
                spawnedGroup.Spawn();
            }
            foreach (List<ClusteredNpc> varWalkers in walkerVariants.Values)
            {
                ClusteredNpc spawnedWalker = Rnd.Get(varWalkers);
                spawnedWalker.Spawn(spawnedWalker.GetNpc().GetZ());
            }
        }
    }

    protected internal void ChangeCluster(WalkerGroup walkerGroup)
    {
        if (walkerGroup.GetVersionId() == null)
            return;
        List<WalkerGroup> varGroups = formationVariants.GetValueOrDefault(walkerGroup.GetVersionId());
        if (varGroups == null)
            return;
        List<WalkerGroup> notSpawned = varGroups.Where(group => !group.IsSpawned()).ToList();
        WalkerGroup newGroup = Rnd.Get(notSpawned);
        newGroup.Spawn();
        if (walkerGroup.IsSpawned())
            walkerGroup.Despawn();
    }

    protected internal void ChangeWalker(Npc npc)
    {
        string walkerId = npc.GetSpawn().GetWalkerId();
        if (walkerId == null)
            return;
        string versionId = DataManager.WALKER_VERSIONS_DATA.GetRouteVersionId(walkerId);
        if (versionId == null)
            return;
        List<ClusteredNpc> varWalkers = walkerVariants.GetValueOrDefault(versionId);
        if (varWalkers == null)
            return;
        List<ClusteredNpc> notSpawned = varWalkers.Where(cNpc => !cNpc.GetNpc().IsSpawned()).ToList();
        ClusteredNpc newWalker = Rnd.Get(notSpawned);
        newWalker.Spawn(newWalker.GetNpc().GetZ());
        if (!npc.IsSpawned())
            return;
        foreach (ClusteredNpc snpc in varWalkers)
        {
            if (snpc.GetNpc().Equals(npc))
            {
                snpc.Despawn();
                break;
            }
        }
    }

    protected internal void OnInstanceDestroy()
    {
        lock (this)
        {
            groupedSpawnObjects.Clear();
            walkFormations.Clear();
        }
    }
}
