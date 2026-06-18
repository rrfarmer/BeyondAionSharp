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

/// <summary>Java parity: instance/abyss/AsteriaInstance (bobobear) : GeneralInstanceHandler. @InstanceID(300050000); AtomicLong startTime compareAndSet(0,now)→long+Interlocked.CompareExchange==0; FlyRing/onPassFlyingRing/onEnterInstance/spawnGoldChest 1:1; ThreadPoolManager.schedule(...,900000) deleteAliveNpcs.</summary>
[InstanceID(300050000)]
public class AsteriaInstance : GeneralInstanceHandler
{
    private long startTime;
    private Race? instanceRace;

    public AsteriaInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        new FlyRing(new FlyRingTemplate("ASTERIA_WING_1", mapId, new Point3D(479.24, 572.57, 202.72), new Point3D(477.95, 567.64, 212.9),
            new Point3D(477.97, 563.35, 202.12), 10), instance.GetInstanceId()).Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("ASTERIA_WING_1"))
        {
            if (Interlocked.CompareExchange(ref startTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0) == 0)
            {
                PacketSendUtility.SendPacket(player, SM_SYSTEM_MESSAGE.STR_MSG_INSTANCE_START_IDABRE());
                PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 900));
                ThreadPoolManager.GetInstance().Schedule(_ => { DeleteAliveNpcs(700475, 700476, 700477, 701483, 701488); return ValueTask.CompletedTask; }, 900000L);
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
        Spawn(instanceRace == Race.ELYOS ? 701483 : 701488, 512.8f, 565.35f, 198f, (byte)60);
    }
}
