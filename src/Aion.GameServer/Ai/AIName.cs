using System;

namespace Aion.GameServer.Ai;

/// <summary>Java parity: ai/AIName. Java @interface (RUNTIME/TYPE) → C# class Attribute.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AIName : Attribute
{
    public string Value { get; }

    public AIName(string value)
    {
        Value = value;
    }
}
