namespace Aion.GameServer.Ai;

/// <summary>
/// Marks an AI class with its registered name (used by the AI registry).
/// Java parity: ai/AIName — a runtime @interface annotation; C# models it as an attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AiNameAttribute : Attribute
{
    // Java parity: String value()
    public string Value { get; }

    public AiNameAttribute(string value) => Value = value;
}
