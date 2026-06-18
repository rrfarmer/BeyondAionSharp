using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/KrotanBarracks (Estrayl, AION 4.8) : AbstractInnerUpperAbyssInstance. @InstanceID(301300000). 1:1.</summary>
[InstanceID(301300000)]
public class KrotanBarracks : AbstractInnerUpperAbyssInstance
{
    public KrotanBarracks(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBossId()
    {
        return 233633;
    }

    protected override int GetChestId()
    {
        return 702288;
    }

    protected override int GetDoorId()
    {
        return 700545;
    }

    protected override int GetKeymasterId()
    {
        return 233627;
    }

    protected override int GetTimerNpcId()
    {
        return 206095;
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 233599: OpenDoor(10); break;
            case 233600: OpenDoor(5); break;
            case 233601: OpenDoor(30); break;
            case 233602: OpenDoor(6); break;
            case 233611: OpenDoor(32); break;
            case 233612: OpenDoor(31); break;
            case 233613: OpenDoor(12); break;
            case 233614: OpenDoor(29); break;
            case 233623: OpenDoor(13); break;
            case 233624: OpenDoor(8); break;
            case 233625: OpenDoor(7); break;
            case 233626: OpenDoor(9); break;
            case 233631: OpenDoor(16); break;
            case 233632: case 233633: SpawnChests(); break;
            case 215413: SwitchToEasyMode(); break;
            case 235536: RewardStatueKill(3000); break;
            case 235537: RewardStatueKill(6000); break;
            case 235538: RewardStatueKill(12000); break;
        }
    }
}
