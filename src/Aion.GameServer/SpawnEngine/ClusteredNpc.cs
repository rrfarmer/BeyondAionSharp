using System;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Model.Templates.Walker;

namespace Aion.GameServer.SpawnEngine;

/// <summary>Java parity: spawnengine/ClusteredNpc (vlog, Rolandas) : WalkerGroupShift. Stores spawn info for walker-group forming. Float.floatToIntBits→BitConverter.SingleToInt32Bits; getWalkerIndex()→int? (Integer). Npc/RouteStep/WalkerTemplate/SpawnEngine red-tolerated.</summary>
public class ClusteredNpc : WalkerGroupShift
{
    private Npc npc;
    private int instance;
    private WalkerTemplate walkTemplate;
    private float x;
    private float y;

    public ClusteredNpc(Npc npc, int instance, WalkerTemplate walkTemplate) : base(0, 0)
    {
        this.npc = npc;
        this.instance = instance;
        this.walkTemplate = walkTemplate;
        this.x = npc.GetSpawn().GetX();
        this.y = npc.GetSpawn().GetY();
    }

    public Npc GetNpc()
    {
        return npc;
    }

    public int GetInstance()
    {
        return instance;
    }

    public void Spawn(float z)
    {
        SpawnEngine.BringIntoWorld(npc, npc.GetSpawn().GetWorldId(), instance, x, y, z, npc.GetSpawn().GetHeading());
    }

    public void Despawn()
    {
        npc.GetMoveController().AbortMove();
        npc.GetController().DeleteIfAliveOrCancelRespawn();
    }

    public void SetNpc(Npc npc, RouteStep step)
    {
        this.npc = npc;
        this.x = step.GetX();
        this.y = step.GetY();
    }

    public bool HasSamePosition(ClusteredNpc other)
    {
        if (this == other)
            return true;
        if (other == null)
            return false;
        return this.x == other.x && this.y == other.y;
    }

    public int GetPositionHash()
    {
        const int prime = 31;
        int result = 1;
        result = prime * result + BitConverter.SingleToInt32Bits(x);
        result = prime * result + BitConverter.SingleToInt32Bits(y);
        return result;
    }

    /// <summary>the x</summary>
    public float GetX()
    {
        return x;
    }

    public float GetXDelta()
    {
        return walkTemplate.GetRouteStep(0).GetX() - x;
    }

    /// <summary>the x to set</summary>
    public void SetX(float x)
    {
        this.x = x;
    }

    /// <summary>the y</summary>
    public float GetY()
    {
        return y;
    }

    public float GetYDelta()
    {
        return walkTemplate.GetRouteStep(0).GetY() - y;
    }

    /// <summary>the y to set</summary>
    public void SetY(float y)
    {
        this.y = y;
    }

    /// <summary>the walkTemplate</summary>
    public WalkerTemplate GetWalkTemplate()
    {
        return walkTemplate;
    }

    public int? GetWalkerIndex()
    {
        return npc.GetSpawn().GetWalkerIndex();
    }
}
