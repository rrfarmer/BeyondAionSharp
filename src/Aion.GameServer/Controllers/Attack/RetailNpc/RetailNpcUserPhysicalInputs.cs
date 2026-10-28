using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using M = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcToUserPhysicalSkillPveModifier;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcPhysicalInstantParameters;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive source-bound byte views and complete User physical getter programs.
/// Addresses label the native proof layout; supplied current images remain declarations.
/// No live actor, gear, native memory, callback or desired getter result is acquired here.</summary>
internal static class RetailNpcUserPhysicalInputs
{
    internal const ulong NpcVtable = 0x1412b8470, UserVtable = 0x141161250;
    internal const string PhysicalProof = "0c61182fe68eecbd2d61799279ff223f3fac2d18cb30dc511fd39ec8cd692c05";
    internal const string UserProof = "4528bf35ed3a4c1b1f226938bdfa90e0535bbaf1a4007d49ba9e1f6c7bcbac05";
    internal const string UserCallerProof = "a3054dffaf932520f4853a1785e4a765d65753af6c59e5c5e8d755aae2fc17bf";
    internal const string LookupProof = "7c726e29c5bf21c6f44b9bd631e955f1e182dce21478a6b32adada5ba3146324";
    internal enum Origin { ConstructedFixture, DeclaredCurrentSnapshot }
    internal sealed record Sources(string Main, string Package, string Parameters, string Caller,
        string Physical, string User, string UserCaller, string Lookup, string UserClassLeaf, string NpcPreparation, string Modifier, string ModifierOracle, string ModifierRelease);
    internal static Sources RequiredSources => new(P.MainSha256, P.PackageContext, P.SourceEvidenceSha256,
        P.CallerReviewSha256, PhysicalProof, UserProof, UserCallerProof, LookupProof,
        "b70c004eff8893079b7a06f67bd815973c02cbbd0b4915b46e2546fb4ca881fe",
        "7408dbb50fe4c3467a0a7b3c5e4bd94dc4184f2792a9ef5b9d27c7934d95710e",
        M.ProofSha256, M.OracleAmendmentSha256, M.ReleaseEvidenceSha256);
    internal sealed record Context(Sources Sources, string Operation, string Epoch, Origin Origin, string Receipt);
    internal sealed record Binding(ulong Address, string Identity, string Generation, Context Context);
    internal sealed record Step(int Sequence, ulong Site, string Kind, string Operand, ulong Address,
        string Before, string After, long? Value = null);
    internal sealed record Failure(string Code, string Operand, ulong Site, string Detail, bool Unsupported);
    internal sealed class Domain(Failure failure) : Exception(failure.Detail)
    { internal Failure Failure { get; } = failure; }

    /// <summary>Copies the complete supplied raw/known images. Unknown bytes are preserved but
    /// cannot become read operands. Content hashes validate this image, never external provenance.</summary>
    internal sealed class Image
    {
        internal Binding Binding { get; }
        internal ImmutableArray<byte> Bytes { get; }
        internal ImmutableArray<byte> Known { get; }
        internal string BytesSha256 { get; }
        internal string KnownSha256 { get; }
        private Image(Binding binding, ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> known)
        {
            Binding = binding; Bytes = ImmutableArray.CreateRange(bytes.ToArray());
            Known = ImmutableArray.CreateRange(known.ToArray());
            BytesSha256 = Hash(Bytes.AsSpan()); KnownSha256 = Hash(Known.AsSpan());
        }
        internal static Image Create(Binding binding, ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> known)
        {
            ArgumentNullException.ThrowIfNull(binding);
            if (bytes.Length == 0 || bytes.Length > 2_000_000 || known.Length != bytes.Length ||
                binding.Address == 0 || binding.Address > ulong.MaxValue - (ulong)bytes.Length ||
                known.IndexOfAnyExcept((byte)0, (byte)1) >= 0)
                throw new ArgumentException("Explicit bounded raw image and 0/1 knowledge mask required.");
            return new(binding, bytes, known);
        }
    }
    internal sealed class Memory
    {
        internal ImmutableArray<Image> Images { get; }
        internal int ByteCount { get; }
        private Memory(ImmutableArray<Image> images) { Images = images; ByteCount = images.Sum(x => x.Bytes.Length); }
        internal static Memory Create(IEnumerable<Image> images)
        {
            ArgumentNullException.ThrowIfNull(images);
            var owned = images.Take(129).ToImmutableArray();
            if (owned.Length is 0 or > 128 || owned.Any(x => x is null) || owned.Sum(x => (long)x.Bytes.Length) > 8_000_000)
                throw new ArgumentException("Bounded immutable image bank required.");
            for (int i = 0; i < owned.Length; i++) for (int j = 0; j < i; j++)
            {
                var x = owned[i]; var y = owned[j];
                if (x.Binding.Identity == y.Binding.Identity ||
                    !(x.Binding.Address + (ulong)x.Bytes.Length <= y.Binding.Address ||
                      y.Binding.Address + (ulong)y.Bytes.Length <= x.Binding.Address))
                    throw new ArgumentException("Image identities/ranges must be unique; represent aliases with one actual image.");
            }
            return new(owned);
        }
    }

    internal sealed class Cursor
    {
        private readonly Memory original;
        private readonly byte[][] bytes, known;
        private readonly List<Step> steps = [];
        private readonly int limit;
        private readonly List<(ulong Address, int Bytes)> protectedRanges = [];
        private readonly List<(ulong Address, int Bytes, string Role)> externalRanges = [];
        internal ImmutableArray<Step> Steps => [.. steps];
        internal Cursor(Context context, Memory memory, int maxSteps)
        {
            CheckContext(context); Need(BitConverter.IsLittleEndian, "environment", "byte_order", 0, "Little-endian host required for optional typed raw projections.");
            Need(memory is not null, "memory", "bank", 0, "Missing explicit current image bank.");
            Need(maxSteps > 0, "budget", "steps", 0, "Positive adapter trace capacity required.");
            foreach (Image image in memory.Images)
            {
                Need(image.Binding.Context == context, "context", image.Binding.Identity, 0,
                    "Image and operation/epoch/source/origin declarations differ.");
                Text(image.Binding.Identity); Text(image.Binding.Generation);
            }
            original = memory; limit = maxSteps;
            bytes = memory.Images.Select(x => x.Bytes.ToArray()).ToArray();
            known = memory.Images.Select(x => x.Known.ToArray()).ToArray();
        }
        internal void Protect(ulong address, int count)
        { _ = Locate(address, count); protectedRanges.Add((address, count)); }
        internal void External(ulong address, int count, string role)
        {
            _ = Locate(address, count);
            foreach (var old in protectedRanges)
                Need(address + (ulong)count <= old.Address || old.Address + (ulong)old.Bytes <= address,
                    "alias", role, 0, "Reached external object/table overlaps a calculator actor/Skill/template/output owner.");
            foreach (var old in externalRanges)
            {
                if (old.Address == address && old.Bytes == count && old.Role == role) return;
                Need(address + (ulong)count <= old.Address || old.Address + (ulong)old.Bytes <= address,
                    "alias", role, 0, "Distinct reached external role ranges overlap; exact snapshot aliases use one raw view.");
            }
            externalRanges.Add((address, count, role));
        }
        internal Memory Snapshot() => Memory.Create(original.Images.Select((x, i) => Image.Create(x.Binding, bytes[i], known[i])));
        internal Binding BindingAt(ulong address, int count)
        { var (i, _) = Locate(address, count); return original.Images[i].Binding; }
        internal int Available(ulong address)
        { var (i, off) = Locate(address, 1); return bytes[i].Length - off; }
        private (int Index, int Offset) Locate(ulong address, int count)
        {
            Need(count > 0 && address != 0 && address <= ulong.MaxValue - (ulong)count,
                "range", "memory", 0, "Invalid consumed address/range.");
            for (int i = 0; i < original.Images.Length; i++)
            {
                ulong start = original.Images[i].Binding.Address;
                if (start <= address && address + (ulong)count <= start + (ulong)bytes[i].Length)
                    return (i, checked((int)(address - start)));
            }
            throw Error("unknown-storage", "0x" + address.ToString("x"), 0, "Reached source address has no owned storage image.");
        }
        internal byte[] Peek(ulong address, int count)
        {
            var (i, off) = Locate(address, count);
            Need(known[i].AsSpan(off, count).IndexOf((byte)0) < 0,
                "unknown", "0x" + address.ToString("x"), 0, "Unknown reached byte operand.");
            return bytes[i].AsSpan(off, count).ToArray();
        }
        internal T? Optional<T>(ulong address) where T : unmanaged
        {
            int count = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            for (int i = 0; i < original.Images.Length; i++)
            {
                ulong start = original.Images[i].Binding.Address;
                if (start <= address && address <= ulong.MaxValue - (ulong)count && address + (ulong)count <= start + (ulong)bytes[i].Length)
                {
                    int off = checked((int)(address - start));
                    if (known[i].AsSpan(off, count).IndexOf((byte)0) >= 0) return null;
                    return System.Runtime.InteropServices.MemoryMarshal.Read<T>(bytes[i].AsSpan(off, count));
                }
            }
            return null;
        }
        internal byte[] Raw(ulong address, int count)
        { var (i, off) = Locate(address, count); return bytes[i].AsSpan(off, count).ToArray(); }
        internal byte[] Read(ulong address, int count, ulong site, string name)
        {
            byte[] value;
            try { value = Peek(address, count); }
            catch (Domain e) { throw Error(e.Failure.Code, name, site, e.Message); }
            Note(site, "Read", name, address, Convert.ToHexStringLower(value), ""); return value;
        }
        internal byte U8(ulong a, ulong s, string n) => Read(a, 1, s, n)[0];
        internal ushort U16(ulong a, ulong s, string n) => BinaryPrimitives.ReadUInt16LittleEndian(Read(a, 2, s, n));
        internal int I32(ulong a, ulong s, string n) => BinaryPrimitives.ReadInt32LittleEndian(Read(a, 4, s, n));
        internal uint U32(ulong a, ulong s, string n) => BinaryPrimitives.ReadUInt32LittleEndian(Read(a, 4, s, n));
        internal ulong U64(ulong a, ulong s, string n) => BinaryPrimitives.ReadUInt64LittleEndian(Read(a, 8, s, n));
        internal float F32(ulong a, ulong s, string n) => Normal(BitConverter.UInt32BitsToSingle(U32(a, s, n)));
        internal void Write(ulong address, ReadOnlySpan<byte> value, ulong site, string name)
        {
            var (i, off) = Locate(address, value.Length);
            string before = Convert.ToHexStringLower(bytes[i].AsSpan(off, value.Length));
            value.CopyTo(bytes[i].AsSpan(off)); known[i].AsSpan(off, value.Length).Fill(1);
            Note(site, "Write", name, address, before, Convert.ToHexStringLower(value));
        }
        internal void W8(ulong a, byte v, ulong s, string n) => Write(a, [v], s, n);
        internal void W32(ulong a, uint v, ulong s, string n)
        { Span<byte> buffer = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(buffer, v); Write(a, buffer, s, n); }
        internal void W64(ulong a, ulong v, ulong s, string n)
        { Span<byte> buffer = stackalloc byte[8]; BinaryPrimitives.WriteUInt64LittleEndian(buffer, v); Write(a, buffer, s, n); }
        internal void Note(ulong site, string kind, string name, ulong address = 0, string before = "", string after = "", long? value = null)
        {
            Need(steps.Count < limit, "budget", "trace", site, "Adapter trace budget reached; native would continue.", true);
            steps.Add(new(steps.Count + 1, site, kind, name, address, before, after, value));
        }
        internal float Keep(string name, float value, ulong site = 0)
        { Normal(value); Note(site, "Float32", name, value: BitConverter.SingleToUInt32Bits(value)); return value; }
    }

    internal enum StatusGetter { Dodge, Parry, Block }
    internal readonly record struct StatusValue(uint ReturnedBits32, int Selector)
    { internal ushort Value => unchecked((ushort)ReturnedBits32); }
    internal static StatusValue Status(Cursor c, ulong user, StatusGetter kind)
    {
        Need(Enum.IsDefined(kind), "getter", "status", 0, "Undefined status getter.");
        (int offset, byte mask, int field, ulong site) = kind switch
        { StatusGetter.Dodge => (0x17c, (byte)4, 0x14ae, 0x1400df620ul),
          StatusGetter.Parry => (0x180, (byte)8, 0x14b4, 0x1400df6f0ul),
          _ => (0x184, (byte)16, 0x14b6, 0x1400df7c0ul) };
        // Actual NPC source gives R8B=0, so all PvP bonus operands stay unread.
        ulong primary = c.U64(user + 0x6e50, site, "User.primary");
        c.External(primary, 0x1cc, "snapshot");
        ushort occupied = c.U16(primary, site, "primary.occupied");
        if (occupied != 0)
        {
            if ((c.U8(primary + 0x1c8, site, "primary.status_flags") & mask) != 0)
                return new(unchecked((uint)c.I32(primary + (ulong)offset, site, "primary.status")), 0);
        }
        else
        {
            ulong backup = c.U64(user + 0x6e38, site, "User.backup");
            c.External(backup, 0x1cc, "snapshot");
            if (c.U16(backup, site, "backup.occupied") != 0 &&
                (c.U8(backup + 0x1c8, site, "backup.status_flags") & mask) != 0)
                return new(unchecked((uint)c.I32(backup + (ulong)offset, site, "backup.status")), 1);
        }
        return new(c.U16(user + (ulong)field, site, "User.live_status"), 2);
    }
    internal static uint Defense(Cursor c, ulong user) => SnapshotOrLive(c, user, 2, 0x178, 0x1494, 0x14036ecd1);
    internal static int Critical(Cursor c, ulong user, bool damageReduction)
    {
        uint mask = damageReduction ? 0x1000u : 0x800u;
        int offset = damageReduction ? 0x1a4 : 0x1a0;
        ulong site = damageReduction ? 0x14036e325ul : 0x14036e299ul;
        ulong primary = c.U64(user + 0x6e50, site, "critical.primary");
        c.External(primary, 0x1cc, "snapshot");
        ushort occupied = c.U16(primary, site, "critical.primary_occupied");
        ulong selected = primary;
        if (occupied == 0)
        {
            selected = c.U64(user + 0x6e38, site, "critical.backup");
            c.External(selected, 0x1cc, "snapshot");
            occupied = c.U16(selected, site, "critical.backup_occupied");
        }
        if (occupied != 0 && (c.U32(selected + 0x1c8, site, "critical.flags") & mask) != 0)
            return c.I32(selected + (ulong)offset, site, "critical.snapshot_signed32");
        return c.U16(user + (damageReduction ? 0x14caul : 0x14c6ul), site, "critical.live_word");
    }
    private static uint SnapshotOrLive(Cursor c, ulong user, uint mask, int offset, int field, ulong site)
    {
        ulong p = c.U64(user + 0x6e50, site, "PDef.primary");
        c.External(p, 0x1cc, "snapshot");
        if (c.U16(p, site, "PDef.primary_occupied") != 0)
        {
            if ((c.U8(p + 0x1c8, site, "PDef.primary_flags") & mask) != 0)
                return c.U32(p + (ulong)offset, site, "PDef.primary_unsigned32");
        }
        else
        {
            ulong b = c.U64(user + 0x6e38, site, "PDef.backup");
            c.External(b, 0x1cc, "snapshot");
            if (c.U16(b, site, "PDef.backup_occupied") != 0 &&
                (c.U8(b + 0x1c8, site, "PDef.backup_flags") & mask) != 0)
                return c.U32(b + (ulong)offset, site, "PDef.backup_unsigned32");
        }
        return c.U32(user + (ulong)field, site, "PDef.live_unsigned32");
    }
    internal static bool ShieldEligible(Cursor c, ulong user)
    {
        if (c.U8(user + 0x569c, 0x140540e10, "shield.live_present") != 0) return true;
        ulong p = c.U64(user + 0x6e50, 0x140540e10, "shield.primary");
        c.External(p, 0x1cc, "snapshot");
        if (c.U16(p, 0x140540e10, "shield.primary_occupied") != 0 &&
            (c.U8(p + 0x1c8, 0x140540e10, "shield.primary_flags") & 0x20) != 0) return true;
        ulong b = c.U64(user + 0x6e38, 0x140540e10, "shield.backup");
        c.External(b, 0x1cc, "snapshot");
        return c.U16(b, 0x140540e10, "shield.backup_occupied") != 0 &&
            (c.U8(b + 0x1c8, 0x140540e10, "shield.backup_flags") & 0x20) != 0;
    }
    internal readonly record struct ShieldValue(bool Applied, float Damage, int RatioSelector);
    internal static ShieldValue Shield(Cursor c, ulong user, float damage)
    {
        Normal(damage);
        ulong p = c.U64(user + 0x6e50, 0x1400e1bf0, "shield.primary");
        c.External(p, 0x1cc, "snapshot");
        ushort occupied = c.U16(p, 0x1400e1bfa, "shield.primary_occupied");
        ulong selected = 0; int selector = 2;
        if (occupied != 0)
        {
            if ((c.U8(p + 0x1c8, 0x1400e1c04, "shield.primary_ratio_flag") & 0x20) != 0)
            { selected = p; selector = 0; }
        }
        else
        {
            ulong b = c.U64(user + 0x6e38, 0x1400e1c21, "shield.backup");
            c.External(b, 0x1cc, "snapshot");
            if (c.U16(b, 0x1400e1c28, "shield.backup_occupied") != 0 &&
                (c.U8(b + 0x1c8, 0x1400e1c2e, "shield.backup_ratio_flag") & 0x20) != 0)
            { selected = b; selector = 1; }
        }
        float ratio = selector == 2 ? c.F32(user + 0x56b0, 0x1400e1c4c, "shield.live_ratio") :
            FromUInt(c.U32(selected + 0x188, 0x1400e1c0e, "shield.snapshot_ratio_unsigned"));
        ratio = Sub(ratio, 0f); // Actual source type2 excludes the User penetration child.
        ratio = ratio < 0f ? 0f : ratio > 1f ? 1f : ratio;
        c.Keep("shield.clamped_ratio", ratio, 0x1400e1cd2);
        if (selector == 2 && c.U8(user + 0x569c, 0x1400e1cf5, "shield.live_present") == 0)
            return new(false, damage, selector);
        float cap, reduction;
        if (selector == 2)
        {
            reduction = Mul(ratio, damage);
            cap = c.F32(user + 0x56ac, 0x1400e1d0f, "shield.live_fractional_cap");
        }
        else
        {
            ulong capSource = 0;
            if (occupied != 0)
            {
                if ((c.U8(p + 0x1c8, 0x1400e1d34, "shield.primary_cap_flag") & 0x40) != 0) capSource = p;
            }
            else
            {
                ulong b = c.U64(user + 0x6e38, 0x1400e1d47, "shield.backup_for_cap");
                if (c.U16(b, 0x1400e1d4e, "shield.backup_cap_occupied") != 0 &&
                    (c.U8(b + 0x1c8, 0x1400e1d54, "shield.backup_cap_flag") & 0x40) != 0) capSource = b;
            }
            uint rawCap = capSource != 0 ? c.U32(capSource + 0x18c, 0x1400e1d3e, "shield.snapshot_cap_unsigned") :
                unchecked((uint)Cvtt64(c.F32(user + 0x56ac, 0x1400e1d65, "shield.fallback_cap_cvtt64_low32")));
            reduction = Mul(ratio, damage);
            cap = FromUInt(rawCap);
        }
        if (cap <= reduction) reduction = cap;
        float after = Sub(damage, reduction); if (after < 0f) after = 0f;
        c.Keep("shield.cap", cap); c.Keep("shield.after", after, 0x1400e1da3);
        return new(true, after, selector);
    }
    internal static byte Movement(Cursor c, ulong user, uint originalTick, string caller)
    {
        // The owner consumes the per-call original tick before entering this getter.
        uint old = c.U32(user + 0x4094, 0x140578ed6, "User.movement_lock_before");
        c.W32(user + 0x4094, 1, 0x140578ed6, "User.movement_lock_acquire");
        Need(old == 0, "incomplete-child", "movement.contended_lock", 0x140578ee7,
            "Reached native contended lock requires its owner; no supplied post-lock result.", true);
        byte[] raw = c.Read(user + 0x4084, 16, 0x140578eec, "User.inline_movement16");
        c.W32(user + 0x4094, 0, 0x140578ef5, "User.movement_lock_release");
        uint firstTick = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4, 4));
        uint secondTick = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12, 4));
        byte flags = 0;
        if (raw[0] != 0 && unchecked(originalTick - firstTick) < 1332) flags |= raw[0];
        if (raw[8] != 0 && unchecked(originalTick - secondTick) > 666) flags |= raw[8];
        byte result = (byte)(flags & 7); c.Note(0x140578f4a, "ComputedGetter", caller, value: result); return result;
    }
    internal static ulong ClassLookup(Cursor c, int sourceId)
    {
        int bucket = unchecked((uint)(sourceId - 200000)) < 100000 ? 0 :
            unchecked((uint)(sourceId - 600000)) < 300000 ? 1 : -1;
        if (bucket < 0) return 0;
        int index = unchecked(sourceId - (bucket == 0 ? 200000 : 600000));
        c.External(0x14f398f40ul + (ulong)bucket * 4, 4, "class-count");
        c.External(0x14f398f30ul + (ulong)bucket * 8, 8, "class-table");
        int count = c.I32(0x14f398f40ul + (ulong)bucket * 4, 0x1400a9c44, "class_lookup.count");
        ulong table = c.U64(0x14f398f30ul + (ulong)bucket * 8, 0x1400a9c49, "class_lookup.table");
        if (table == 0 || index < 0) return 0;
        Need(table <= ulong.MaxValue - (ulong)index * 8, "range", "class_lookup.entry", 0x1400a9c59, "Invalid selected pointer address.");
        c.External(table + (ulong)index * 8, 8, "class-lookup-entry");
        ulong selected = c.U64(table + (ulong)index * 8, 0x1400a9c59, "class_lookup.selected_before_count_compare");
        return selected != 0 && index < count ? selected : 0;
    }
    internal static void CheckContext(Context context)
    {
        Need(context is not null && context.Sources == RequiredSources && Enum.IsDefined(context.Origin),
            "source", "context", 0, "Exact pinned source cohort and defined origin required.");
        Text(context.Operation); Text(context.Epoch); Text(context.Receipt);
    }
    internal static string Hash(ReadOnlySpan<byte> raw) => Convert.ToHexStringLower(SHA256.HashData(raw));
    internal static void Text(string? value) => Need(!string.IsNullOrWhiteSpace(value), "identity", "declaration", 0, "Missing identity/receipt declaration.");
    internal static Domain Error(string code, string operand, ulong site, string detail, bool unsupported = false) =>
        new(new(code, operand, site, detail, unsupported));
    internal static void Need([DoesNotReturnIf(false)] bool condition, string code, string operand, ulong site, string detail, bool unsupported = false)
    { if (!condition) throw Error(code, operand, site, detail, unsupported); }
    internal static float Normal(float value)
    {
        uint magnitude = BitConverter.SingleToUInt32Bits(value) & 0x7fffffff;
        Need(magnitude < 0x7f800000 && (magnitude == 0 || magnitude >= 0x00800000), "floating", "operand", 0,
            "Consumed nonfinite/subnormal value or intermediate is outside the finite contract."); return value;
    }
    internal static int Cvtt(float value)
    { Normal(value); Need(value >= -2147483648f && value < 2147483648f, "conversion", "signed32", 0, "Unrepresentable native CVTT32."); return (int)value; }
    internal static long Cvtt64(float value)
    { Normal(value); Need(value >= -9223372036854775808f && value < 9223372036854775808f, "conversion", "signed64", 0, "Unrepresentable native CVTT64."); return (long)value; }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float FromInt(int value) => Normal(value);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float FromUInt(uint value) => Normal(value);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Add(float a, float b) => Normal(a + b);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Sub(float a, float b) => Normal(a - b);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Mul(float a, float b) => Normal(a * b);
    [MethodImpl(MethodImplOptions.NoInlining)] internal static float Div(float a, float b)
    { Normal(a); Normal(b); Need(b != 0f, "conversion", "division", 0, "Zero denominator."); return Normal(a / b); }
}
