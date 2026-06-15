using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;

namespace Aion.GameServer.Handlers.AI;

/// <summary>Java parity: ai/instance/illuminaryObelisk/IDF5_U3_VortexAI (@author Estrayl).</summary>
[AIName("idf5_u3_vortex")]
public class IDF5_U3_VortexAI : NoActionAI
{
    private readonly List<ScheduledTask> tasks = new List<ScheduledTask>();
    private readonly List<int> npcIds = new List<int>();

    public IDF5_U3_VortexAI(Npc owner)
        : base(owner)
    {
    }

    protected override void HandleSpawned()
    {
        base.HandleSpawned();
        if (GetPosition().GetMapId() == 301230000) // normal mode
            npcIds.AddRange(new[] { 233857, 233880, 233881 });
        else // hard mode
            npcIds.AddRange(new[] { 234687, 234688, 234689 });
        lock (tasks)
        {
            tasks.Add(ThreadPoolManager.GetInstance().ScheduleAtFixedRateTask(_ => { HandlePhaseAttacks(); return ValueTask.CompletedTask; }, TimeSpan.FromMilliseconds(120000), TimeSpan.FromMilliseconds(120000)));
        }
    }

    private void HandlePhaseAttacks()
    {
        switch (GetNpcId())
        {
            case 702014: // east
                SpawnWalker(npcIds[0], 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_1");
                SpawnWalker(npcIds[0], 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                SpawnWalker(npcIds[1], 256.6376f, 328.7015f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                SpawnWalker(npcIds[1], 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_4");
                SpawnWalker(npcIds[1], 256.9199f, 326.4982f, 325.0038f, (byte)90, 0, "idf5_u3_east_5");
                SpawnWalker(npcIds[1], 253.8757f, 326.5010f, 325.0038f, (byte)90, 0, "idf5_u3_east_6");
                SpawnWalker(npcIds[0], 252.3243f, 328.5881f, 325.0092f, (byte)90, 15000, "idf5_u3_east_1");
                SpawnWalker(npcIds[0], 255.3635f, 328.5584f, 325.0038f, (byte)90, 15000, "idf5_u3_east_2");
                SpawnWalker(npcIds[2], 256.6376f, 328.7015f, 325.0038f, (byte)90, 15000, "idf5_u3_east_3");
                SpawnWalker(npcIds[2], 258.5159f, 328.5792f, 325.0038f, (byte)90, 15000, "idf5_u3_east_4");
                SpawnWalker(npcIds[2], 256.9199f, 326.4982f, 325.0038f, (byte)90, 15000, "idf5_u3_east_5");
                SpawnWalker(npcIds[2], 253.8757f, 326.5010f, 325.0038f, (byte)90, 15000, "idf5_u3_east_6");
                break;
            case 702015: // west
                SpawnWalker(npcIds[0], 251.9594f, 183.4159f, 325.0038f, (byte)30, 0, "idf5_u3_west_1");
                SpawnWalker(npcIds[0], 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                SpawnWalker(npcIds[1], 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                SpawnWalker(npcIds[1], 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                SpawnWalker(npcIds[1], 258.7057f, 183.6840f, 325.0038f, (byte)30, 0, "idf5_u3_west_5");
                SpawnWalker(npcIds[1], 255.0448f, 185.5452f, 325.0038f, (byte)30, 0, "idf5_u3_west_6");
                SpawnWalker(npcIds[0], 251.9594f, 183.4159f, 325.0038f, (byte)30, 15000, "idf5_u3_west_1");
                SpawnWalker(npcIds[0], 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                SpawnWalker(npcIds[2], 255.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                SpawnWalker(npcIds[2], 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");
                SpawnWalker(npcIds[2], 258.7057f, 183.6840f, 325.0038f, (byte)30, 15000, "idf5_u3_west_5");
                SpawnWalker(npcIds[2], 255.0448f, 185.5452f, 325.0038f, (byte)30, 15000, "idf5_u3_west_6");
                break;
            case 702016: // south
                SpawnWalker(npcIds[0], 326.3734f, 251.2209f, 291.8364f, (byte)60, 0, "idf5_u3_south_1");
                SpawnWalker(npcIds[0], 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                SpawnWalker(npcIds[1], 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                SpawnWalker(npcIds[1], 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                SpawnWalker(npcIds[1], 326.4354f, 257.6836f, 291.8466f, (byte)60, 0, "idf5_u3_south_5");
                SpawnWalker(npcIds[1], 324.7853f, 254.2962f, 291.8364f, (byte)60, 0, "idf5_u3_south_6");
                SpawnWalker(npcIds[0], 326.3734f, 251.2209f, 291.8364f, (byte)60, 15000, "idf5_u3_south_1");
                SpawnWalker(npcIds[0], 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                SpawnWalker(npcIds[2], 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                SpawnWalker(npcIds[2], 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                SpawnWalker(npcIds[2], 326.4354f, 257.6836f, 291.8466f, (byte)60, 15000, "idf5_u3_south_5");
                SpawnWalker(npcIds[2], 324.7853f, 254.2962f, 291.8364f, (byte)60, 15000, "idf5_u3_south_6");
                break;
            case 702017: // north
                SpawnWalker(npcIds[0], 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_1");
                SpawnWalker(npcIds[0], 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                SpawnWalker(npcIds[1], 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                SpawnWalker(npcIds[1], 184.7428f, 251.3166f, 291.8842f, (byte)0, 0, "idf5_u3_north_4");
                SpawnWalker(npcIds[1], 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_5");
                SpawnWalker(npcIds[1], 186.8694f, 254.6730f, 291.8364f, (byte)0, 0, "idf5_u3_north_6");
                SpawnWalker(npcIds[0], 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_1");
                SpawnWalker(npcIds[0], 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_2");
                SpawnWalker(npcIds[2], 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                SpawnWalker(npcIds[2], 184.7428f, 251.3166f, 291.8842f, (byte)0, 15000, "idf5_u3_north_4");
                SpawnWalker(npcIds[2], 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_5");
                SpawnWalker(npcIds[2], 186.8694f, 254.6730f, 291.8364f, (byte)0, 15000, "idf5_u3_north_6");
                break;
        }
    }

    private void SpawnWalker(int npcId, float x, float y, float z, byte h, int delay, string walkerId)
    {
        AddTask(() =>
        {
            Npc npc = (Npc)Spawn(npcId, x, y, z, (sbyte)h);
            npc.GetSpawn().SetWalkerId(walkerId);
            AddTask(() =>
            {
                WalkManager.StartWalking((NpcAI)npc.GetAi());
                npc.SetState(CreatureState.ACTIVE, true);
                PacketSendUtility.BroadcastToMap(GetOwner(), new SM_EMOTION(GetOwner(), EmotionType.RUN));
            }, 2500);
        }, delay);
    }

    private void AddTask(Action task, int delayMs)
    {
        lock (tasks)
        {
            if (tasks.Count != 0)
                tasks.Add(ThreadPoolManager.GetInstance().Schedule(task, delayMs));
        }
    }

    protected override void HandleDespawned()
    {
        lock (tasks)
        {
            tasks.Where(t => !t.IsDone()).ToList().ForEach(t => t.Cancel(true));
            tasks.Clear();
        }
        base.HandleDespawned();
    }
}
