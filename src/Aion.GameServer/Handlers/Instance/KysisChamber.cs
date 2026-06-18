using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/KysisChamber (Estrayl, AION 4.8) : AbstractInnerUpperAbyssInstance. @InstanceID(300120000). 1:1.</summary>
[InstanceID(300120000)]
public class KysisChamber : AbstractInnerUpperAbyssInstance
{
    public KysisChamber(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBossId()
    {
        return 215179;
    }

    protected override int GetChestId()
    {
        return 700541;
    }

    protected override int GetDoorId()
    {
        return 700546;
    }

    protected override int GetKeymasterId()
    {
        return 215173;
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
            case 215145: OpenDoor(5); break;
            case 215146: OpenDoor(10); break;
            case 215147: OpenDoor(30); break;
            case 215148: OpenDoor(6); break;
            case 215157: OpenDoor(29); break;
            case 215158: OpenDoor(31); break;
            case 215159: OpenDoor(12); break;
            case 215160: OpenDoor(32); break;
            case 215169: OpenDoor(8); break;
            case 215170: OpenDoor(9); break;
            case 215171: OpenDoor(13); break;
            case 215172: OpenDoor(7); break;
            case 215177: OpenDoor(16); break;
            case 215178: case 215179: SpawnChests(); break;
            case 215414: SwitchToEasyMode(); break;
        }
    }
}
