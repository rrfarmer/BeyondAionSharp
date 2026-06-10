using System.Collections.Generic;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SHOW_BRAND (Sweetkr). Shows target-brand icons (iconId -> targetObjectId). IntStream.range->for loop; putAll->dictionary copy; Map.forEach->foreach KeyValuePair.</summary>
public class SM_SHOW_BRAND : AionServerPacket
{
    private readonly Dictionary<int, int> targetIdsByIconId = new Dictionary<int, int>();

    public SM_SHOW_BRAND(int iconId, int targetObjectId)
    {
        targetIdsByIconId[iconId] = targetObjectId;
    }

    public SM_SHOW_BRAND(IDictionary<int, int> targetIdsByIconId)
    {
        if (targetIdsByIconId.Count == 0)
        {
            for (int brandId = 0; brandId < 16; brandId++)
                this.targetIdsByIconId[brandId] = 0; // reset all brands
        }
        else
        {
            foreach (KeyValuePair<int, int> entry in targetIdsByIconId)
                this.targetIdsByIconId[entry.Key] = entry.Value;
        }
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(targetIdsByIconId.Count);
        foreach (KeyValuePair<int, int> entry in targetIdsByIconId)
        {
            WriteD(1); // 0 = solo?, 1 = group/alliance?, 2 = league? - doesn't seem to make any difference
            WriteD(entry.Key);
            WriteD(entry.Value); // 0 = remove icon
        }
    }
}
