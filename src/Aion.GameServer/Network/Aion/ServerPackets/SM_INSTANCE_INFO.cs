using System;
using System.Collections.Generic;
using System.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Templates;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>Java parity: network/aion/serverpackets/SM_INSTANCE_INFO (nrg, Neon). Sends per-player instance cooldown info (reuse time, max/used entries, race hide flag) for the given (or all) instance ids. Converges PlayerEnterWorldService. Integer...->params int[]; Arrays.asList->new List; keySet().toArray->Keys.ToArray; currentTimeMillis->UtcNow. PortalCooldown/InstanceCooltime red-tolerated.</summary>
public class SM_INSTANCE_INFO : AionServerPacket
{
    private byte updateType; // 0 = reset+write, 1 = update team info, 2 = add/overwrite without resetting
    private ICollection<Player> players; // players whose cooldown info is sent
    private int[] instanceIds; // instances to update

    public SM_INSTANCE_INFO(byte updateType, Player player, params int[] instanceId)
        : this(updateType, new List<Player> { player }, instanceId)
    {
    }

    public SM_INSTANCE_INFO(byte updateType, ICollection<Player> players, params int[] instanceId)
    {
        this.updateType = updateType;
        this.players = players;
        this.instanceIds = instanceId.Length > 0 ? instanceId : DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimes().Keys.ToArray();
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player activePlayer = con.GetActivePlayer();
        WriteC(updateType);
        WriteD(instanceIds.Length == 1 ? DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(instanceIds[0]).GetId() : 0); // cooldown ID if only one instance is updated
        WriteC(0x00); // unk1
        WriteH(players.Count);
        foreach (Player player in players)
        {
            WriteD(player.GetObjectId());
            WriteH(instanceIds.Length);
            foreach (int worldId in instanceIds)
            {
                PortalCooldown cooldown = player.GetPortalCooldownList().GetPortalCooldown(worldId);
                InstanceCooltime cooltime = DataManager.INSTANCE_COOLTIME_DATA.GetInstanceCooltimeByWorldId(worldId);
                WriteD(cooltime.GetId());
                WriteD(0x00);
                WriteD(cooldown == null ? 0 : (int)(cooldown.GetReuseTime() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000); // will only be shown from client if entriesUsed == maxEntries
                WriteD(cooltime.GetMaxCount()); // max entries
                WriteD(cooldown == null ? 0 : -cooldown.GetEnterCount()); // entry offset (from max)
                WriteC(cooltime.GetRace() == activePlayer.GetOppositeRace() ? 0 : 1); // hide flag (1 = show, 0 = hide instance from list)
            }
            WriteS(player.GetName());
        }
    }
}
