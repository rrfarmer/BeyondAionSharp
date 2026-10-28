using System.Buffers.Binary;
using U = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;
using static Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcUserPhysicalInputs;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive complete actual User normal getter programs. Current images remain
/// declarations; no effective getter result, eligibility answer or native memory is acquired.</summary>
internal static class RetailNpcUserNormalInputs
{
    internal const string NormalProof = "a3054dffaf932520f4853a1785e4a765d65753af6c59e5c5e8d755aae2fc17bf";
    internal const string ParentProof = "3888a15db3fcc5baec8a81e3f5e50811f1c86cb87c130ebe09f39675fad1f348";
    internal readonly record struct Selected(uint Bits, int Selector);
    internal static void SnapshotPrefix(Cursor c, ulong address) => c.External(address, 0x1cc, "snapshot");
    internal static void SnapshotTail(Cursor c, ulong address)
    {
        SnapshotPrefix(c, address);
        Need(address <= ulong.MaxValue - 0x218, "range", "snapshot.tail", 0, "Wrapped snapshot tail.");
        c.External(address + 0x1cc, 0x4c, "snapshot-tail");
    }
    internal static ushort Level(Cursor c, ulong actor) => c.U16(actor + 0x1492, 0x1400b2bc0, "actor.level1492");
    internal static int DenominatorAdjustment(Cursor c, ulong source, ulong user)
    {
        if (Level(c, source) <= 50) return 0;
        if (Level(c, user) <= 50) return 0;
        uint target = Level(c, user), attacker = Level(c, source);
        return (Level(c, attacker < target ? source : user) - 50) * 10;
    }
    internal static int ResistanceLevelBonus(Cursor c, ulong source, ulong user)
    {
        int difference = Level(c, user) - Level(c, source);
        return difference > 4 ? (difference - 4) * 100 : difference < -4 ? (difference + 4) * 100 : 0;
    }
    internal static int Floor(Cursor c, ulong user)
    {
        uint level = Level(c, user);
        Need(level <= 100, "range", "User.floor_level", 0x14036a74d, "Proved initializer covers101 cells0..100; no adjacent or clamped substitute.");
        ulong address = 0x145a28930ul + level * 4;
        c.External(address, 4, "User-floor-cell");
        return c.I32(address, 0x14036a74d, "current.User_elemental_floor");
    }
    internal static Selected Defense(Cursor c, ulong user, bool physical)
    {
        ulong site = physical ? 0x14036a927ul : 0x14036a7eeul;
        int flags = physical ? 0x1c8 : 0x214, field = physical ? 0x178 : 0x1d0;
        ulong p = c.U64(user + 0x6e50, site, "normal_defense.primary");
        SnapshotPrefix(c, p); if (!physical) SnapshotTail(c, p);
        if (c.U16(p, site, "normal_defense.primary_occupied") != 0)
        {
            if ((c.U8(p + (ulong)flags, site, "normal_defense.primary_flags") & 2) != 0)
                return new(c.U32(p + (ulong)field, site, "normal_defense.primary_unsigned32"), 0);
        }
        else
        {
            ulong b = c.U64(user + 0x6e38, site, "normal_defense.backup");
            SnapshotPrefix(c, b); if (!physical) SnapshotTail(c, b);
            if (c.U16(b, site, "normal_defense.backup_occupied") != 0 &&
                (c.U8(b + (ulong)flags, site, "normal_defense.backup_flags") & 2) != 0)
                return new(c.U32(b + (ulong)field, site, "normal_defense.backup_unsigned32"), 1);
        }
        return new(c.U32(user + (physical ? 0x1494ul : 0x1498ul), site, "normal_defense.live_unsigned32"), 2);
    }
    internal readonly record struct ElementalValue(short Value, int Selector);
    internal static ElementalValue Elemental(Cursor c, ulong user, int index)
    {
        const ulong site = 0x1400de340;
        Need(index is >= 0 and < 6, "range", "elemental.index", site, "Normal elements1..6 require original index0..5.");
        ulong p = c.U64(user + 0x6e50, site, "elemental.primary"); SnapshotPrefix(c, p);
        uint bit = 1u << (index + 6);
        if (c.U16(p, site, "elemental.primary_occupied") != 0)
        {
            if ((c.U16(p + 0xbc, site, "elemental.primary_flags") & bit) != 0)
                return new(unchecked((short)c.U16(p + 0xb0 + (ulong)index * 2, site, "elemental.primary_signed16")), 0);
        }
        else
        {
            ulong b = c.U64(user + 0x6e38, site, "elemental.backup"); SnapshotPrefix(c, b);
            if (c.U16(b, site, "elemental.backup_occupied") != 0 && (c.U16(b + 0xbc, site, "elemental.backup_flags") & bit) != 0)
                return new(unchecked((short)c.U16(b + 0xb0 + (ulong)index * 2, site, "elemental.backup_signed16")), 1);
        }
        return new(unchecked((short)c.U16(user + 0x764 + (ulong)index * 2, site, "elemental.live_signed16")), 2);
    }
    internal static Selected MagicalResistance(Cursor c, ulong user)
    {
        const ulong site = 0x1400e0240;
        ulong p = c.U64(user + 0x6e50, site, "MRes.primary"); SnapshotTail(c, p);
        if (c.U16(p, site, "MRes.primary_occupied") != 0)
        {
            if ((c.U8(p + 0x214, site, "MRes.primary_flags") & 4) != 0)
                return new(c.U32(p + 0x1d4, site, "MRes.primary_unsigned32"), 0);
        }
        else
        {
            ulong b = c.U64(user + 0x6e38, site, "MRes.backup"); SnapshotTail(c, b);
            if (c.U16(b, site, "MRes.backup_occupied") != 0 && (c.U8(b + 0x214, site, "MRes.backup_flags") & 4) != 0)
                return new(c.U32(b + 0x1d4, site, "MRes.backup_unsigned32"), 1);
        }
        // Actual source type2 gives R8B=0: no PvP additions are consumed.
        return new(c.U16(user + 0x149c, site, "MRes.live_ushort"), 2);
    }
    internal static int Critical(Cursor c, ulong user, bool physical, bool reduction)
    {
        ulong site = physical ? 0x14036ad7cul : 0x14036adecul;
        int flags = physical ? 0x1c8 : 0x214;
        uint bit = physical ? (reduction ? 0x1000u : 0x800u) : (reduction ? 0x800u : 0x400u);
        int field = physical ? (reduction ? 0x1a4 : 0x1a0) : (reduction ? 0x1f8 : 0x1f4);
        ulong p = c.U64(user + 0x6e50, site, "normal_critical.primary");
        SnapshotPrefix(c, p); if (!physical) SnapshotTail(c, p);
        ushort occupied = c.U16(p, site, "normal_critical.primary_occupied");
        if (occupied == 0)
        {
            p = c.U64(user + 0x6e38, site, "normal_critical.backup");
            SnapshotPrefix(c, p); if (!physical) SnapshotTail(c, p);
            occupied = c.U16(p, site, "normal_critical.backup_occupied");
        }
        if (occupied != 0 && (c.U32(p + (ulong)flags, site, "normal_critical.flags") & bit) != 0)
            return c.I32(p + (ulong)field, site, "normal_critical.snapshot_signed32");
        ulong live = physical ? (reduction ? 0x14caul : 0x14c6ul) : (reduction ? 0x14ccul : 0x14c8ul);
        return c.U16(user + live, site, "normal_critical.live_ushort");
    }
}
