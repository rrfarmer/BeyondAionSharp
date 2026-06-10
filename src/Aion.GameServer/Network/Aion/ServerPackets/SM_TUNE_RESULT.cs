using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.Iteminfo;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TUNE_RESULT (Estrayl, Sykra). Item tuning result (stat bonus + enchant blob + manastone/cancel flags). getBuf()->GetBuf(); chained assignment preserved. Item/PendingTuneResult/EnchantInfoBlobEntry red-tolerated.</summary>
public class SM_TUNE_RESULT : AionServerPacket
{
    private readonly Item targetItem;
    private readonly int tuningScrollItemId;
    private readonly PendingTuneResult result;
    private readonly bool tuneCancelPossible;
    private readonly bool showManastoneSlots;

    public SM_TUNE_RESULT(Item targetItem, int tuningScrollItemId, PendingTuneResult result)
    {
        this.targetItem = targetItem;
        this.tuningScrollItemId = tuningScrollItemId;
        this.result = result;
        this.tuneCancelPossible = this.showManastoneSlots = !result.IsAttributeOnly();
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteD(targetItem.GetObjectId());
        WriteD(tuningScrollItemId);
        WriteC(result.GetStatBonusId());
        EnchantInfoBlobEntry.WriteInfo(GetBuf(), targetItem, result.GetOptionalSockets(), result.GetEnchantBonus());
        WriteC(showManastoneSlots ? 0 : 1);
        WriteC(tuneCancelPossible ? 0 : 1);
    }
}
