using System.Collections.Generic;
using Aion.GameServer.Model.Town;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_TOWNS_LIST (ViAl). Sends town id/level/levelUpDate. getLevelUpDate().getTime()/1000 -> ToUnixTimeMilliseconds()/1000; Map.values iteration. Town red-tolerated.</summary>
// Java parity (writeImpl audited 1:1 vs game-server/src/com/aionemu/gameserver/network/aion/serverpackets/SM_TOWNS_LIST.java): 2026-06-17
// TIER-2 audit-only: the only DTO input is Town, whose constructor (faithful 1:1 with Java) calls spawnNewObjects()
// (DataManager.TOWN_SPAWNS_DATA + SpawnEngine) and GeoService.getInstance().updateTown(...) — a live world/data-graph the
// unit harness cannot bounded-build, so validated by line-by-line writeImpl comparison rather than a runtime golden.
// Confirmed IDENTICAL field set/order/encoding: writeH(towns.size) + per-town writeD(id) writeD(level)
// writeD((int)(levelUpDate.getTime()/1000)) over Map.values(). getTime()/1000 -> ToUnixTimeMilliseconds()/1000. 0 fidelity discrepancies.
public class SM_TOWNS_LIST : AionServerPacket
{
    private IDictionary<int, Town> towns;

    public SM_TOWNS_LIST(IDictionary<int, Town> towns)
    {
        this.towns = towns;
    }

    protected override void WriteImpl(AionConnection con)
    {
        WriteH(towns.Count);
        foreach (Town town in towns.Values)
        {
            WriteD(town.GetId());
            WriteD(town.GetLevel());
            WriteD((int)(town.GetLevelUpDate().ToUnixTimeMilliseconds() / 1000));
        }
    }
}
