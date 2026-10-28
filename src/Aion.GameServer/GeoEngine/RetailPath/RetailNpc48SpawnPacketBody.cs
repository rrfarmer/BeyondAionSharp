using System.Buffers.Binary;
using System.Collections.Immutable;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.GeoEngine.RetailPath;

/// <summary>The complete4.8 SM_NPC_INFO body grammar, with immutable wire fields.
/// This infrastructure codec does not establish a retail/4.8 gameplay or content mapping.</summary>
internal static class RetailNpc48SpawnPacketBody
{
    // Java ItemSlot declaration order, excluding combo values. The final six slots retain
    // Java's narrowing compound-assignment mask semantics even though high bits are unusual for NPCs.
    private static readonly ulong[] Slots = [
        1UL, 1UL<<1, 1UL<<2, 1UL<<3, 1UL<<4, 1UL<<5, 1UL<<6, 1UL<<7,
        1UL<<8, 1UL<<9, 1UL<<10, 1UL<<11, 1UL<<12, 1UL<<13, 1UL<<14, 1UL<<15,
        1UL<<16, 1UL<<17, 1UL<<18, 1UL<<19, 1UL<<30, 1UL<<31, 1UL<<32,
        1UL<<33, 1UL<<34, 1UL<<35
    ];
    internal sealed record GearEntry(ulong SlotMask, uint ItemTemplateId);
    internal sealed record Fields(
        RetailMainVectorWords Position, uint ObjectId, uint NpcTemplateId, uint VisualTemplateId,
        byte CreatureType, ushort State, byte Heading, uint NameId, uint TitleId,
        uint CreatorId, string? MasterName, byte HpPercentage, int MaximumHp, byte Level,
        ImmutableArray<GearEntry> Equipment, uint RadiusWord, uint HeightWord, uint MovementSpeedWord,
        ushort MainAttackDelay, ushort OffAttackDelay, byte SpawnFlag, RetailMainVectorWords Goal,
        byte MovementMask, ushort StaticId, byte VisualState, ushort NpcObjectType,
        uint TargetObjectId, uint TownId);
    internal enum Status { Encoded, InvalidEquipment, PacketTooLarge }
    internal sealed record Result(Status Status, ImmutableArray<byte> Bytes, string? Failure)
    {
        internal bool CertifiesGameplay => false;
        internal bool CanApplyToHost => false;
        internal bool ActivationReady => false;
        internal AionServerPacket CreatePacket()
        {
            if (Status != RetailNpc48SpawnPacketBody.Status.Encoded || Bytes.IsDefault)
                throw new InvalidOperationException("An entire encoded4.8 body is required.");
            return RetailNpc48MoveDeletePackets.SnapshotPacket.FromEncodedBody(
                new(RetailNpc48MoveDeletePackets.Status.Encoded, typeof(SM_NPC_INFO), Bytes, null));
        }
    }

    /// <summary>Equipment is the already selected4.8 presentation order, not native slot indexes
    /// or a proof of item acquisition. Unknown order is not an empty equipment list.</summary>
    internal static Result Calculate(Fields fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Equipment.IsDefault || fields.Equipment.Length > Slots.Length)
            return Invalid("Unknown or excessive4.8 selected equipment.");
        int previous = -1; uint mask = 0;
        foreach (var item in fields.Equipment)
        {
            int ordinal = item is null ? -1 : Array.IndexOf(Slots, item.SlotMask);
            if (ordinal <= previous)
                return Invalid("Equipment needs distinct single slots in Java declaration order.");
            previous = ordinal;
            mask |= unchecked((uint)item!.SlotMask);
        }
        long length = 115L + 2L * (fields.MasterName?.Length ?? 0) + 16L * fields.Equipment.Length;
        if (length > AionServerPacket.MAX_USABLE_PACKET_BODY_SIZE)
            return new(Status.PacketTooLarge, [], "Complete4.8 body exceeds the existing client frame limit.");
        byte[] bytes = new byte[(int)length]; int at = 0;
        void C(byte v) { bytes[at++] = v; }
        void H(ushort v) { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at,2),v); at += 2; }
        void D(uint v) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at,4),v); at += 4; }
        void Vector(RetailMainVectorWords v) { D(v.X); D(v.Y); D(v.Z); }
        Vector(fields.Position); D(fields.ObjectId); D(fields.NpcTemplateId); D(fields.VisualTemplateId);
        C(fields.CreatureType); H(fields.State); C(fields.Heading); D(fields.NameId); D(fields.TitleId);
        H(0); C(0); D(0); D(fields.CreatorId);
        // WriteS is code-unit based: do not use an encoding replacement fallback for unpaired surrogates.
        if (fields.MasterName is not null) foreach (char unit in fields.MasterName) H(unit);
        H(0); C(fields.HpPercentage); D(unchecked((uint)fields.MaximumHp)); C(fields.Level); D(mask);
        foreach (var item in fields.Equipment) { D(item.ItemTemplateId); D(0); D(0); H(0); H(0); }
        D(fields.RadiusWord); D(fields.HeightWord); D(fields.MovementSpeedWord);
        H(fields.MainAttackDelay); H(fields.OffAttackDelay); C(fields.SpawnFlag); Vector(fields.Goal);
        C(fields.MovementMask); H(fields.StaticId);
        for (int n = 0; n < 8; n++) C(0);
        C(fields.VisualState); H(fields.NpcObjectType); C(0);
        D(fields.TargetObjectId); D(fields.TownId); D(0);
        if (at != bytes.Length) throw new InvalidOperationException("Internal4.8 grammar length mismatch.");
        return new(Status.Encoded, [..bytes], null);
    }
    private static Result Invalid(string detail) => new(Status.InvalidEquipment, [], detail);
}
