using System;

namespace Aion.Commons.Configuration;

/// <summary>
/// Java parity: commons/configuration/Property (SoulKeeper). Marks a static field/property that should be
/// processed by <see cref="ConfigurableProcessor"/>. Supported target types are controlled by the registered
/// transformers (see <see cref="Transformers.PropertyTransformers"/>).
/// <para>
/// C# note: Java annotates fields; the C# port allows the attribute on a field OR a static property (both carry
/// the same Java-key/default-value contract). The processor writes through whichever member shape is annotated.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PropertyAttribute : Attribute
{
    /// <summary>
    /// Java parity: Property.DEFAULT_VALUE. Sentinel meaning "do not overwrite the member's initialization value"
    /// when the key is absent from the loaded properties.
    /// </summary>
    public const string DEFAULT_VALUE = "DO_NOT_OVERWRITE_INITIALIAZION_VALUE";

    /// <summary>Java parity: Property.key(). Property name in configuration.</summary>
    public string Key { get; }

    /// <summary>
    /// Java parity: Property.defaultValue(). Value parsed if the key is not found. When left at
    /// <see cref="DEFAULT_VALUE"/> the member keeps its initialization value.
    /// </summary>
    public string DefaultValue { get; }

    public PropertyAttribute(string key, string defaultValue = DEFAULT_VALUE)
    {
        Key = key;
        DefaultValue = defaultValue;
    }
}
