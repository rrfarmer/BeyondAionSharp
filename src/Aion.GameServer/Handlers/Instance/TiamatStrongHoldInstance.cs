using System.Threading;
using Aion.GameServer.Ai;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.GameObjects.State;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;
using Aion.GameServer.World.Zone;
using Rnd = Aion.GameServer.Commons.Utils.Rnd;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/TiamatStrongHoldInstance (Cheatkiller) : GeneralInstanceHandler. @InstanceID(300510000). AtomicInteger/Boolean→int+Interlocked; onDie drakan-wave/reward webs + setDoorState; firstWave/secondWave/thirdWave + attackPlayer (AIState.WALKING, moveToTargetObject, SM_EMOTION); spawnKahrun/moveToForward escort; spawnColonels Rnd; spawnExitIfCleared; onEnterZone surama event; onInstanceCreate doors+colonels; isBoss. 1:1.</summary>
[InstanceID(300510000)]
public class TiamatStrongHoldInstance : GeneralInstanceHandler
{
    private int drakans;
    private int startSuramaEvent;
    private bool isInstanceDestroyed;

    public TiamatStrongHoldInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnDie(Npc npc)
    {
        if (isInstanceDestroyed)
        {
            return;
        }
        switch (npc.GetNpcId())
        {
            case 730612:
                FirstWave();
                break;
            case 219373:// ex 219421
            case 219369:// ex 219417
            case 219411:// ex 219459
            case 219370:// ex 219418
                int killedDrakans = Interlocked.Increment(ref drakans);
                if (killedDrakans == 5)
                    SecondWave();
                else if (killedDrakans == 12)
                    ThirdWave();
                break;
            case 219352: // ex 219400
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(283177, 1175.65f, 1069.08f, 498.52f, (byte)0); // ex 283913
                Spawn(701501, 1075.4409f, 1078.5071f, 787.685f, (byte)16);
                instance.SetDoorState(48, true);
                SpawnKahrun();
                break;
            case 219357:// ex 219405
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(701501, 1077.1716f, 1058.1995f, 787.685f, (byte)61);
                instance.SetDoorState(37, true);
                break;
            case 219358:// ex 219406
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(701541, 677.35785f, 1069.5361f, 499.86716f, (byte)0);
                Spawn(701527, 1073.948f, 1068.8732f, 787.685f, (byte)61);
                Spawn(730622, 652.4821f, 1069.0302f, 498.7787f, (byte)0, 82);
                Spawn(283178, 679.88f, 1068.88f, 504.2f, (byte)119);// ex 283916
                SpawnExitIfCleared();
                break;
            case 219353:// ex 219401
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(701501, 1071.5909f, 1040.6797f, 787.685f, (byte)23);
                instance.SetDoorState(711, true);
                break;
            case 219354:// ex 219402
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(283179, 1030.03f, 301.83f, 411f, (byte)26);// ex 283914
                Spawn(701501, 1086.274f, 1098.3997f, 787.685f, (byte)90);
                Spawn(730622, 1029.792f, 267.0502f, 409.7982f, (byte)0, 83);
                SpawnExitIfCleared();
                break;
            case 219355:// ex 219403
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(701501, 1063.5973f, 1092.7402f, 787.685f, (byte)107);
                instance.SetDoorState(51, true);
                instance.SetDoorState(54, true);
                instance.SetDoorState(78, true);
                instance.SetDoorState(11, true);
                instance.SetDoorState(79, true);
                break;
            case 219356:// ex 219404
                SendMsg(SM_SYSTEM_MESSAGE.STR_IDTIAMAT_TIAMAT_REWARD_SPAWN());
                Spawn(701501, 1099.8691f, 1047.1895f, 787.685f, (byte)64);
                Spawn(730622, 644.4221f, 1319.6221f, 488.7422f, (byte)0, 15);
                Spawn(800438, 665.63409f, 1319.7051f, 487.9f, (byte)61);
                Spawn(283180, 629.1f, 1319.5f, 501.2f, (byte)0);// ex 283915
                SpawnExitIfCleared();
                break;
        }
    }

    private void FirstWave()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            AttackPlayer((Npc)Spawn(219373, 1505.09f, 1068.54f, 491.38f, (byte)0));
            AttackPlayer((Npc)Spawn(219369, 1510.54f, 1058.04f, 491.5f, (byte)0));
            AttackPlayer((Npc)Spawn(219411, 1517.38f, 1063.5f, 491.52f, (byte)0));
            AttackPlayer((Npc)Spawn(219411, 1516.81f, 1073.6f, 491.52f, (byte)0));
            AttackPlayer((Npc)Spawn(219369, 1510.41f, 1078.8f, 491.52f, (byte)0));
        }, 5000L);
    }

    private void SecondWave()
    {
        AttackPlayer((Npc)Spawn(219370, 1426.08f, 1068.41f, 491.38f, (byte)0));
        AttackPlayer((Npc)Spawn(219369, 1430.3f, 1061.13f, 491.5f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1428.5f, 1056.6f, 491.52f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1439.49f, 1058.5f, 491.4f, (byte)0));
        AttackPlayer((Npc)Spawn(219369, 1430.3f, 1075.49f, 491.52f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1439.4f, 1078.6f, 491.4f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1428.5f, 1080.9f, 491.46f, (byte)0));
    }

    private void ThirdWave()
    {
        AttackPlayer((Npc)Spawn(219370, 1296.1f, 1068.3f, 491.38f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1290.9f, 1059.13f, 491.5f, (byte)0));
        AttackPlayer((Npc)Spawn(219369, 1300.6f, 1056.4f, 491.52f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1302.78f, 1053.55f, 491.4f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1290.94f, 1077.8f, 491.52f, (byte)0));
        AttackPlayer((Npc)Spawn(219369, 1300.6f, 1080.3f, 491.4f, (byte)0));
        AttackPlayer((Npc)Spawn(219411, 1302.78f, 1082.8f, 491.5f, (byte)0));
    }

    private void AttackPlayer(Npc npc)
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            if (!isInstanceDestroyed)
            {
                foreach (Player player in instance.GetPlayersInside())
                {
                    npc.SetTarget(player);
                    npc.GetAi().SetStateIfNot(AIState.WALKING);
                    npc.SetState(CreatureState.ACTIVE, true);
                    npc.GetMoveController().MoveToTargetObject();
                    PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
                }
            }
        }, 2000L);
    }

    private void SpawnKahrun()
    {
        ThreadPoolManager.GetInstance().Schedule(() =>
        {
            MoveToForward((Npc)Spawn(800463, 1201.272f, 1074.5463f, 491f, (byte)61), 1039.5f, 1075.9f, 497.3f, false);
            MoveToForward((Npc)Spawn(800463, 1201.272f, 1072.5137f, 491f, (byte)61), 1130, 1072, 497.3f, false);
            MoveToForward((Npc)Spawn(800463, 1192.8656f, 1071.1085f, 491f, (byte)61), 1112, 1070, 497, false);
            MoveToForward((Npc)Spawn(800463, 1201.272f, 1064.1759f, 491f, (byte)61), 1039, 1061, 497.3f, false);
            MoveToForward((Npc)Spawn(800463, 1208.4175f, 1071.1797f, 491f, (byte)61), 1133, 1072.5f, 497.3f, false);
            MoveToForward((Npc)Spawn(800463, 1192.8656f, 1068.3411f, 491f, (byte)61), 1114, 1067, 496.7f, false);
            MoveToForward((Npc)Spawn(800463, 1208.4175f, 1068.3979f, 491f, (byte)61), 1133.32f, 1066.47f, 497.3f, false);
            MoveToForward((Npc)Spawn(800463, 1201.272f, 1066.2085f, 491f, (byte)61), 1128.8f, 1067, 497.3f, false);
            MoveToForward((Npc)Spawn(800380, 1190.323f, 1068.1558f, 491.03488f, (byte)61), 1108, 1066, 497.3f, false);
            MoveToForward((Npc)Spawn(800374, 1188.4259f, 1066.4757f, 491.55029f, (byte)61), 1094, 1064, 497.4f, true);
            MoveToForward((Npc)Spawn(800374, 1188.2158f, 1074.2047f, 491.55029f, (byte)61), 1092.5f, 1074.6f, 497.4f, true);
            MoveToForward((Npc)Spawn(800376, 1190.3859f, 1071.6548f, 491.03488f, (byte)61), 1109, 1073, 497.2f, false);
            MoveToForward((Npc)Spawn(800461, 1184.7582f, 1068.6f, 491.03488f, (byte)61), 1111, 1068.6f, 497.33f, false);
            MoveToForward((Npc)Spawn(800460, 1184.7358f, 1070.77f, 491.03488f, (byte)61), 1111, 1071, 497, false);
            MoveToForward((Npc)Spawn(800347, 1178.0425f, 1072.28f, 491.02545f, (byte)61), 1106, 1072, 497.2f, false);
            MoveToForward((Npc)Spawn(800336, 1178.0559f, 1069.6f, 491.02545f, (byte)61), 1104, 1069, 497, true);
        }, 7000L);
    }

    private void MoveToForward(Npc npc, float x, float y, float z, bool despawn)
    {
        npc.GetAi().SetStateIfNot(AIState.WALKING);
        npc.SetState(CreatureState.ACTIVE, true);
        npc.GetMoveController().MoveToPoint(x, y, z);
        PacketSendUtility.BroadcastPacket(npc, new SM_EMOTION(npc, EmotionType.CHANGE_SPEED, 0, npc.GetObjectId()));
        if (despawn)
        {
            ThreadPoolManager.GetInstance().Schedule(() =>
            {
                if (npc.GetNpcId() == 800336)
                {
                    Spawn(800338, 1104, 1069f, 497, (byte)61);
                    Npc kahrun = GetNpc(800338);
                    PacketSendUtility.BroadcastMessage(kahrun, 1500599, 1000);
                    PacketSendUtility.BroadcastMessage(kahrun, 1500600, 5000);
                }
                npc.GetController().Delete();
            }, 13000L);
        }
    }

    private void SpawnColonels()
    {
        switch (Rnd.NextInt(4))
        {
            case 0:
                Spawn(219364, 763.4179f, 1445.6504f, 495.6519f, (byte)90);
                Spawn(219395, 893.7009f, 1445.4846f, 495.6421f, (byte)90);
                Spawn(219395, 893.3f, 1190.71f, 495.6f, (byte)30);
                Spawn(219395, 762.6f, 1192.1f, 495.6f, (byte)30);
                break;
            case 1:
                Spawn(219395, 763.4179f, 1445.6504f, 495.6519f, (byte)90);
                Spawn(219364, 893.7009f, 1445.4846f, 495.6421f, (byte)90);
                Spawn(219395, 893.3f, 1190.71f, 495.6f, (byte)30);
                Spawn(219395, 762.6f, 1192.1f, 495.6f, (byte)30);
                break;
            case 2:
                Spawn(219395, 763.4179f, 1445.6504f, 495.6519f, (byte)90);
                Spawn(219395, 893.7009f, 1445.4846f, 495.6421f, (byte)90);
                Spawn(219364, 893.3f, 1190.71f, 495.6f, (byte)30);
                Spawn(219395, 762.6f, 1192.1f, 495.6f, (byte)30);
                break;
            case 3:
                Spawn(219395, 763.4179f, 1445.6504f, 495.6519f, (byte)90);
                Spawn(219395, 893.7009f, 1445.4846f, 495.6421f, (byte)90);
                Spawn(219395, 893.3f, 1190.71f, 495.6f, (byte)30);
                Spawn(219364, 762.6f, 1192.1f, 495.6f, (byte)30);
                break;
        }
    }

    private void SpawnExitIfCleared()
    {
        if (instance.GetNpcs(219354, 219356, 219358).TrueForAll(n => n.IsDead()))
        {
            Spawn(800464, 1119.7076f, 1071.1401f, 496.8615f, (byte)119);
            Spawn(800465, 1119.7421f, 1068.4998f, 496.8616f, (byte)3);
            Spawn(730629, 1121.3807f, 1069.8124f, 500.3319f, (byte)0, 555);
        }
    }

    public override void OnEnterZone(Player player, ZoneInstance zone)
    {
        if (zone.GetAreaTemplate().GetZoneName() == ZoneName.Get("LAKSYAKA_LEGION_HQ_300510000"))
        {
            if (Interlocked.CompareExchange(ref startSuramaEvent, 1, 0) == 0)
            {
                Spawn(800433, 725.93f, 1319.9f, 490.7f, (byte)61);
            }
        }
        else if (zone.GetAreaTemplate().GetZoneName() == ZoneName.Get("GLORIOUS_NEXUS_300510000"))
        {
            player.GetEffectController().RemoveEffect(300);
        }
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        if (npc.GetNpcId() == 701494)
            instance.SetDoorState(22, true);
    }

    public override void OnInstanceCreate()
    {
        instance.SetDoorState(610, true);
        // instance.setDoorState(20, true);
        instance.SetDoorState(706, true);
        SpawnColonels();
    }

    public override void OnInstanceDestroy()
    {
        isInstanceDestroyed = true;
    }

    public override bool IsBoss(Npc npc)
    {
        return npc.GetNpcId() switch
        {
            219352 or 219353 or 219354 or 219355 or 219356 or 219357 or 219358 => true,
            _ => false,
        };
    }
}
