using F = Aion.GameServer.Controllers.Attack.RetailNpc.RetailNpcNormalDamageStages;

namespace Aion.GameServer.Controllers.Attack.RetailNpc;

/// <summary>Ordinary NPC normal base arithmetic; no actor, memory reader or RNG.</summary>
internal static partial class RetailNpcStatNormalInputs
{
    internal readonly record struct Modifiers(int Percent, int Flat);

    /// <summary>The supplied sample is from the current per-hand range, after
    /// the native dual split when applicable. Profile and effect percentages add;
    /// neither scales the flat channel. Movement, defense, outcome, PvE,
    /// protection and final HP remain with their current owners.</summary>
    internal static float CalculateBase(int sample, int attackModifier, Modifiers modifiers)
    {
        if (sample is < 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(sample));
        float profileRate = F.Multiply(F.FromInt(attackModifier), BitConverter.UInt32BitsToSingle(0x3c23d70a));
        float effectRate = F.Divide(F.FromInt(modifiers.Percent), 100f);
        float value = F.Add(F.Multiply(F.FromInt(sample), F.Add(effectRate, profileRate)), F.FromInt(modifiers.Flat));
        return Math.Max(1f, value);
    }
}
