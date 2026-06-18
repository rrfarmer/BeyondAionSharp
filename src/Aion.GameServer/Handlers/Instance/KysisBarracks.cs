using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/KysisBarracks (Estrayl, AION 4.8) : AbstractInnerUpperAbyssInstance. @InstanceID(301280000). 1:1.</summary>
[InstanceID(301280000)]
public class KysisBarracks : AbstractInnerUpperAbyssInstance
{
    public KysisBarracks(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBossId()
    {
        return 233676;
    }

    protected override int GetChestId()
    {
        return 702292;
    }

    protected override int GetDoorId()
    {
        return 700546;
    }

    protected override int GetKeymasterId()
    {
        return 233670;
    }

    protected override int GetTimerNpcId()
    {
        return 206096;
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 233642: OpenDoor(5); break;
            case 233643: OpenDoor(10); break;
            case 233644: OpenDoor(30); break;
            case 233645: OpenDoor(6); break;
            case 233654: OpenDoor(29); break;
            case 233655: OpenDoor(31); break;
            case 233656: OpenDoor(12); break;
            case 233657: OpenDoor(32); break;
            case 233666: OpenDoor(8); break;
            case 233667: OpenDoor(9); break;
            case 233668: OpenDoor(13); break;
            case 233669: OpenDoor(7); break;
            case 233674: OpenDoor(16); break;
            case 233675: case 233676: SpawnChests(); break;
            case 215414: SwitchToEasyMode(); break;
            case 235536: RewardStatueKill(3000); break;
            case 235537: RewardStatueKill(6000); break;
            case 235538: RewardStatueKill(12000); break;
        }
    }
}
