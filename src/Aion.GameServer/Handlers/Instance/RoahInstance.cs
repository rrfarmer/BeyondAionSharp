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

/// <summary>Java parity: instance/abyss/RoahInstance (bobobear) : GeneralInstanceHandler. @InstanceID(300070000); AtomicLong startTime compareAndSet(0,now)→Interlocked.CompareExchange==0; FlyRing/onPassFlyingRing/onEnterInstance/spawnGoldChest 1:1.</summary>
[InstanceID(300070000)]
public class RoahInstance : GeneralInstanceHandler
{
    private long startTime;
    private Race? instanceRace;

    public RoahInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        new FlyRing(new FlyRingTemplate("ROAH_WING_1", mapId, new Point3D(501.77, 409.53, 94.12), new Point3D(503.93, 409.65, 98.9),
            new Point3D(506.26, 409.7, 94.15), 10), instance.GetInstanceId()).Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("ROAH_WING_1"))
        {
            if (Interlocked.CompareExchange(ref startTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0) == 0)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_START_IDABRE());
                PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 900));
                ThreadPoolManager.GetInstance().Schedule(_ => { DeleteAliveNpcs(700472, 700473, 700474, 701489, 701484); return ValueTask.CompletedTask; }, 900000L);
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
        Spawn(instanceRace == Race.ELYOS ? 701484 : 701489, 504.44f, 460.57f, 86.88f, (byte)60);
    }
}
