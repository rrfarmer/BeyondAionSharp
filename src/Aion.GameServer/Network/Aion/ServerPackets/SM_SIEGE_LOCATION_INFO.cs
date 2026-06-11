using System.Collections.Generic;
using Aion.GameServer.Configs.Main;
using Aion.GameServer.Configs.Network;
using Aion.GameServer.Model.GameObjects.Players;
using Aion.GameServer.Model.Siege;
using Aion.GameServer.Model.Team.Legion;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>Java parity: network/aion/serverpackets/SM_SIEGE_LOCATION_INFO (Sarynth, Neon). All-siege-locations (reset) or single (update) info: owner legion/emblem/race/vulnerability/teleport/next-state/timer. Map.values iteration; SiegeConfig/SiegeService/LegionService red-tolerated.</summary>
public class SM_SIEGE_LOCATION_INFO : AionServerPacket
{
    /// <summary>infoType 0 - reset 1 - update</summary>
    private int infoType;
    private IDictionary<int, SiegeLocation> locations;

    public SM_SIEGE_LOCATION_INFO()
    {
        this.infoType = 0;
        locations = SiegeService.GetInstance().GetSiegeLocations();
    }

    public SM_SIEGE_LOCATION_INFO(SiegeLocation loc)
    {
        this.infoType = 1;
        locations = new Dictionary<int, SiegeLocation>();
        locations[loc.GetLocationId()] = loc;
    }

    protected override void WriteImpl(AionConnection con)
    {
        Player player = con.GetActivePlayer();
        if (!SiegeConfig.SIEGE_ENABLED)
        {
            WriteC(0);
            WriteH(0);
            return;
        }

        WriteC(infoType);
        WriteH(locations.Count);

        foreach (SiegeLocation loc in locations.Values)
        {
            LegionEmblem emblem = new LegionEmblem();
            int legionId = loc.GetLegionId();
            int locId = loc.GetLocationId();
            WriteD(locId);
            WriteD(legionId);
            if (legionId != 0 && LegionService.GetInstance().GetLegion(legionId) != null) // can be null if legion got deleted
                emblem = LegionService.GetInstance().GetLegion(legionId).GetLegionEmblem();
            WriteC(emblem.GetEmblemId());
            WriteC(emblem.GetEmblemType().GetValue());
            WriteH(0);
            WriteC(emblem.GetColor_a());
            WriteC(emblem.GetColor_r());
            WriteC(emblem.GetColor_g());
            WriteC(emblem.GetColor_b());
            WriteC(loc.GetRace().GetRaceId());
            WriteC(loc.IsVulnerable() ? 2 : 0); // is vulnerable (0 - no, 2 - yes)
            WriteC(loc.IsCanTeleport(player) ? 1 : 0);
            WriteC(loc.GetNextState()); // Next State (0 - invulnerable, 1 - vulnerable)
            WriteH(0); // unk
            WriteH(0);
            WriteD(locId == 2111 || locId == 3111 ? SiegeService.GetInstance().GetRemainingSiegeTimeInSeconds(locId) : 0); // veille/masta timer
            WriteD(NetworkConfig.GAMESERVER_ID); // server ID of the fortress owner (TODO relevant for panesterra, so change this later)
            WriteD(0); // unk 4.7 (some timestamp, maybe Capture Date?)
            WriteD(loc.GetOccupiedCount());
        }
    }
}
