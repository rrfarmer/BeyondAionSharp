namespace Aion.Commons.Options;

/// <summary>
/// Java parity: commons/options/Assertion. Compile-time-style flag gating the network reactor's
/// <c>assert</c> blocks. In Java, when NetworkAssertion is a false compile-time constant javac
/// elides the guarded blocks; in C# the guarded code simply never runs. Kept as a const so the
/// JIT can likewise drop the dead branches.
/// </summary>
public static class Assertion
{
    public const bool NetworkAssertion = false;
}
