using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Model.Drop;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services.Drop;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_LOOT_STATUS (alexa026). Loot enable/disable + drop-list open/close, plus loot-effect lookup. Status enum sequential 0..3; stream mapToInt/filter/findAny.orElse(0) -> Select/Where/DefaultIfEmpty(0).First; getOrDefault->GetValueOrDefault. DropItem/DropRegistrationService red-tolerated.</summary>
public class SM_LOOT_STATUS : AionServerPacket
{
    private readonly int targetObjectId;
    private readonly Status status;
    private readonly int lootEffectId;

    public SM_LOOT_STATUS(int targetObjectId, Status status)
    {
        this.targetObjectId = targetObjectId;
        this.status = status;
        this.lootEffectId = status == Status.LOOT_ENABLE ? GetLootEffect(targetObjectId) : 0;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetObjectId);
        WriteC(status.GetId());
        WriteD(lootEffectId);
    }

    private static int GetLootEffect(int targetObjectId)
    {
        ISet<DropItem> items = DropRegistrationService.GetInstance().GetCurrentDropMap().GetValueOrDefault(targetObjectId, new HashSet<DropItem>());
        return items.Select(d => d.GetLootEffectId()).Where(i => i != 0).DefaultIfEmpty(0).First();
    }

    public enum Status
    {
        LOOT_ENABLE = 0,
        LOOT_DISABLE = 1,
        OPEN_DROP_LIST = 2,
        CLOSE_DROP_LIST = 3,
    }
}

/// <summary>Java parity: SM_LOOT_STATUS.Status.getId() — id == ordinal (sequential), exposed as an extension to keep the call site faithful.</summary>
public static class SM_LOOT_STATUS_StatusExtensions
{
    public static int GetId(this SM_LOOT_STATUS.Status status) => (int)status;
}
