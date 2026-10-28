using System.Buffers.Binary;

namespace Aion.GameServer.Commons.Utils.RetailRandom;

/// <summary>Complete integer primitives from the pinned bodies. No process/global/system RNG access.</summary>
public static class RetailRandomEngine
{
    public const ulong NativeEngineAddress = 0x1518fb8d0;
    public const string MainBinarySha256 = "da1ee5633d65a7ebb436b29d15ca24e46ab4f267449384d177bf20d2d26eb8f1";
    public const string RngEvidenceSha256 = "5205ae20874718f803f2b86cac63be841d5f00c79c9fadf1284cffab578046f9";
    public const ulong NormalSeedReturnSite = 0x140013959;

    /// <summary>One fixed64/max descriptor raw call, including regeneration and exact preservation.</summary>
    public static (RetailRandomState State, RetailRandomRawStep Step) Draw(RetailRandomState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var engine = new Mutable(state);
        var step = engine.Draw();
        return (engine.Snapshot(), step);
    }

    /// <summary>
    /// Pure constructor suffix after a separately supplied normal uint32 provider return.
    /// This does not execute or calculate the original seed provider.
    /// </summary>
    public static RetailRandomState ApplyNormalSeedReturn(RetailRandomState previousStorage, uint originalSeed)
    {
        ArgumentNullException.ThrowIfNull(previousStorage);
        var engine = new Mutable(previousStorage);
        engine.ApplyNormalSeedReturn(originalSeed);
        return engine.Snapshot();
    }

    internal sealed class Mutable
    {
        private const ulong Twist = 0xb5026f5aa96619e9;
        private readonly byte[] _bytes;
        internal Mutable(RetailRandomState state) => _bytes = state.CopyBytes();
        internal RetailRandomState Snapshot() => RetailRandomState.FromOwnedEngineBytes(_bytes);
        private uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_bytes.AsSpan(offset));
        private ulong U64(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(_bytes.AsSpan(offset));
        private void W32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(_bytes.AsSpan(offset), value);
        private void W64(int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(_bytes.AsSpan(offset), value);
        private ulong Word(int index) => U64(8 + index * 8);
        private void SetWord(int index, ulong value) => W64(8 + index * 8, value);
        private static ulong Join(ulong current, ulong next) => (current & 0xffffffff80000000UL) | (next & 0x7fffffffUL);
        private static ulong Mix(ulong current, ulong next, ulong tap)
        {
            ulong joined = Join(current, next);
            return tap ^ (joined >> 1) ^ ((joined & 1) != 0 ? Twist : 0);
        }

        internal void ApplyNormalSeedReturn(uint seed)
        {
            //01396a mask,01398a word0,0139a9 remaining first half,0139b2 index.
            W64(0x1388, 0x5555555555555555);
            ulong value = seed;
            SetWord(0, value);
            for (int i = 1; i < 312; i++)
            {
                value = unchecked((value ^ (value >> 62)) * 0x5851f42d4c957f2dUL + (ulong)i);
                SetWord(i, value);
            }
            W32(0, 312);
        }

        internal RetailRandomRawStep Draw()
        {
            uint before = U32(0);
            var regeneration = RetailRandomRegeneration.None;
            if (before == 312)
            {
                // a0d700/a0db40: taps156..467 and next word312 can refer to
                // second-buffer words generated earlier by this same loop.
                for (int i = 0; i < 312; i++)
                    SetWord(312 + i, Mix(Word(i), Word(i + 1), Word(i + 156)));
                regeneration = RetailRandomRegeneration.WriteSecond312;
            }
            else if (before >= 624)
            {
                // d830: preserve source second half; late taps use newly written first half.
                for (int i = 0; i < 156; i++)
                    SetWord(i, Mix(Word(312 + i), Word(313 + i), Word(468 + i)));
                for (int i = 156; i < 311; i++)
                    SetWord(i, Mix(Word(312 + i), Word(313 + i), Word(i - 156)));
                SetWord(311, Mix(Word(623), Word(0), Word(155)));
                W32(0, 0);
                regeneration = RetailRandomRegeneration.WriteFirst312AndReset;
            }
            uint selected = U32(0);
            // selected is now0..623 for every uint32 before value.
            ulong value = Word((int)selected);
            uint after = selected + 1;
            W32(0, after);
            value ^= (value >> 29) & U64(0x1388);
            value ^= unchecked((value & 0x38eb3ffff6d3UL) << 17);
            value ^= unchecked((value & 0xffffffffffffbf77UL) << 37);
            value ^= value >> 43;
            return new(before, selected, after, value, regeneration);
        }
    }
}
