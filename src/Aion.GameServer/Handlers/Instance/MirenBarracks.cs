using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Handlers.Instance;

/// <summary>Java parity: instance/abyss/MirenBarracks (Estrayl, AION 4.8) : AbstractInnerUpperAbyssInstance. @InstanceID(301290000). 1:1.</summary>
[InstanceID(301290000)]
public class MirenBarracks : AbstractInnerUpperAbyssInstance
{
    public MirenBarracks(WorldMapInstance instance) : base(instance)
    {
    }

    protected override int GetBossId()
    {
        return 233719;
    }

    protected override int GetChestId()
    {
        return 702296;
    }

    protected override int GetDoorId()
    {
        return 700547;
    }

    protected override int GetKeymasterId()
    {
        return 233713;
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
            case 233685: OpenDoor(78); break;
            case 233686: OpenDoor(77); break;
            case 233687: OpenDoor(15); break;
            case 233688: OpenDoor(11); break;
            case 233697: OpenDoor(12); break;
            case 233698: OpenDoor(7); break;
            case 233699: OpenDoor(81); break;
            case 233700: OpenDoor(8); break;
            case 233709: OpenDoor(13); break;
            case 233710: OpenDoor(75); break;
            case 233711: OpenDoor(16); break;
            case 233712: OpenDoor(82); break;
            case 233717: OpenDoor(73); break;
            case 233718: case 233719: SpawnChests(); break;
            case 215415: SwitchToEasyMode(); break;
            case 235536: RewardStatueKill(3000); break;
            case 235537: RewardStatueKill(6000); break;
            case 235538: RewardStatueKill(12000); break;
        }
    }
}
