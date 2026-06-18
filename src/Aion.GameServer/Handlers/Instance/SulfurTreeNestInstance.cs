using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/SulfurTreeNestInstance (xTz) : GeneralInstanceHandler. @InstanceID(300060000); AtomicLong startTime compareAndSet(0,now)→Interlocked.CompareExchange==0; FlyRing/onPassFlyingRing/onEnterInstance/spawnGoldChest 1:1.</summary>
[InstanceID(300060000)]
public class SulfurTreeNestInstance : GeneralInstanceHandler
{
    private long startTime;
    private Race? instanceRace;

    public SulfurTreeNestInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        new FlyRing(new FlyRingTemplate("SULFUR_1", mapId, new Point3D(462.9394, 380.34888, 168.97256), new Point3D(462.9394, 380.34888,
            174.97256), new Point3D(468.9229, 380.7933, 168.97256), 6), instance.GetInstanceId()).Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("SULFUR_1"))
        {
            if (Interlocked.CompareExchange(ref startTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0) == 0)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_START_IDABRE());
                PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 900));
                ThreadPoolManager.GetInstance().Schedule(_ => { DeleteAliveNpcs(214804, 700463, 700462, 700464, 701485, 701480); return ValueTask.CompletedTask; }, 900000L);
            }
        }
        return false;
    }

    public override void OnEnterInstance(Player player)
    {
        long start = Volatile.Read(ref startTime);
        if (start > 0)
        {
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - start;
            if (time < 900000)
            {
                PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 900 - (int)time / 1000));
            }
        }

        if (instanceRace == null)
        {
            instanceRace = player.GetRace();
            SpawnGoldChest();
        }
    }

    private void SpawnGoldChest()
    {
        Spawn(instanceRace == Race.ELYOS ? 701480 : 701485, 482.87f, 474.07f, 163.16f, (byte)90);
    }
}
