using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Model.GameObjects.Siege;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Templates.Npc;
using Aion.GameServer.Model.Templates.Siegelocation;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Services.Siege;

/// <summary>Java parity: services/siege/FortressAssault (Luzien, Estrayl) extends Assault&lt;FortressSiege&gt;. Balaur fortress assault: handleAssault (dredgion + wave scheduling), spawnWave (teleport waves @1/10, budgeted computeWave + commander chance), computeWave/addAssaulters (budget spend by spawn stake/cost), difficulty settings from faction balance + influence, onDredgionCommanderKilled (budget drain -> despawn carrier). EnumMap->Dictionary; AssaulterType.values()->Values(); method-ref this::spawnWave->ct-lambda; schedule(...,delay,SECONDS)->TimeSpan.FromSeconds; remove(0)->[0]+RemoveAt(0); stream.filter.collect->LINQ; Math.round(float)->(int)Floor(x+0.5f); switch-on-SiegeRace. AssaultData/Assaulter/Influence red-tolerated.</summary>
public class FortressAssault : Assault<FortressSiege, FortressLocation>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("SIEGE_LOG");
    private readonly List<Assaulter> commanderSpawnList = new List<Assaulter>();
    private readonly AssaultData assaultData;
    private float difficulty, commanderSpawnChance, spawnBudget, startBudget;
    private int minSpawnDelay, waveCount, possibleCommanderCount;

    public FortressAssault(FortressSiege siege)
        : base(siege)
    {
        assaultData = siegeLocation.GetTemplate().GetAssaultData();
        CalculateDifficultySettings();
    }

    protected override void HandleAssault()
    {
        BalaurAssaultService.GetInstance().SpawnDredgion(assaultData.GetDredgionId());
        ScheduleSpawns();
    }

    protected override void OnAssaultFinish(bool isCaptured)
    {
        if (isCaptured)
            Announce(SM_SYSTEM_MESSAGE.STR_ABYSS_DRAGON_BOSS_KILLED(GetBossNpcL10n()));
    }

    private void ScheduleSpawns()
    {
        int delay = minSpawnDelay >= assaultData.GetBaseDelay() ? minSpawnDelay : Rnd.Get(minSpawnDelay, assaultData.GetBaseDelay());
        spawnTask = ThreadPoolManager.GetInstance().Schedule(ct => { SpawnWave(); return ValueTask.CompletedTask; }, TimeSpan.FromSeconds(delay));
    }

    private void SpawnWave()
    {
        if (!siegeLocation.IsVulnerable() || spawnBudget < 0.1f)
            return;

        switch (++waveCount)
        {
            case 1:
            case 10:
                List<Assaulter> teleportWave = assaultData.GetProcessedAssaulters().GetValueOrDefault(AssaulterType.TELEPORT);
                foreach (SiegeNpc npc in Aion.GameServer.World.World.GetInstance().GetLocalSiegeNpcs(locationId))
                    if (npc.GetRating() != NpcRating.LEGENDARY && npc.GetAbyssNpcType() != AbyssNpcType.ARTIFACT && Rnd.Chance() < 40)
                        SpawnAssaulter(Rnd.Get(teleportWave), npc);
                Announce(SM_SYSTEM_MESSAGE.STR_ABYSS_WARP_DRAGON());
                break;
            default:
                foreach (Assaulter a in ComputeWave())
                    SpawnAssaulter(a, boss);
                if (commanderSpawnList.Count != 0)
                {
                    if (Rnd.Chance() < commanderSpawnChance)
                    {
                        Assaulter commander = commanderSpawnList[0];
                        commanderSpawnList.RemoveAt(0);
                        SpawnAssaulter(commander, boss);
                        commanderSpawnChance = 0f;
                    }
                    else
                    {
                        commanderSpawnChance += 15 + 5 * difficulty;
                    }
                }
                Announce(SM_SYSTEM_MESSAGE.STR_ABYSS_CARRIER_DROP_DRAGON());
                break;
        }
        ScheduleSpawns();
    }

    private List<Assaulter> ComputeWave()
    {
        List<Assaulter> finalList = new List<Assaulter>();
        Dictionary<AssaulterType, List<Assaulter>> assaulterMap = assaultData.GetProcessedAssaulters();
        foreach (AssaulterType type in System.Enum.GetValues<AssaulterType>())
        {
            if (type == AssaulterType.TELEPORT || type == AssaulterType.COMMANDER)
                continue;
            AddAssaulters(finalList, assaulterMap.GetValueOrDefault(type), spawnBudget * type.GetSpawnStake());
        }
        return finalList;
    }

    private void AddAssaulters(List<Assaulter> output, List<Assaulter> input, float budget)
    {
        if (input == null || input.Count == 0)
            return;
        while (budget > 0.0f)
        {
            float budgetCopy = budget;
            Assaulter a = Rnd.Get(input.Where(assaulter => assaulter.GetSpawnCost() <= budgetCopy).ToList());
            if (a == null)
            {
                a = input[0];
            }
            output.Add(a);
            budget -= a.GetSpawnCost();
        }
    }

    private void Announce(SM_SYSTEM_MESSAGE msg)
    {
        siegeLocation.ForEachPlayer(p => PacketSendUtility.SendPacket(p, msg));
    }

    private void CalculateDifficultySettings()
    {
        float factionBalance = GetFactionBalanceMultiplier();
        float influence = GetInfluenceMultiplier();

        difficulty = factionBalance / 3f * (1f + influence) * SiegeConfig.SIEGE_DIFFICULTY_MULTIPLIER;

        spawnBudget = Math.Max(assaultData.GetBaseBudget() / 3f, (int)Math.Floor(assaultData.GetBaseBudget() * difficulty + 0.5f));
        startBudget = spawnBudget;
        AddAssaulters(commanderSpawnList, assaultData.GetProcessedAssaulters().GetValueOrDefault(AssaulterType.COMMANDER), difficulty);
        possibleCommanderCount = commanderSpawnList.Count;

        minSpawnDelay = Math.Min((int)Math.Floor(assaultData.GetBaseDelay() / difficulty + 0.5f), assaultData.GetBaseDelay() - 10);
        if (minSpawnDelay < 30) // just in case SIEGE_DIFFICULTY_MULTIPLIER is set beyond 1.0 (100%)
            minSpawnDelay = 30;

        log.LogInformation("Initialized fortress assault on [locationID=" + locationId + "] with [difficulty=" + difficulty + "] [factionBalance=" + factionBalance
            + "] [influence=" + influence + "] [difficultyMultiplier=" + SiegeConfig.SIEGE_DIFFICULTY_MULTIPLIER + "]");
    }

    private float GetFactionBalanceMultiplier()
    {
        int factionBalance = siegeLocation.GetFactionBalance();
        switch (siegeLocation.GetRace())
        {
            case SiegeRace.ASMODIANS:
                if (factionBalance < 0)
                    return Math.Abs(factionBalance);
                break;
            case SiegeRace.ELYOS:
                if (factionBalance > 0)
                    return Math.Abs(factionBalance);
                break;
        }
        return 1f;
    }

    private float GetInfluenceMultiplier()
    {
        switch (siegeLocation.GetRace())
        {
            case SiegeRace.ASMODIANS:
                return Influence.GetInstance().GetAsmodianInfluenceRate();
            case SiegeRace.ELYOS:
                return Influence.GetInstance().GetElyosInfluenceRate();
            default:
                return 1f;
        }
    }

    public void OnDredgionCommanderKilled()
    {
        spawnBudget -= startBudget * (1f / possibleCommanderCount);
        if (spawnBudget < 0.1f)
        {
            Aion.GameServer.World.World.GetInstance().ForEachPlayer(p =>
            {
                PacketSendUtility.SendPacket(p, new SM_NPC_ASSEMBLER(null));
                PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_ABYSS_CARRIER_DESPAWN());
            });
            if (spawnTask != null)
                spawnTask.Cancel(true);
            log.LogInformation("Finished fortress assault on [locationID=" + locationId + "] by defeating " + possibleCommanderCount + " dredgion commanders after "
                + waveCount + " waves.");
        }
    }
}
