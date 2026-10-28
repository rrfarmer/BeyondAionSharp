using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Xml.Linq;
using P = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcSpellInstantParameters;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Inactive magical-context owned raw images. Source equality, managed current
/// ownership and observed retail state are distinct. No physical-context admission is relabeled.</summary>
internal static class RetailNpcUserMagicalInputs
{
    internal enum Origin { ConstructedFixture, DeclaredCurrentSnapshot }
    internal const ulong NpcVtable=0x1412b8470,UserVtable=0x141161250;
    internal const string Package="frozen-USA-base-spell13-pilot25";
    internal const string DatasetD="05ee65a433d2764f97af414851983c7c03d5b17d5cde8f866e7fcf15756809a3";
    internal const string PilotD="bfaae899bd9aa2264f39cea9b2389d116f4a1954b5926f6113b5267c67f7b45b";
    internal const string Crosswalk="279d0f2ceea22d39dccf8c713c37ebc54413646d721cda0b7382ba6021fee88e";
    internal const string Proposal="d75e6d1a7116300cf5ae07d08cd40719b07db6f61ab4434162a15fe956d75680";
    internal const string IndependentReview="4b38e38eca195055476c9425e83661af73649f62522cbc8532e2694f97f3646b";
    internal sealed record Sources(string Main,string HistoricalPackage,string Dataset,string Pilot,
        string Crosswalk,string Proposal,string IndependentReview,string NativeBodies,string Abi,string Movement);
    internal static Sources RequiredSources=>new(P.MainSha256,Package,DatasetD,PilotD,Crosswalk,Proposal,IndependentReview,
        "3e0910364926000815341aabe669f9af200dc83fdc27e9175029713b4b29aabd",
        "f4225b2d6a7d46ea6932172b2436a9fa99465bd853b695545929a499e2461996",
        "941af9dc7561074b12577514f69cdddb9ff5d4ce535e8b8be51826e71f0bd353");
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
            Need(steps.Count < limit, "budget", "trace", site, "Adapter trace budget reached before a store; no unrecorded write is published.", true);
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

    /// <summary>Private raw-source/caller capability. Reprojects all original bytes through the
    /// historical13/43/25 admission, then binds the independently reviewed D equality crosswalk.
    /// Existing SelectedSpell constructors/copies are never accepted as admission tokens.</summary>
    internal sealed class SelectedUserSpell
    {
        internal P.SelectedSpell Spell {get;}
        internal P.NpcRecipe Npc {get;}
        internal Sources Sources {get;}
        internal int Chance17 {get;}
        internal int Chance18 {get;}
        internal bool ActivationReady=>false;
        private SelectedUserSpell(P.SelectedSpell spell,P.NpcRecipe npc,Sources sources,int r17,int r18)
        {Spell=spell;Npc=npc;Sources=sources;Chance17=r17;Chance18=r18;}
        internal static SelectedUserSpell Admit(ReadOnlySpan<byte> completeSkill,ReadOnlySpan<byte> completeNpc,
            P.NpcSkillBinding binding,ImmutableArray<P.BindingField> actualOrderedBinding,Sources sources)
        {
            Need(sources==RequiredSources,"source","capability",0,"Explicit historical package/D crosswalk/source pins required.");
            Need(completeSkill.Length is >0 and <=131072 && completeSkill.Length%2==0 && completeNpc.Length is >0 and <=131072
                && completeNpc.Length%2==0 && binding is not null && !actualOrderedBinding.IsDefault,"source","complete bytes",0,"Bounded complete source records and ordered binding required before allocation.");
            try
            {
                var spell=P.ProjectSelectedSpell(completeSkill,binding,P.RequiredPins);var npc=P.ProjectNpcRecipe(completeNpc,P.RequiredPins);
                Need(npc.Id==binding.NpcId && npc.Devname==binding.NpcDevname && binding.SkillLevel is >=0 and <=32767
                    && actualOrderedBinding.SequenceEqual(spell.OrderedBindingFields),"source","binding",0,"Exact source NPC/ordinal/ordered fields and signed16 level required.");
                var skills=XElement.Parse(npc.OrderedFields.Single(x=>x.Name=="skills").NormalizedXml).Elements().ToArray();
                Need(binding.ListOrdinal>=0 && binding.ListOrdinal<skills.Length,"source","NPC list",0,"Original list ordinal differs.");
                var fields=skills[binding.ListOrdinal].Elements().Select(x=>new P.BindingField(x.Name.LocalName,x.Value)).ToImmutableArray();
                Need(fields.SequenceEqual(actualOrderedBinding),"source","NPC binding",0,"Complete NPC raw record must contain exactly the selected ordered binding.");
                int Read(string key)
                {
                    var rows=spell.Occurrences(key);Need(rows.Length<=1,"source",key,0,"Duplicate constructor input.");
                    if(rows.IsEmpty)return 0;
                    Need(int.TryParse(rows[0].Text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out int value),"source",key,0,"Invalid raw integer.");return value;
                }
                int a=Read("effect1_reserved17"),b=Read("effect1_reserved18");
                if(a==0&&b==0)b=100;
                Need(a>=0 && unchecked(a+b)>=0,"source","chance",0,"Native chance helper validates the selected pair.");
                // All selected original records construct this exact pair. It is not a new default
                // applied to an arbitrary current Skill or to missing known-mask bytes.
                Need(a==0 && b==100,"source","selected chance",0,"Outside the reviewed thirteen-source chance domain.");
                return new(spell,npc,sources,a,b);
            }
            catch(RetailNpcMagicalCalculationInputs.ContractError e){throw Error("source",e.Path,0,e.Message);}
            catch(Exception e)when(e is InvalidOperationException or System.Xml.XmlException or FormatException)
            {throw Error("source","complete recipe",0,e.Message);}
        }
    }
    internal static void CheckContext(Context context)
    {
        Need(context is not null && context.Sources==RequiredSources && Enum.IsDefined(context.Origin),
            "source","context",0,"Exact magical source/current origin required.");Text(context.Operation);Text(context.Epoch);Text(context.Receipt);
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
