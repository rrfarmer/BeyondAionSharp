using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Network.Aion;

/// <summary>
/// Java parity: network/aion/AionClientPacket (-Nemesiss-). Base for every Aion -> GS client packet.
/// extends BaseClientPacket&lt;AionConnection&gt;; run() guards on isValid() (connection state); validStates
/// is the set of AionConnection.State in which the packet is processable.
/// </summary>
public abstract class AionClientPacket : BaseClientPacket<AionConnection>
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(AionClientPacket));

    private readonly ISet<AionConnection.State> validStates;

    protected AionClientPacket(int opcode, ISet<AionConnection.State> validStates) : base(opcode)
    {
        this.validStates = validStates;
    }

    public override void Run()
    {
        try
        {
            if (IsValid()) // run only if the packet is still valid (connection state didn't change, e.g. due to logout)
                RunImpl();
        }
        catch (Exception e)
        {
            log.LogError(e, "Error handling client packet from " + GetConnection() + ": " + this);
        }
    }

    /// <summary>Send a server packet to this packet's connection. Equivalent to getConnection().sendPacket(msg).</summary>
    protected void SendPacket(AionServerPacket msg)
    {
        GetConnection().SendPacket(msg);
    }

    /// <summary>Reads a fixed-size string, omitting not-present trailing characters from the return value.</summary>
    protected string ReadS(int characterCount)
    {
        string str = ReadS(); // read byte length = characters * 2 + 2
        if (str.Length < characterCount)
            ReadB((characterCount - str.Length) * 2);
        return str;
    }

    /// <summary>True if the packet is still valid for its connection's current state.</summary>
    public bool IsValid()
    {
        return validStates.Contains(GetConnection().GetState());
    }

    protected override int GetOpCodeZeroPadding() => 3;
}
