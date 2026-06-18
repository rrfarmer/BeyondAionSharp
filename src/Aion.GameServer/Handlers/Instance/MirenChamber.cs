using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/MirenChamber (Estrayl, AION 4.8) : AbstractInnerUpperAbyssInstance. @InstanceID(300130000). 1:1.</summary>
[InstanceID(300130000)]
public class MirenChamber : AbstractInnerUpperAbyssInstance
{
    public MirenChamber(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBossId()
    {
        return 215222;
    }

    protected override int GetChestId()
    {
        return 700543;
    }

    protected override int GetDoorId()
    {
        return 700547;
    }

    protected override int GetKeymasterId()
    {
        return 215216;
    }

    protected override int GetTimerNpcId()
    {
        return 206097;
    }

    public override void OnDie(Npc npc)
    {
        base.OnDie(npc);
        switch (npc.GetNpcId())
        {
            case 215188: OpenDoor(78); break;
            case 215189: OpenDoor(77); break;
            case 215190: OpenDoor(15); break;
            case 215191: OpenDoor(11); break;
            case 215200: OpenDoor(12); break;
            case 215201: OpenDoor(7); break;
            case 215202: OpenDoor(81); break;
            case 215203: OpenDoor(8); break;
            case 215212: OpenDoor(13); break;
            case 215213: OpenDoor(75); break;
            case 215214: OpenDoor(16); break;
            case 215215: OpenDoor(82); break;
            case 215220: OpenDoor(73); break;
            case 215221: case 215222: SpawnChests(); break;
            case 215415: SwitchToEasyMode(); break;
        }
    }
}
