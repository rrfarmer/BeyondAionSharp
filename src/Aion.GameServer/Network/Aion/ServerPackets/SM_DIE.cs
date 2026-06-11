using Aion.GameServer.Instance.Handlers;
using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_DIE (orz, Sarynth, Rhys2002). On death, tells the client which revive options are allowed (skill/item/instance), remaining kisk time, and invasion-world flag. InstanceHandler/WorldMapType red-tolerated.</summary>
public class SM_DIE : AionServerPacket
{
    private readonly bool allowReviveBySkill;
    private readonly bool allowReviveByItem;
    private readonly int remainingKiskTimeSeconds;
    private readonly bool allowInstanceRevive;
    private readonly bool invasion;

    public SM_DIE(Player player)
    {
        InstanceHandler instanceHandler = player.GetWorldMapInstance().GetInstanceHandler();
        allowReviveBySkill = instanceHandler.AllowSelfReviveBySkill() && player.CanUseRebirthRevive();
        allowReviveByItem = instanceHandler.AllowSelfReviveByItem() && player.HaveSelfRezItem();
        remainingKiskTimeSeconds = instanceHandler.AllowKiskRevive() && player.GetKisk() != null ? player.GetKisk().GetRemainingLifetime() : 0;
        allowInstanceRevive = instanceHandler.AllowInstanceRevive();
        invasion = player.GetWorldId() == GetInvasionWorld(player).GetId();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(allowReviveBySkill ? 1 : 0);
        WriteC(allowReviveByItem ? 1 : 0);
        WriteD(remainingKiskTimeSeconds);
        WriteC(allowInstanceRevive ? 1 : 0); // select between obelisk and instance revive (0 = ReviveType.BIND_REVIVE, else ReviveType.INSTANCE_REVIVE)
        WriteC(invasion ? 0x80 : 0x00);
    }

    private WorldMapType GetInvasionWorld(Player player)
    {
        return player.GetRace() == Race.ASMODIANS ? WorldMapType.THEOBOMOS : WorldMapType.BRUSTHONIN;
    }
}
