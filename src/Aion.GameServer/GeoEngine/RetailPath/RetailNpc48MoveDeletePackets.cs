using System.Buffers.Binary;
using System.Collections.Immutable;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;
using M = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedMembershipMove;
using V = Aion.GameServer.GeoEngine.RetailPath.RetailNpcManagedVisibility;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>Immutable ordinary NPC move/delete snapshots in the existing4.8 wire grammar.
/// No framing/encryption, socket send, known-list callback or actor mutation executes here.
/// A connected spawn requires its separate complete4.8 content/display adapter.</summary>
internal static class RetailNpc48MoveDeletePackets
{
    internal enum Status { Encoded, NoConnection, UnsupportedMeaning, InvalidMeaning }
    internal sealed record Body(Status Status, Type? OpcodeOwner, ImmutableArray<byte> Bytes, string? Failure)
    { internal bool HostEffectsExecuted => false; }
    internal sealed record Delivery(uint RecipientId, string RecipientLifetime, string ConnectionGeneration,
        uint ObjectId, SnapshotPacket Packet);
    internal sealed record Batch(Status Status, ImmutableArray<Delivery> Deliveries, int? FailureIndex,
        string? Failure, string SourceSnapshotSha256, string Operation, string Epoch)
    {
        internal bool HostEffectsExecuted => false;
        internal bool CanApplyToHost => false;
        internal bool ActivationReady => false;
    }
    internal sealed class SnapshotPacket : AionServerPacket
    {
        private readonly ImmutableArray<byte> body;
        private SnapshotPacket(Type owner, ImmutableArray<byte> bytes)
            : base(ServerPacketsOpcodes.GetOpcode(owner)) { body = bytes; }
        internal static SnapshotPacket FromEncodedBody(Body value)
        {
            if (value.Status != Status.Encoded || value.OpcodeOwner is null || value.Bytes.IsDefault)
                throw new ArgumentException("A complete supported body is required.", nameof(value));
            // This is only an internal packet snapshot constructor, not a whole-owner admission.
            return new(value.OpcodeOwner, value.Bytes);
        }
        protected override void WriteImpl(AionConnection con) => WriteB(body.ToArray());
    }

    internal static Body CalculateBody(V.Intent intent)
    {
        if (intent is null || intent.Meaning is null || intent.Meaning.RecipientId == 0 || intent.Meaning.ObjectId == 0
            || string.IsNullOrWhiteSpace(intent.RecipientLifetime) || string.IsNullOrWhiteSpace(intent.ConnectionGeneration))
            return Failed(Status.InvalidMeaning, "Missing owned recipient/object or connection lifetime.");
        // Native SendBuf has no socket effect without a connection. No unused codec is reached.
        if (!intent.HasConnection) return new(Status.NoConnection, null, [], null);
        if (intent.Meaning is V.Delete remove)
        {
            if (remove.Mode != 0) return Failed(Status.UnsupportedMeaning, "Only the source ordinary mode0 maps to4.8 NONE.");
            byte[] bytes = new byte[5]; BinaryPrimitives.WriteUInt32LittleEndian(bytes, remove.ObjectId);
            bytes[4] = 0;
            return new(Status.Encoded, typeof(SM_DELETE), [..bytes], null);
        }
        if (intent.Meaning is not V.Move move)
            return Failed(Status.UnsupportedMeaning, "Connected spawn/other meaning needs its complete4.8 adapter.");
        if (move.Heading >= 120 || (move.Flags & 0x10) != 0)
            return Failed(Status.InvalidMeaning, "Ordinary Main NPC move requires heading<120 and the removed vehicle bit.");
        bool manualPosition = (move.Flags & 0xc0) == 0xc0;
        bool glide = (move.Flags & 4) != 0;
        if (glide && move.NativeGlideTail != 0)
            return Failed(Status.UnsupportedMeaning, "Ordinary NPC glide tail must be the calculated source zero.");
        byte[] body = new byte[18 + (manualPosition ? 12 : 0) + (glide ? 1 : 0)];
        BinaryPrimitives.WriteUInt32LittleEndian(body, move.ObjectId);
        void Vector(int offset, RetailMainVectorWords value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(offset, 4), value.X);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(offset + 4, 4), value.Y);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(offset + 8, 4), value.Z);
        }
        // Copy all words verbatim, including signed zero/exceptional copied words.
        Vector(4, move.EffectiveOld); body[16] = move.Heading; body[17] = move.Flags;
        if (manualPosition) Vector(18, move.Goal);
        if (glide) body[^1] = 0;
        return new(Status.Encoded, typeof(SM_MOVE), [..body], null);
    }

    /// <summary>Calculates the entire supported delivery batch before exposing any packet.
    /// The host still must validate/retain the original once ledger and current exclusive lease.
    /// No connection, generation or callback is acquired or executed by this method.</summary>
    internal static Batch CalculateCompleted(M.Candidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var value = candidate.Value;
        var deliveries = ImmutableArray.CreateBuilder<Delivery>();
        for (int i = 0; i < value.State.ProvisionalIntents.Length; i++)
        {
            V.Intent intent = value.State.ProvisionalIntents[i]; Body body = CalculateBody(intent);
            if (body.Status == Status.NoConnection) continue;
            if (body.Status != Status.Encoded)
                return new(body.Status, [], i, body.Failure, value.SourceSnapshotSha256, value.Operation, value.Epoch);
            deliveries.Add(new(intent.Meaning.RecipientId, intent.RecipientLifetime, intent.ConnectionGeneration,
                intent.Meaning.ObjectId, SnapshotPacket.FromEncodedBody(body)));
        }
        return new(Status.Encoded, deliveries.ToImmutable(), null, null, value.SourceSnapshotSha256, value.Operation, value.Epoch);
    }
    private static Body Failed(Status status, string failure) => new(status, null, [], failure);
}
