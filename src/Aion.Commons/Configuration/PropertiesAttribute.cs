using System;

namespace Aion.Commons.Configuration;

/// <summary>
/// Java parity: commons/configuration/Properties (Neon). A specialized form of <see cref="PropertyAttribute"/>
/// used to generate a key-value map of select config keys. Marks a static field/property whose type is a map
/// (<see cref="System.Collections.Generic.IDictionary{TKey,TValue}"/> / <c>Dictionary&lt;K,V&gt;</c>);
/// <see cref="ConfigurableProcessor"/> scans ALL loaded property keys, selects the ones matching
/// <see cref="KeyPattern"/>, derives the map key from the capturing group (or the whole key if none), and coerces
/// key→K and value→V via the transformer registry.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class PropertiesAttribute : Attribute
{
    /// <summary>
    /// Java parity: Properties.keyPattern() (default ".+"). Pattern used to filter which properties end up in the
    /// annotated result map. If the pattern contains a capturing group, the group content becomes the map keys;
    /// otherwise the whole matched property key is the map key.
    /// </summary>
    public string KeyPattern { get; }

    public PropertiesAttribute(string keyPattern = ".+")
    {
        KeyPattern = keyPattern;
    }
}
