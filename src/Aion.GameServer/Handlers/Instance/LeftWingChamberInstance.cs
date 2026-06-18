using System;
using System.Threading;
using System.Threading.Tasks;
using Aion.GameServer.Commons.Utils;
using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.Flyring;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Geometry;
using Aion.GameServer.Model.Templates.Flyring;
using Aion.GameServer.Network.Aion.ServerPackets;
using Aion.GameServer.Services.Teleport;
using Aion.GameServer.Utils;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/LeftWingChamberInstance (xTz) : GeneralInstanceHandler. @InstanceID(300080000); Rnd.nextBoolean Treasurer Nabatma pool spawn; AtomicLong startTime compareAndSet(0,now)→Interlocked.CompareExchange==0; FlyRing/onPassFlyingRing/onEnterInstance/spawnGoldChest/handleUseItemFinish 1:1.</summary>
[InstanceID(300080000)]
public class LeftWingChamberInstance : GeneralInstanceHandler
{
    private long startTime;
    private Race? instanceRace;

    public LeftWingChamberInstance(WorldMapInstance instance) : base(instance)
    {
    }

    public override void OnInstanceCreate()
    {
        SpawnRings();
        if (Rnd.NextBoolean()) // spawn Treasurer Nabatma (pool=1)
            Spawn(215424, 502.1326f, 502.89673f, 352.94437f, (byte)20);
        else
            Spawn(215424, 508.71814f, 660.93994f, 352.94638f, (byte)60);
    }

    private void SpawnRings()
    {
        FlyRing f1 = new FlyRing(new FlyRingTemplate("LEFT_WING_1", mapId, new Point3D(576.2102, 585.4146, 353.90677),
            new Point3D(576.2102, 585.4146, 359.90677), new Point3D(575.18384, 596.36664, 353.90677), 10), instance.GetInstanceId());
        f1.Spawn();
    }

    public override bool OnPassFlyingRing(Player player, string flyingRing)
    {
        if (flyingRing.Equals("LEFT_WING_1"))
        {
            if (Interlocked.CompareExchange(ref startTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), 0) == 0)
            {
                PacketSendUtility.SendPacket(player, new SM_QUEST_ACTION(0, 900));
                ThreadPoolManager.GetInstance().Schedule(_ => { DeleteAliveNpcs(700466, 701481, 701486); return ValueTask.CompletedTask; }, 900000L);
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
        Spawn(instanceRace == Race.ELYOS ? 701481 : 701486, 496.87f, 664.07f, 352.94f, (byte)90);
    }

    public override void HandleUseItemFinish(Player player, Npc npc)
    {
        if (npc.GetNpcId() == 700455)
            TeleportService.MoveToInstanceExit(player, mapId, player.GetRace());
    }
}
