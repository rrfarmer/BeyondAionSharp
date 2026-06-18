using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/KrotanChamber (Estrayl, AION 4.8) : AbstractInnerUpperAbyssInstance. @InstanceID(300140000). 1:1.</summary>
[InstanceID(300140000)]
public class KrotanChamber : AbstractInnerUpperAbyssInstance
{
    public KrotanChamber(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBossId()
    {
        return 215136;
    }

    protected override int GetChestId()
    {
        return 700539;
    }

    protected override int GetDoorId()
    {
        return 700545;
    }

    protected override int GetKeymasterId()
    {
        return 215130;
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
            case 215102: OpenDoor(10); break;
            case 215103: OpenDoor(5); break;
            case 215104: OpenDoor(30); break;
            case 215105: OpenDoor(6); break;
            case 215114: OpenDoor(32); break;
            case 215115: OpenDoor(31); break;
            case 215116: OpenDoor(12); break;
            case 215117: OpenDoor(29); break;
            case 215126: OpenDoor(13); break;
            case 215127: OpenDoor(8); break;
            case 215128: OpenDoor(7); break;
            case 215129: OpenDoor(9); break;
            case 215134: OpenDoor(16); break;
            case 215135: case 215136: SpawnChests(); break;
            case 215413: SwitchToEasyMode(); break;
        }
    }
}
