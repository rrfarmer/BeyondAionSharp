using System;
using System.Collections.Generic;
using Aion.GameServer.Model.Items;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_ITEM_COOLDOWN (ATracer). Sends remaining item reuse cooldowns. currentTimeMillis()->DateTimeOffset; Map.entrySet->KeyValuePair iteration. ItemCooldown red-tolerated.</summary>
public class SM_ITEM_COOLDOWN : AionServerPacket
{
    private IDictionary<int, ItemCooldown> cooldowns;

    public SM_ITEM_COOLDOWN(IDictionary<int, ItemCooldown> cooldowns)
    {
        this.cooldowns = cooldowns;
    }

    // Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_ITEM_COOLDOWN.java): 2026-06-17. Reads System.currentTimeMillis; (int)((reuse-now)/1000) cast precedence matches Java.
    protected override void WriteImpl(AionConnection con)
    {
        WriteH(cooldowns.Count);
        long currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (KeyValuePair<int, ItemCooldown> entry in cooldowns)
        {
            WriteH(entry.Key);
            int left = (int)((entry.Value.GetReuseTime() - currentTime) / 1000);
            WriteD(left > 0 ? left : 0);
            WriteD(entry.Value.GetUseDelay());
        }
    }
}
