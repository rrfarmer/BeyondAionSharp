using System;
using System.Collections.Generic;

namespace Aion.GameServer.Model.Assemblednpc;

/// <summary>Java parity: model/assemblednpc/AssembledNpc.</summary>
public class AssembledNpc
{
    private List<AssembledNpcPart> assembledPatrs = new List<AssembledNpcPart>();
    private long spawnTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private int routeId;
    private int mapId;

    public AssembledNpc(int routeId, int mapId, int liveTime, List<AssembledNpcPart> assembledPatrs)
    {
        this.assembledPatrs = assembledPatrs;
        this.routeId = routeId;
        this.mapId = mapId;
    }

    public List<AssembledNpcPart> GetAssembledParts()
    {
        return assembledPatrs;
    }

    public int GetRouteId()
    {
        return routeId;
    }

    public int GetMapId()
    {
        return mapId;
    }

    public long GetTimeOnMap()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - spawnTime;
    }
}
