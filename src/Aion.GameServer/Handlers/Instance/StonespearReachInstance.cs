using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Instance;
using Aion.GameServer.Model.Instance.Instancescore;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion.Instanceinfo;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services;
using Aion.GameServer.Services.Abyss;
using Aion.GameServer.Services.Items;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/StonespearReachInstance (Yeats) : GeneralInstanceHandler. @InstanceID(301500000). Legion Dominion siege defense: 3min prep, 30min fail-timer, staged wave spawns + aetheric-field bomb tasks, point-based S/A/B/D/F ranking, LegionDominionScore reward. Future→ScheduledTask; System.currentTimeMillis→DateTimeOffset; synchronized checkRank/canEnter→lock; Rnd.get→Rnd.Get; Math.toRadians→Math.PI/180. 1:1.</summary>
[InstanceID(301500000)]
public class StonespearReachInstance : GeneralInstanceHandler
{
    private LegionDominionScore reward;
    private long? startTime;
    private readonly List<ScheduledTask> tasks = new();
    private readonly List<WorldPosition> points = new();
    private ScheduledTask timer, failTask;
    private Race? instanceRace;
    private Legion instanceLegion;
    private int aethericKills = 0;
    private readonly List<int> allowedPlayers = new();
    private readonly List<int> deadPlayers = new();
    private readonly object @lock = new();

    public StonespearReachInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        reward = new LegionDominionScore();
        reward.SetInstanceProgressionType(InstanceProgressionType.PREPARING);
        Spawn(833284, 231.14f, 264.399f, 96.23f, (byte)1, 14);
        AddWorldPoints();
        if (timer == null)
        {
            startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            timer = ThreadPoolManager.GetInstance().Schedule(() =>
            {
                startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                reward.SetInstanceProgressionType(InstanceProgressionType.START_PROGRESS);
                SendPacket(null, 0);
                StartInstance();
                StartFailTask();
            }, 180000); // 3min
        }
    }

    public override void OnEnterInstance(Player player)
    {
        if (!allowedPlayers.Contains(player.GetObjectId()))
        {
            if (allowedPlayers.Count >= 12)
            {
                TeleportService.TeleportTo(player, instance, 152, 264, 103);
            }
            else
            {
                allowedPlayers.Add(player.GetObjectId());
            }
        }

        if (deadPlayers.Contains(player.GetObjectId()))
        {
            TeleportService.TeleportTo(player, instance, 152, 264, 103);
        }

        if (reward != null && !reward.IsRewarded())
        {
            SendPacket(null, 0);
        }
        if (instanceLegion == null)
        {
            instanceLegion = player.GetLegion();
        }
        if (instanceRace == null)
        {
            instanceRace = player.GetRace();
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            // trash mobs
            case 855765:
            case 855766:
            case 855767:
            case 855768:
            case 855769:
            case 855770:
            case 855771:
            case 855772:
            case 855773:
                AddPoints(npc, 100);
                break;
            case 855788:
            case 855789:
            case 855790:
            case 855791:
            case 855792:
            case 855793:
            case 855794:
            case 855795:
            case 855796:
                AddPoints(npc, 200);
                break;
            case 855811:
            case 855812:
            case 855813:
            case 855814:
            case 855815:
            case 855816:
            case 855817:
            case 855818:
            case 855819:
                AddPoints(npc, 300);
                break;
            case 855834:
            case 855835:
            case 855836:
            case 855837:
            case 855838:
            case 855839:
            case 855840:
            case 855841:
            case 855842:
                AddPoints(npc, 400);
                break;
            // aetherfield
            case 855764:
                aethericKills++;
                AddPoints(npc, 500);
                break;
            case 855787:
                aethericKills++;
                AddPoints(npc, 1000);
                break;
            case 856303: // kebbit
                AddPoints(npc, 1500);
                break;
            case 855810:
                aethericKills++;
                AddPoints(npc, 1500);
                break;
            case 855833:
                aethericKills++;
                AddPoints(npc, 2000);
                break;
            // bosses
            case 856305: // clown
            case 855774: // hamerun
            case 855775: // kromede
            case 855776: // kalliga
                AddPoints(npc, 12000);
                break;
            case 855797: // bakarma
            case 855798: // triroan
            case 855799: // lanmark
                AddPoints(npc, 21000);
                break;
            case 855820: // calindi
            case 855821: // tahabata
            case 855822: // stormwing
                AddPoints(npc, 30000);
                break;
            case 855843: // guardian general
                AddPoints(npc, 42000);
                CheckRank(reward.GetPoints(), true);
                break;
            // guardian stone
            // elyos
            case 855763:
            case 855786:
            case 855809:
            case 855832:
            // asmodian
            case 856466:
            case 856467:
            case 856468:
            case 856469:
                CheckRank(reward.GetPoints(), false);
                break;
        }

        npc.GetController().Delete();
    }

    private void CheckRank(int points, bool bossKilled)
    {
        lock (@lock)
        {
            CancelAllTasks();
            int rank = 8;
            if (points > 67000)
            { // S-Rank
                if (bossKilled)
                {
                    reward.SetRewardItem1Count(4);
                    reward.SetFinalAP((points / 10));
                }
                else
                {
                    reward.SetRewardItem1Count(3);
                }
                reward.SetFinalGP(120);
                reward.SetRewardItem1(186000242); // Ceranium-Medal
                reward.SetRewardItem2(188053801); // Stonespear Siege Champion Reward Chest
                reward.SetRewardItem2Count(1);
                reward.SetRewardItem3(188053804); // Stonespear Siege Champion Relic Chest
                reward.SetRewardItem3Count(1);
                rank = 1;
            }
            else if (points > 41000)
            { // A-Rank
                reward.SetFinalGP(100);
                reward.SetRewardItem1(186000242); // Ceranium-Medal
                reward.SetRewardItem1Count(2);
                reward.SetRewardItem2(188053800); // Stonespear Siege Runner-Up Reward Chest
                reward.SetRewardItem2Count(1);
                reward.SetRewardItem3(188053803); // Stonespear Siege Runner-Up Relic Chest
                reward.SetRewardItem3Count(1);
                rank = 2;
            }
            else if (points > 25000)
            { // B-Rank
                reward.SetFinalGP(80);
                reward.SetRewardItem1(186000147); // Mithril-Medal
                reward.SetRewardItem1Count(5);
                reward.SetRewardItem2(188053799); // Stonespear Siege Reward Chest
                reward.SetRewardItem2Count(1);
                reward.SetRewardItem3(188053802); // Stonespear Siege Relic Chest
                reward.SetRewardItem3Count(1);
                rank = 3;
            }
            else if (points > 8800)
            { // D-Rank
                reward.SetRewardItem1(186000243); // Ceranium-Fragment
                reward.SetRewardItem1Count(12);
                reward.SetRewardItem2(186000096); // Platinum Medal
                reward.SetRewardItem2Count(3);
                rank = 5;
            }
            else if (points > 0)
            { // F-Rank
                if (points > 1000)
                {
                    reward.SetRewardItem1(186000243); // Ceranium-Fragment
                    reward.SetRewardItem1Count(6);
                    reward.SetRewardItem2(186000096); // Platinum Medal
                    reward.SetRewardItem2Count(1);
                }
                rank = 6;
            }

            if (!reward.IsRewarded())
            {
                reward.SetInstanceProgressionType(InstanceProgressionType.END_PROGRESS);
                reward.SetRank(rank);
                DespawnAll();
                long endTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                SendPacket(null, 0);
                Reward();
                LegionDominionService.GetInstance().OnFinishInstance(instanceLegion, reward.GetPoints(), (endTime - startTime.Value));
            }
        }
    }

    private void Reward()
    {
        instance.ForEachPlayer(player =>
        {
            ItemService.AddItem(player, reward.GetRewardItem1(), reward.GetRewardItem1Count(), true);
            ItemService.AddItem(player, reward.GetRewardItem2(), reward.GetRewardItem2Count(), true);
            ItemService.AddItem(player, reward.GetRewardItem3(), reward.GetRewardItem3Count(), true);
            if (reward.GetFinalGP() > 0)
            {
                Aion.GameServer.Services.Abyss.GloryPointsService.AddGp(player.GetObjectId(), Rates.GP.CalcResult(player, reward.GetFinalGP()));
            }
            if (reward.GetFinalAp() > 0)
            {
                Aion.GameServer.Services.Abyss.AbyssPointsService.AddAp(player, reward.GetFinalAp());
            }
        });
    }

    private void StartInstance()
    {
        DeleteAliveNpcs(833284);
        StartStage1_1();
    }

    private void StartStage1_1()
    { // minute 0 - 1
        SpawnGuardianStone(instanceRace == Race.ELYOS, 1);
        int npcId = 855765 + Rnd.Get(0, 8);
        SpawnAtPointsTask(50, npcId, -1);
        SpawnAtPointsTask(8000, 856305, 0);
        SpawnAtPointsTask(8000, npcId, -1);
        SpawnAtPointsTask(16000, npcId, -1);
        SpawnAtPointsTask(24000, npcId, -1);
        SpawnAtPointsTask(32000, npcId, -1);
        SpawnAtPointsTask(40000, npcId, -1);
        RndSpawnTask(36000, 856303, 1, 22, 23);
        StartStage1_2();
    }

    private void StartStage1_2()
    { // minute 1 - 2
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            RndSpawnTask(50, npcId, 3, 8, 16);
            RndSpawnTask(2000, npcId, 3, 7, 18);
            RndSpawnTask(4000, npcId, 6, 7, 22);
            npcId = 855765 + Rnd.Get(0, 8);
            RndSpawnTask(6000, npcId, 4, 7, 18);
            RndSpawnTask(7000, npcId, 4, 7, 18);
            RndSpawnTask(8000, npcId, 6, 7, 18);
            RndSpawnTask(11000, npcId, 8, 7, 18);
            RndSpawnTask(25000, 856303, 1, 22, 23);
            StartStage1_3();
        }, 60000));
    }

    private void StartStage1_3()
    { // minute 2 - 3
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            // 2k points
            Spawn(855764, 230.8977f, 285.5198f, 96.42f, (byte)80);
            Spawn(855764, 211.254f, 264.134f, 96.53f, (byte)0);
            Spawn(855764, 231.2034f, 243.8273f, 96.37f, (byte)30);
            Spawn(855764, 251.3068f, 264.307f, 96.31f, (byte)60);
            StartAethericFieldTask(1);

            RndSpawnTask(500, npcId, 5, 8, 12);
            RndSpawnTask(2500, npcId, 5, 7, 11);
            RndSpawnTask(5000, npcId, 5, 12, 22);
            npcId = 855765 + Rnd.Get(0, 8);
            RndSpawnTask(10000, npcId, 5, 12, 22);
            RndSpawnTask(13000, npcId, 5, 7, 11);
            RndSpawnTask(16000, npcId, 5, 7, 11);
            RndSpawnTask(26000, 856303, 1, 22, 23);
            StartStage1_4();
        }, 60000));
    }

    private void StartStage1_4()
    { // minute 3 - 5
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            // 12k points + 3k points
            Spawn(855776, 231.14f, 264.399f, 96.5f, (byte)10); // kaliga 855776
            RndSpawnTask(35000, 856303, 2, 22, 23);
            StartStage2_1();
        }, 60000));
    }

    private void StartStage2_1()
    { // minute 5 - 6
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            SpawnGuardianStone(instanceRace == Race.ELYOS, 2);
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855788 + Rnd.Get(0, 8);
            // 2k + 800 = 2,8k
            SpawnAtPointsTask(50, npcId, 0);
            SpawnAtPointsTask(50, npcId, 2);
            SpawnAtPointsTask(50, npcId, 4);
            SpawnAtPointsTask(50, npcId, 6);
            SpawnAtPointsTask(250, npcId2, 1);
            SpawnAtPointsTask(250, npcId2, 3);
            SpawnAtPointsTask(250, npcId2, 5);
            SpawnAtPointsTask(250, npcId2, 7);
            SpawnAtPointsTask(6000, npcId, -1);
            SpawnAtPointsTask(11000, npcId, -1);
            // 12k points
            if (instance != null && instance.GetNpc(856305) == null)
            {
                SpawnAtPointsTask(1000, 856305, 1);
            }
            // 1,5k points
            RndSpawnTask(20000, 856303, 1, 22, 23);
            StartStage2_2();
        }, 120000));
    }

    private void StartStage2_2()
    { // minute 6 - 7
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855788 + Rnd.Get(0, 8);
            // 2,2k points + 1,4k points = 3,6k points
            RndSpawnTask(50, npcId, 6, 11, 21);
            RndSpawnTask(500, npcId, 6, 11, 21);
            RndSpawnTask(1000, npcId, 10, 11, 21);
            RndSpawnTask(1500, npcId2, 7, 11, 21);
            // 1,5k points
            RndSpawnTask(28000, 856303, 1, 22, 23);
            StartStage2_3();
        }, 60000));
    }

    private void StartStage2_3()
    { // minute 7 - 8
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855788 + Rnd.Get(0, 8);
            // 4k points
            Spawn(855787, 230.8977f, 285.5198f, 96.42f, (byte)80);
            Spawn(855787, 211.254f, 264.134f, 96.53f, (byte)0);
            Spawn(855787, 231.2034f, 243.8273f, 96.37f, (byte)30);
            Spawn(855787, 251.3068f, 264.307f, 96.31f, (byte)60);
            StartAethericFieldTask(2);

            // 2,6k + 1,2k = 3,8k
            RndSpawnTask(500, npcId, 10, 11, 21);
            RndSpawnTask(4000, npcId, 10, 11, 21);
            RndSpawnTask(7000, npcId, 6, 11, 21);
            RndSpawnTask(10000, npcId2, 6, 11, 21);

            // 3k points
            RndSpawnTask(28000, 856303, 2, 22, 23);
            StartStage2_4();
        }, 60000));
    }

    private void StartStage2_4()
    { // minute 8 - 10
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            // 21k
            Spawn(855799, 231.14f, 264.399f, 96.5f, (byte)10); // lanmark
            // 3k
            RndSpawnTask(35000, 856303, 1, 22, 23);
            RndSpawnTask(45000, 856303, 1, 22, 23);
            StartStage3_1();
        }, 60000));
    }

    private void StartStage3_1()
    { // minute 10 - 11
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            SpawnGuardianStone(instanceRace == Race.ELYOS, 3);
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855788 + Rnd.Get(0, 8);
            int npcId3 = 855811 + Rnd.Get(0, 8);
            // 1,6k + 4,8k + 2,4k 8,8k
            SpawnAtPointsTask(50, npcId, -1);
            SpawnAtPointsTask(6000, npcId, -1);
            SpawnAtPointsTask(13000, npcId2, -1);
            SpawnAtPointsTask(20000, npcId2, -1);
            SpawnAtPointsTask(27000, npcId2, -1);
            SpawnAtPointsTask(33000, npcId3, -1);

            // 12k points
            if (instance != null && instance.GetNpc(856305) == null)
            {
                SpawnAtPointsTask(1000, 856305, 2);
            }
            // 1,5k points
            RndSpawnTask(36000, 856303, 1, 22, 23);
            StartStage3_2();
        }, 120000));
    }

    private void StartStage3_2()
    { // minute 11 - 12
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855811 + Rnd.Get(0, 8);
            // 4,2k points + 3,8k = 8k
            RndSpawnTask(100, npcId2, 7, 6, 9);
            SpawnAtPointsTask(5000, npcId, -1);
            RndSpawnTask(10000, npcId2, 7, 6, 9);
            SpawnAtPointsTask(15000, npcId, -1);
            RndSpawnTask(20000, npcId, 7, 6, 9);
            SpawnAtPointsTask(25000, npcId, -1);
            RndSpawnTask(30000, npcId, 7, 6, 9);

            // 1,5k points
            RndSpawnTask(36000, 856303, 1, 22, 23);
            StartStage3_3();
        }, 60000));
    }

    private void StartStage3_3()
    { // minute 12 - 13
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            // 6k points
            Spawn(855810, 230.8977f, 285.5198f, 96.42f, (byte)80);
            Spawn(855810, 211.254f, 264.134f, 96.53f, (byte)0);
            Spawn(855810, 231.2034f, 243.8273f, 96.37f, (byte)30);
            Spawn(855810, 251.3068f, 264.307f, 96.31f, (byte)60);
            StartAethericFieldTask(3);

            // 2,5k points
            RndSpawnTask(500, npcId, 25, 7, 14);

            // 1,5k points
            RndSpawnTask(25000, 856303, 1, 22, 23);
            StartStage3_4();
        }, 60000));
    }

    private void StartStage3_4()
    { // minute 13 - 14
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            // 3k + 2k + 1,5k = 6,5k
            RndSpawnTask(500, 855813, 10, 7, 10);
            RndSpawnTask(2000, npcId, 10, 7, 14);
            RndSpawnTask(6000, npcId, 15, 7, 14);
            RndSpawnTask(10000, npcId, 10, 7, 14);

            // 1,5k points
            RndSpawnTask(15000, 856303, 1, 22, 23);
            StartStage3_5();
        }, 60000));
    }

    private void StartStage3_5()
    { // minute 14 - 16
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            // 30k points
            Spawn(855822, 231.14f, 264.399f, 96.5f, (byte)10); // stormwing

            // 3k points
            RndSpawnTask(25000, 856303, 1, 22, 23);
            RndSpawnTask(30000, 856303, 1, 22, 23);
            StartStage4_1();
        }, 60000));
    }

    private void StartStage4_1()
    { // minute 16 - 17
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            SpawnGuardianStone(instanceRace == Race.ELYOS, 4);
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855788 + Rnd.Get(0, 8);
            int npcId3 = 855811 + Rnd.Get(0, 8);
            // 2k + 1,6k + 4,8k = 8,4k
            RndSpawnTask(500, npcId, 10, 9, 13);
            RndSpawnTask(7000, npcId, 10, 9, 13);
            RndSpawnTask(15000, npcId2, 8, 9, 13);
            RndSpawnTask(22000, 855835, 8, 15, 20);
            RndSpawnTask(30000, npcId3, 8, 9, 13);

            // 1,5k points
            RndSpawnTask(25000, 856303, 1, 22, 23);
            StartStage4_2();
        }, 120000));
    }

    private void StartStage4_2()
    { // minute 17 - 18
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            // 1k + 2k + 6k = 9k
            RndSpawnTask(500, 855765, 10, 9, 13);
            RndSpawnTask(2000, 855788, 10, 9, 13);

            SpawnAtPointsTask(13000, 855835, 0);
            SpawnAtPointsTask(14500, 855836, 1);
            SpawnAtPointsTask(16000, 855835, 2);
            SpawnAtPointsTask(17500, 855836, 3);
            SpawnAtPointsTask(19000, 855835, 4);
            SpawnAtPointsTask(20500, 855836, 5);
            SpawnAtPointsTask(22000, 855835, 6);
            SpawnAtPointsTask(23500, 855836, 7);

            RndSpawnTask(27000, 855835, 7, 12, 20);

            // 1,5k points
            RndSpawnTask(15000, 856303, 1, 22, 23);

            StartStage4_3();
        }, 60000));
    }

    private void StartStage4_3()
    { // minute 18 - 19
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            // 1,5k + 1k + 9,6k = 12,1k
            RndSpawnTask(500, 855765, 15, 9, 13);
            RndSpawnTask(2000, 855788, 5, 9, 13);

            SpawnAtPointsTask(1000, 855836, 0);
            SpawnAtPointsTask(1000, 855835, 1);
            SpawnAtPointsTask(3000, 855836, 2);
            SpawnAtPointsTask(3000, 855835, 3);
            SpawnAtPointsTask(5000, 855836, 4);
            SpawnAtPointsTask(5000, 855835, 5);
            SpawnAtPointsTask(7000, 855836, 6);
            SpawnAtPointsTask(7000, 855835, 7);

            SpawnAtPointsTask(9000, 855836, 0);
            SpawnAtPointsTask(90000, 855835, 1);
            SpawnAtPointsTask(11000, 855836, 2);
            SpawnAtPointsTask(11000, 855835, 3);
            SpawnAtPointsTask(13000, 855836, 4);
            SpawnAtPointsTask(13000, 855835, 5);
            SpawnAtPointsTask(15000, 855836, 6);
            SpawnAtPointsTask(15000, 855835, 7);

            SpawnAtPointsTask(18000, 855836, 0);
            SpawnAtPointsTask(18000, 855835, 1);
            SpawnAtPointsTask(20000, 855836, 2);
            SpawnAtPointsTask(20000, 855835, 3);
            SpawnAtPointsTask(22000, 855836, 4);
            SpawnAtPointsTask(22000, 855835, 5);
            SpawnAtPointsTask(24000, 855836, 6);
            SpawnAtPointsTask(24000, 855835, 7);

            // 3k points
            RndSpawnTask(10000, 856303, 1, 22, 23);
            RndSpawnTask(20000, 856303, 1, 22, 23);
            StartStage4_4();
        }, 60000));
    }

    private void StartStage4_4()
    { // minute 19 - 20
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            // 12k points + 8k points = 20k points
            // 2k + 6,4k + 6,4k = 14,8k
            int npcId = 855788 + Rnd.Get(0, 8);
            RndSpawnTask(500, 855765, 20, 9, 20);

            RndSpawnTask(8000, 855837, 4, 7, 9);
            RndSpawnTask(9000, npcId, 4, 7, 20);
            RndSpawnTask(10000, npcId, 4, 7, 20);

            npcId = 855765 + Rnd.Get(0, 8);
            RndSpawnTask(16000, 855837, 4, 7, 9);
            RndSpawnTask(17000, npcId, 4, 7, 20);
            RndSpawnTask(18000, npcId, 4, 7, 20);

            npcId = 855765 + Rnd.Get(0, 8);
            RndSpawnTask(24000, 855837, 4, 7, 9);
            RndSpawnTask(25000, npcId, 4, 7, 20);
            RndSpawnTask(26000, npcId, 4, 7, 20);

            npcId = 855765 + Rnd.Get(0, 8);
            RndSpawnTask(32000, 855837, 4, 7, 9);
            RndSpawnTask(33000, npcId, 4, 7, 20);
            RndSpawnTask(34000, npcId, 4, 7, 20);

            // 3k points
            RndSpawnTask(20000, 856303, 1, 22, 23);
            RndSpawnTask(25000, 856303, 1, 22, 23);
            StartStage4_5();
        }, 60000));
    }

    private void StartStage4_5()
    { // minute 20 - 21
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            int npcId = 855765 + Rnd.Get(0, 8);
            int npcId2 = 855788 + Rnd.Get(0, 8);
            // 8k
            Spawn(855833, 230.8977f, 285.5198f, 96.42f, (byte)80);
            Spawn(855833, 211.254f, 264.134f, 96.53f, (byte)0);
            Spawn(855833, 231.2034f, 243.8273f, 96.37f, (byte)30);
            Spawn(855833, 251.3068f, 264.307f, 96.31f, (byte)60);
            StartAethericFieldTask(4);

            // 2k + 2k + 3,2k = 7,2k
            RndSpawnTask(500, npcId, 20, 9, 20);
            RndSpawnTask(5000, npcId2, 10, 7, 20);
            RndSpawnTask(9000, 855835, 4, 7, 20);
            RndSpawnTask(11000, 855836, 4, 7, 20);

            // 3k
            RndSpawnTask(20000, 856303, 1, 22, 23);
            RndSpawnTask(25000, 856303, 1, 22, 23);

            StartStage4_6();
        }, 60000));
    }

    private void StartStage4_6()
    { // minute 21 - 30
        // 42k
        Spawn(855843, 231.14f, 264.399f, 96.5f, (byte)10); // general of illusion
        // 6k
        RndSpawnTask(25000, 856303, 1, 22, 23);
        RndSpawnTask(30000, 856303, 1, 22, 23);
        RndSpawnTask(35000, 856303, 1, 22, 23);
    }

    private void StartFailTask()
    {
        failTask = ThreadPoolManager.GetInstance().Schedule(() => CheckRank(reward.GetPoints(), false), 1800000); // 30min
    }

    private void AddPoints(Npc npc, int points)
    {
        if (reward.IsStartProgress())
        {
            reward.AddPoints(points);
            SendPacket(npc.GetObjectTemplate().GetL10n(), points);
            // TODO points >= 100mio -> stop. But its actually impossible to reach 100mio points.. I'll just leave it out
        }
    }

    private void SendPacket(string npcL10n, int points)
    {
        if (npcL10n != null)
            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_GET_SCORE(npcL10n, points));
        PacketSendUtility.BroadcastToMap(instance, new SM_INSTANCE_SCORE(instance.GetMapId(), new LegionDominionScoreWriter(reward), GetTime()));
    }

    private int GetTime()
    {
        long result = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime.Value;
        if (reward.IsPreparing())
        {
            return (int)(180000 - result);
        }
        else if (result < 1800000)
        {
            return (int)(1800000 - result);
        }
        return 0;
    }

    private void SpawnAtPointsTask(int delay, int npcId, int index)
    {
        if (!reward.IsStartProgress())
        {
            return;
        }
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (reward.IsStartProgress())
            {
                Spawn(npcId, index);
            }
        }, delay));
    }

    private void Spawn(int npcId, int index)
    {
        if (!reward.IsStartProgress())
        {
            return;
        }
        if (index >= 0)
        {
            WorldPosition point = points[index];
            if (point != null)
            {
                Spawn(npcId, point.GetX(), point.GetY(), point.GetZ(), point.GetHeading());
            }
        }
        else
        {
            foreach (WorldPosition point in points)
            {
                if (!reward.IsStartProgress())
                {
                    break;
                }
                Spawn(npcId, point.GetX(), point.GetY(), point.GetZ(), point.GetHeading());
            }
        }
    }

    private void RndSpawnTask(int delay, int npcId, int amount, int minRange, int maxRange)
    {
        if (!reward.IsStartProgress())
        {
            return;
        }
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (reward.IsStartProgress())
            {
                RndSpawnInRange(npcId, amount, minRange, maxRange);
            }
        }, delay));
    }

    private void RndSpawnInRange(int npcId, int amount, int minRange, int maxRange)
    {
        for (int i = 0; i < amount; i++)
        {
            if (!reward.IsStartProgress())
            {
                break;
            }
            SpawnAtRandomPositionWithinRange(npcId, minRange, maxRange);
        }
    }

    private void SpawnAtRandomPositionWithinRange(int npcId, int minRange, int maxRange)
    {
        if (!reward.IsStartProgress())
        {
            return;
        }
        List<int> ranges = Enumerable.Range(minRange, maxRange + 1 - minRange).ToList();
        Shuffle(ranges);
        double angleRadians = Rnd.NextFloat(360f) * Math.PI / 180.0;
        foreach (int range in ranges)
        {
            float x = 231.14f + (float)(Math.Cos(angleRadians) * range);
            float y = 264.399f + (float)(Math.Sin(angleRadians) * range);
            if (IsValidPoint(x, y))
            {
                Spawn(npcId, x, y, 96.51f, (byte)50);
                break;
            }
        }
    }

    private static void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rnd.Get(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void SpawnGuardianStone(bool isElyos, int stage)
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_OBJ_Start());
        switch (stage)
        {
            case 1:
                Spawn((isElyos ? 855763 : 856466), 231.14f, 264.399f, 96.5f, (byte)10);
                break;
            case 2:
                Spawn((isElyos ? 855786 : 856467), 231.14f, 264.399f, 96.5f, (byte)10);
                break;
            case 3:
                Spawn((isElyos ? 855809 : 856468), 231.14f, 264.399f, 96.5f, (byte)10);
                break;
            case 4:
                Spawn((isElyos ? 855832 : 856469), 231.14f, 264.399f, 96.5f, (byte)10);
                break;
        }
    }

    private bool IsValidPoint(float x, float y)
    {
        return (PositionUtil.GetDistance(x, y, 211.254f, 264.134f) >= 2.5) && (PositionUtil.GetDistance(x, y, 230.8977f, 285.5198f) >= 2.5) && (
            PositionUtil.GetDistance(x, y, 251.3068f, 264.307f) >= 2.5) && (PositionUtil.GetDistance(x, y, 231.2034f, 243.8273f) >= 2.5);
    }

    private void AddWorldPoints()
    {
        points.Add(new WorldPosition(mapId, 208.4323f, 264.0647f, 96.223f, (byte)1));
        points.Add(new WorldPosition(mapId, 214.5657f, 281.1983f, 96.1398f, (byte)99));
        points.Add(new WorldPosition(mapId, 230.9258f, 288.3556f, 96.5095f, (byte)87));
        points.Add(new WorldPosition(mapId, 248.5251f, 281.1702f, 96.3423f, (byte)74));
        points.Add(new WorldPosition(mapId, 254.0089f, 264.1608f, 96.1255f, (byte)60));
        points.Add(new WorldPosition(mapId, 248.0067f, 247.8215f, 96.0116f, (byte)47));
        points.Add(new WorldPosition(mapId, 231.1899f, 240.6733f, 96.1348f, (byte)30));
        points.Add(new WorldPosition(mapId, 214.2921f, 247.5521f, 96.267f, (byte)20));
    }

    private void DespawnAll()
    {
        instance.ForEachNpc(npc => npc.GetController().Delete());
    }

    private void CancelAllTasks()
    {
        if (failTask != null && !failTask.IsCancelled)
        {
            failTask.Cancel(false);
        }
        tasks.Where(future => future != null && !future.IsCancelled).ToList().ForEach(future => future.Cancel(true));
    }

    public override bool OnDie(Player player, Creature lastAttacker)
    {
        CheckInstance();
        return true;
    }

    public override bool OnReviveEvent(Player player)
    {
        if (!deadPlayers.Contains(player.GetObjectId()))
        {
            deadPlayers.Add(player.GetObjectId());
        }
        PlayerReviveService.Revive(player, 100, 100, false, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        TeleportService.TeleportTo(player, instance, 152, 264, 103);
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_LEGION_DOMINION_MOVE_BIRTHAREA_FRIENDLY(player.GetName()));
        CheckInstance();
        return true;
    }

    public override void OnInstanceDestroy()
    {
        CancelAllTasks();
        reward.Clear();
        deadPlayers.Clear();
        allowedPlayers.Clear();
        instanceLegion = null;
        instanceRace = null;
        points.Clear();
        DespawnAll();
    }

    private bool AllPlayersAreDead()
    {
        foreach (Player p in instance.GetPlayersInside())
        {
            if (!p.IsDead())
            {
                return false;
            }
        }
        return true;
    }

    private void CheckInstance()
    {
        if (deadPlayers.Count >= allowedPlayers.Count || AllPlayersAreDead())
        {
            CheckRank(reward.GetPoints(), false);
        }
    }

    public override bool CanEnter(Player p)
    {
        lock (@lock)
        {
            if (reward.GetInstanceProgressionType() == InstanceProgressionType.END_PROGRESS)
            {
                PacketSendUtility.SendPacket(p, new SM_SYSTEM_MESSAGE(1402947));
                return false;
            }
            if (instanceLegion != null)
            {
                if (p.GetLegion() != null)
                {
                    if (p.GetLegion().GetCurrentLegionDominion() > 0)
                    {
                        if (reward.GetInstanceProgressionType() == InstanceProgressionType.PREPARING)
                        {
                            if (p.GetLegion().GetLegionId() == instanceLegion.GetLegionId())
                            {
                                if (allowedPlayers.Count < 12 || allowedPlayers.Contains(p.GetObjectId()))
                                {
                                    return true;
                                }
                                else
                                {
                                    PacketSendUtility.SendPacket(p, SM_SYSTEM_MESSAGE.STR_MSG_CANT_INSTANCE_TOO_MANY_MEMBERS(12, mapId));
                                }
                            }
                            else
                            {
                                PacketSendUtility.SendPacket(p, new SM_SYSTEM_MESSAGE(1402907));
                            }
                        }
                        else
                        {
                            PacketSendUtility.SendPacket(p, new SM_SYSTEM_MESSAGE(1400235));
                        }
                    }
                    else
                    {
                        PacketSendUtility.SendPacket(p, new SM_SYSTEM_MESSAGE(1402937));
                    }
                }
                else
                {
                    PacketSendUtility.SendPacket(p, new SM_SYSTEM_MESSAGE(1402906));
                }
            }
            return false;
        }
    }

    public override void LeaveInstance(Player player)
    {
        if (instanceLegion != null)
        {
            switch (instanceLegion.GetCurrentLegionDominion())
            {
                case 1:
                    TeleportService.TeleportTo(player, 220080000, 1770, 2556, 300);
                    break;
                case 2:
                    TeleportService.TeleportTo(player, 220080000, 1474, 1748, 330);
                    break;
                case 3:
                    TeleportService.TeleportTo(player, 220080000, 765, 1278, 255);
                    break;
                case 4:
                    TeleportService.TeleportTo(player, 210070000, 1356, 622, 584);
                    break;
                case 5:
                    TeleportService.TeleportTo(player, 210070000, 1195, 1607, 469);
                    break;
                case 6:
                    TeleportService.TeleportTo(player, 210070000, 2361, 1515, 440);
                    break;
                default:
                    TeleportService.MoveToBindLocation(player);
                    break;
            }
        }
        else
        {
            TeleportService.MoveToBindLocation(player);
        }
    }

    private void StartAethericFieldTask(int stage)
    {
        SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_OBJ_Bomb());
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (reward.IsStartProgress())
            {
                if (aethericKills < stage * 4)
                {
                    SendMsg(SM_SYSTEM_MESSAGE.STR_MSG_OBJ_Bomb_Die());
                    CheckRank(reward.GetPoints(), false);
                }
            }
        }, 120000));
    }
}
