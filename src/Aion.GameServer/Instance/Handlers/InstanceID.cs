using System;

namespace Aion.GameServer.Instance.Handlers;

/// <summary>Java parity: instance/handlers/InstanceID. Java @interface (RUNTIME/TYPE) → C# class Attribute.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class InstanceID : Attribute
{
    public int Value { get; }

    public InstanceID(int value)
    {
        Value = value;
    }
}
