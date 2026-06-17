using System;

namespace Aion.GameServer.Ai;

/// <summary>Java parity: ai/AIName. Java @interface (RUNTIME/TYPE) → C# class Attribute.
/// Java's @AIName is NOT @Inherited, so AIEngine.getAnnotation(AIName.class) returns null for a subclass
/// (e.g. SiegeNpcAI extends AggressiveNpcAI does NOT inherit "aggressive"). C# custom attributes default to
/// Inherited=true, which would make GetCustomAttribute&lt;AIName&gt;() on a subclass return the base's name and
/// double-register it ("Duplicate AIs with name aggressive"). Set Inherited=false to match Java exactly.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AIName : Attribute
{
    public string Value { get; }

    public AIName(string value)
    {
        Value = value;
    }
}
