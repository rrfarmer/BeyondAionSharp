using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Ai.Manager;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Animations;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Players;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.SkillEngine.Model;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using SkillEngineSvc = Aion.GameServer.SkillEngine.SkillEngine;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/IlluminaryObeliskInstance (Estrayl) : GeneralInstanceHandler. @InstanceID(301230000). AtomicBoolean→int+Interlocked; scheduleInstanceStart→scheduleWipe timer cascade; onSpawn generator charge msgs + scheduleChargeAttacks (4 directions, 3 waves each, walkers); checkGenerators→endboss; wipe; handleUseItemFinish teleports/skill; onEnterInstance race herald; onEndEffect 21511; onDie exit; isBoss Dynatoum. 1:1.</summary>
[InstanceID(301230000)]
public class IlluminaryObeliskInstance : GeneralInstanceHandler
{
    private int isRaceSet;
    private readonly List<ScheduledTask> tasks = new();
    public bool isInstanceDestroyed;

    public IlluminaryObeliskInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        ScheduleInstanceStart();
    }

    private void ScheduleInstanceStart()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_DOOR_OPEN());
            instance.SetDoorState(129, true);
            ScheduleWipe(3000);
        }, 60000L);
    }

    protected void ScheduleWipe(int delay)
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (isInstanceDestroyed)
                return;
            switch (delay)
            {
                case 3000: // 30min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_01());
                    ScheduleWipe(300000);
                    break;
                case 300000: // 25min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_02());
                    ScheduleWipe(300001);
                    break;
                case 300001: // 20min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_03());
                    ScheduleWipe(300002);
                    break;
                case 300002: // 15min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_04());
                    ScheduleWipe(300003);
                    break;
                case 300003: // 10min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_05());
                    ScheduleWipe(300004);
                    break;
                case 300004: // 5min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_06());
                    ScheduleWipe(240000);
                    break;
                case 240000: // 1min
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_07());
                    ScheduleWipe(60000);
                    break;
                case 60000: // wipe
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_GAME_TIMER_08());
                    Wipe();
                    break;
            }
        }, delay));
    }

    public override void OnSpawn(VisibleObject obj)
    {
        if (obj is Npc npc)
        {
            int npcId = npc.GetNpcId();
            switch (npc.GetNpcId())
            {
                case 702218:
                case 702219:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_01());
                    break;
                case 702220:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_01());
                    CheckGenerators();
                    break;
                case 702221:
                case 702222:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_02());
                    break;
                case 702223:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_FINAL_CHARGE_02());
                    CheckGenerators();
                    break;
                case 702224:
                case 702225:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_03());
                    break;
                case 702226:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_FINAL_CHARGE_03());
                    CheckGenerators();
                    break;
                case 702227:
                case 702228:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_CHARGE_04());
                    break;
                case 702229:
                    PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_FINAL_CHARGE_04());
                    CheckGenerators();
                    break;
            }
            ScheduleChargeAttacks(npcId);
        }
    }

    protected virtual void ScheduleChargeAttacks(int npcId)
    {
        switch (npcId)
        {
            case 702218: // east first wave
                Spawn(233720, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(233721, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(233721, 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(233722, 255.3635f, 328.5584f, 325.0038f, (byte)90, 15000, "idf5_u3_east_2");
                Spawn(233720, 258.5159f, 328.5792f, 325.0038f, (byte)90, 15000, "idf5_u3_east_3");
                Spawn(233720, 252.3243f, 328.5881f, 325.0092f, (byte)90, 15000, "idf5_u3_east_4");
                Spawn(233723, 255.3635f, 328.5584f, 325.0038f, (byte)90, 30000, "idf5_u3_east_2");
                Spawn(233726, 258.5159f, 328.5792f, 325.0038f, (byte)90, 30000, "idf5_u3_east_3");
                Spawn(233726, 252.3243f, 328.5881f, 325.0092f, (byte)90, 30000, "idf5_u3_east_4");
                break;
            case 702219: // east second wave
                Spawn(233723, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(233726, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(233726, 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(233728, 255.3635f, 328.5584f, 325.0038f, (byte)90, 15000, "idf5_u3_east_2");
                Spawn(233721, 258.5159f, 328.5792f, 325.0038f, (byte)90, 15000, "idf5_u3_east_3");
                Spawn(233721, 252.3243f, 328.5881f, 325.0092f, (byte)90, 15000, "idf5_u3_east_4");
                Spawn(233722, 255.3635f, 328.5584f, 325.0038f, (byte)90, 30000, "idf5_u3_east_2");
                Spawn(233720, 258.5159f, 328.5792f, 325.0038f, (byte)90, 30000, "idf5_u3_east_3");
                Spawn(233720, 252.3243f, 328.5881f, 325.0092f, (byte)90, 30000, "idf5_u3_east_4");
                break;
            case 702220: // east third wave
                Spawn(233721, 252.3243f, 328.5881f, 325.0092f, (byte)90, 0, "idf5_u3_east_1");
                Spawn(233726, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(233721, 256.6376f, 328.7015f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(233726, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(233736, 253.8757f, 326.5010f, 325.0038f, (byte)90, 0, "idf5_u3_east_6");
                Spawn(233720, 255.3635f, 328.5584f, 325.0038f, (byte)90, 0, "idf5_u3_east_2");
                Spawn(233724, 256.6376f, 328.7015f, 325.0038f, (byte)90, 0, "idf5_u3_east_3");
                Spawn(233720, 258.5159f, 328.5792f, 325.0038f, (byte)90, 0, "idf5_u3_east_4");
                Spawn(233733, 256.9199f, 326.4982f, 325.0038f, (byte)90, 0, "idf5_u3_east_5");
                break;
            case 702221: // west first wave
                Spawn(233720, 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                Spawn(233723, 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                Spawn(233720, 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                Spawn(233721, 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                Spawn(233724, 255.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                Spawn(233721, 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");

                Spawn(233722, 253.5314f, 183.5728f, 325.0038f, (byte)30, 30000, "idf5_u3_west_2");
                Spawn(233725, 255.2491f, 183.4584f, 325.0038f, (byte)30, 30000, "idf5_u3_west_3");
                Spawn(233722, 257.0595f, 183.5797f, 325.0045f, (byte)30, 30000, "idf5_u3_west_4");
                break;
            case 702222: // west second wave
                Spawn(233721, 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                Spawn(233720, 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                Spawn(233721, 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                Spawn(233726, 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                Spawn(233727, 255.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                Spawn(233726, 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");

                Spawn(233725, 253.5314f, 183.5728f, 325.0038f, (byte)30, 30000, "idf5_u3_west_2");
                Spawn(233732, 255.2491f, 183.4584f, 325.0038f, (byte)30, 30000, "idf5_u3_west_3");
                Spawn(233725, 257.0595f, 183.5797f, 325.0045f, (byte)30, 30000, "idf5_u3_west_4");
                break;
            case 702223: // west third wave
                Spawn(233721, 251.9594f, 183.4159f, 325.0038f, (byte)30, 0, "idf5_u3_west_1");
                Spawn(233722, 253.5314f, 183.5728f, 325.0038f, (byte)30, 0, "idf5_u3_west_2");
                Spawn(233722, 255.2491f, 183.4584f, 325.0038f, (byte)30, 0, "idf5_u3_west_3");
                Spawn(233721, 257.0595f, 183.5797f, 325.0045f, (byte)30, 0, "idf5_u3_west_4");
                Spawn(233737, 255.0448f, 185.5452f, 325.0038f, (byte)30, 0, "idf5_u3_west_6");
                Spawn(233725, 253.5314f, 183.5728f, 325.0038f, (byte)30, 15000, "idf5_u3_west_2");
                Spawn(233720, 252.2491f, 183.4584f, 325.0038f, (byte)30, 15000, "idf5_u3_west_3");
                Spawn(233731, 257.0595f, 183.5797f, 325.0045f, (byte)30, 15000, "idf5_u3_west_4");
                Spawn(233725, 258.7057f, 183.6840f, 325.0038f, (byte)30, 15000, "idf5_u3_west_5");
                break;
            case 702224: // south first wave
                Spawn(233722, 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                Spawn(233723, 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                Spawn(233722, 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                Spawn(233725, 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                Spawn(233730, 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                Spawn(233725, 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                Spawn(233726, 326.3337f, 252.6159f, 291.8364f, (byte)60, 30000, "idf5_u3_south_2");
                Spawn(233727, 326.3333f, 253.1857f, 291.8364f, (byte)60, 30000, "idf5_u3_south_3");
                Spawn(233726, 326.4392f, 255.9983f, 291.8364f, (byte)60, 30000, "idf5_u3_south_4");
                break;
            case 702225: // south second wave
                Spawn(233722, 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                Spawn(233723, 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                Spawn(233722, 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                Spawn(233725, 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                Spawn(233730, 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                Spawn(233725, 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                Spawn(233726, 326.3337f, 252.6159f, 291.8364f, (byte)60, 30000, "idf5_u3_south_2");
                Spawn(233727, 326.3333f, 253.1857f, 291.8364f, (byte)60, 30000, "idf5_u3_south_3");
                Spawn(233726, 326.4392f, 255.9983f, 291.8364f, (byte)60, 30000, "idf5_u3_south_4");
                break;
            case 702226: // south third wave
                Spawn(233725, 326.3734f, 251.2209f, 291.8364f, (byte)60, 0, "idf5_u3_south_1");
                Spawn(233720, 326.3337f, 252.6159f, 291.8364f, (byte)60, 0, "idf5_u3_south_2");
                Spawn(233720, 326.3333f, 253.1857f, 291.8364f, (byte)60, 0, "idf5_u3_south_3");
                Spawn(233725, 326.4392f, 255.9983f, 291.8364f, (byte)60, 0, "idf5_u3_south_4");
                Spawn(233738, 324.7853f, 254.2962f, 291.8364f, (byte)60, 0, "idf5_u3_south_6");
                Spawn(233722, 326.3337f, 252.6159f, 291.8364f, (byte)60, 15000, "idf5_u3_south_2");
                Spawn(233722, 326.3333f, 253.1857f, 291.8364f, (byte)60, 15000, "idf5_u3_south_3");
                Spawn(233735, 326.4392f, 255.9983f, 291.8364f, (byte)60, 15000, "idf5_u3_south_4");
                Spawn(233723, 326.4354f, 257.6836f, 291.8466f, (byte)60, 15000, "idf5_u3_south_5");
                break;
            case 702227: // north first wave
                Spawn(233722, 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                Spawn(233727, 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                Spawn(233722, 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_4");
                Spawn(233725, 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_2");
                Spawn(233723, 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                Spawn(233725, 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_4");
                Spawn(233725, 184.6565f, 256.3191f, 291.8364f, (byte)0, 30000, "idf5_u3_north_2");
                Spawn(233729, 184.6134f, 253.0914f, 291.8364f, (byte)0, 30000, "idf5_u3_north_3");
                Spawn(233725, 184.6415f, 253.7202f, 291.8364f, (byte)0, 30000, "idf5_u3_north_4");
                Spawn(233882, 253.1755f, 252.6574f, 298.2540f, (byte)60, 30000, "idf5_u3_hide_1");
                Spawn(233883, 253.1821f, 254.5660f, 298.2540f, (byte)60, 30000, "idf5_u3_hide_2");
                Spawn(233882, 253.3598f, 256.3680f, 298.2540f, (byte)60, 30000, "idf5_u3_hide_3");
                break;
            case 702228: // north second wave
                Spawn(233726, 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                Spawn(233723, 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                Spawn(233726, 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_4");
                Spawn(233722, 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_2");
                Spawn(233724, 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                Spawn(233722, 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_4");
                Spawn(233720, 184.6565f, 256.3191f, 291.8364f, (byte)0, 30000, "idf5_u3_north_2");
                Spawn(233734, 184.6415f, 253.7202f, 291.8364f, (byte)0, 30000, "idf5_u3_north_3");
                Spawn(233720, 184.6134f, 253.0914f, 291.8364f, (byte)0, 30000, "idf5_u3_north_4");
                break;
            case 702229: // north third wave
                Spawn(233725, 184.6565f, 256.3191f, 291.8364f, (byte)0, 0, "idf5_u3_north_1");
                Spawn(233720, 184.6415f, 253.7202f, 291.8364f, (byte)0, 0, "idf5_u3_north_2");
                Spawn(233724, 184.6134f, 253.0914f, 291.8364f, (byte)0, 0, "idf5_u3_north_3");
                Spawn(233725, 184.7428f, 251.3166f, 291.8842f, (byte)0, 0, "idf5_u3_north_4");
                Spawn(233731, 186.8694f, 254.6730f, 291.8364f, (byte)0, 0, "idf5_u3_north_6");
                Spawn(233722, 184.7428f, 251.3166f, 291.8842f, (byte)0, 15000, "idf5_u3_north_2");
                Spawn(233721, 184.6565f, 256.3191f, 291.8364f, (byte)0, 15000, "idf5_u3_north_3");
                Spawn(233739, 184.6415f, 253.7202f, 291.8364f, (byte)0, 15000, "idf5_u3_north_4");
                Spawn(233722, 184.6134f, 253.0914f, 291.8364f, (byte)0, 15000, "idf5_u3_north_5");
                break;
        }
    }

    private void CheckGenerators()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            for (int id = 702220; id <= 702229; id += 3)
            {
                if (instance.GetNpc(id) == null)
                    return;
            }
            CancelTasks();
            PacketSendUtility.BroadcastToMap(instance, SM_SYSTEM_MESSAGE.STR_MSG_IDF5_U3_OBJ_ALL_COMPLETE());

            instance.ForEachNpc(npc => npc.GetController().Delete());

            Spawn(730886, 255.49f, 293.03f, 321.1850f, (byte)30);
            Spawn(730886, 255.49f, 215.80f, 321.2134f, (byte)30);
            Spawn(730886, 294.53f, 254.65f, 295.7718f, (byte)60);
            Spawn(730886, 216.80f, 254.65f, 295.7729f, (byte)0);
            SpawnEndboss(233740);
        }, 30000L);
    }

    protected virtual void SpawnEndboss(int npcId)
    {
        Spawn(npcId, 255.48956f, 254.5804f, 455.1201f, (byte)15);
    }

    protected void Wipe()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (isInstanceDestroyed)
                return;
            instance.ForEachObject(o =>
            {
                if (o is Npc npc)
                    npc.GetController().Delete();
                else if (o is Player p && !p.IsDead())
                    p.GetController().Die();
            });
        }, 5000L);
    }

    protected void Spawn(int npcId, float x, float y, float z, byte h, int delay, string walkerId)
    {
        tasks.Add(ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                Npc npc = (Npc)Spawn(npcId, x, y, z, h);
                npc.GetSpawn().SetWalkerId(walkerId);
                tasks.Add(ThreadPoolManager.GetInstance().Schedule(() => WalkManager.StartWalking((NpcAI)npc.GetAi()), 2500L));
            }
        }, delay));
    }

    private void CancelTasks()
    {
        tasks.Where(t => t != null && !t.IsCancelled).ToList().ForEach(t => t.Cancel(true));
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        switch (npc.GetNpcId())
        {
            case 730886:
                TeleportService.TeleportTo(player, instance, 265.45142f, 264.52875f, 455.1256f, (byte)75);
                break;
            case 702009:
                SkillEngineSvc.GetInstance().GetSkill(npc, 21511, 1, player).UseSkill();
                TeleportService.TeleportTo(player, instance, npc.GetX(), npc.GetY(), npc.GetZ(), npc.GetHeading(), TeleportAnimation.FADE_OUT_BEAM);
                npc.GetController().Delete();
                break;
            case 730905:
                TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
                break;
        }
    }

    public override void OnEnterInstance(Player player)
    {
        // TODO: movie id PacketSendUtility.sendPacket(player, new SM_PLAY_MOVIE(0, 0, ???, 0));
        if (Interlocked.CompareExchange(ref isRaceSet, 1, 0) == 0)
        {
            int npcId = player.GetRace() == Race.ASMODIANS ? 802049 : 802048;
            Spawn(npcId, 315.74573f, 306.9366f, 405.49997f, (byte)15);
        }
    }

    public override void OnEndEffect(Effect effect)
    {
        if (effect.GetSkillId() == 21511)
        {
            Creature effected = effect.GetEffected();
            Spawn(702009, effected.GetX(), effected.GetY(), effected.GetZ(), effected.GetHeading());
        }
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        int npcId = npc.GetNpcId();
        if (npcId == 233740 || npcId == 234686)
            Spawn(730905, 267.64062f, 267.84793f, 276.65512f, (byte)75); // exit
    }

    public override bool OnReviveEvent(Player player)
    {
        PlayerReviveService.Revive(player, 25, 25, true, 0);
        player.GetGameStats().UpdateStatsAndSpeedVisually();
        PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_REBIRTH_MASSAGE_ME());
        TeleportService.TeleportTo(player, instance, 271.1714f, 271.4455f, 276.67294f, (byte)75);
        return true;
    }

    public override void LeaveInstance(Player player)
    {
        TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }

    public override void OnInstanceDestroy()
    {
        isInstanceDestroyed = true;
        CancelTasks();
    }

    public override bool IsBoss(Npc npc)
    {
        return npc.GetNpcId() == 233740 || npc.GetNpcId() == 234686; // Dynatoum
    }
}
