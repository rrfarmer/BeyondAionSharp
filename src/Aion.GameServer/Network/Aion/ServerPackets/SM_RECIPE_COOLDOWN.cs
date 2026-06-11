using System.Collections.Generic;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_RECIPE_COOLDOWN (zzsort, Sykra). Sends remaining craft-recipe cooldown seconds (mode 1 = update). Converges PlayerEnterWorldService SM_RECIPE_COOLDOWN. Map->Dictionary; Cooldowns.forEach->ForEach lambda; writeImpl forEach->foreach KeyValuePair. Cooldowns/AionServerPacket red-tolerated.</summary>
public class SM_RECIPE_COOLDOWN : AionServerPacket
{
    /// <summary>0 - unknown; 1 - update recipe cooldown times.</summary>
    private int mode = 0;
    private readonly Dictionary<int, int> cooldowns = new Dictionary<int, int>();

    public SM_RECIPE_COOLDOWN(Player player, int mode)
    {
        this.mode = mode;
        Cooldowns craftCooldowns = player.GetCraftCooldowns();
        if (!craftCooldowns.IsEmpty())
            craftCooldowns.ForEach((cooldownId, value) => cooldowns[cooldownId] = craftCooldowns.RemainingSeconds(cooldownId));
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteC(mode);
        WriteH(cooldowns.Count);
        foreach (KeyValuePair<int, int> entry in cooldowns)
        {
            WriteD(entry.Key);
            WriteD(entry.Value);
        }
    }
}
