using System;

namespace Aion.GameServer.World.Zone.Handler;

/// <summary>Java parity: world/zone/handler/ZoneNameAnnotation. Java @interface (RUNTIME/TYPE) → C# class Attribute.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ZoneNameAnnotation : Attribute
{
    public string Value { get; }
    public int QuestId { get; set; } = 0;

    public ZoneNameAnnotation(string value)
    {
        Value = value;
    }
}
