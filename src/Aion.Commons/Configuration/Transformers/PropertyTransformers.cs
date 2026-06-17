using System;
using System.Collections.Generic;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/PropertyTransformers (SoulKeeper). Holds the ordered transformer registry and
/// resolves the first transformer that matches a target type.
/// <para>
/// Ported this slice (covers every type the migrated *Config holders use): Number, Boolean, Char, String, Enum,
/// Array. Java additionally registers Collection, File, InetSocketAddress, Pattern (registered here), Class,
/// TimeZone, ZoneId — added incrementally as config holders that need them are migrated (see Full-Parity-Backlog §C).
/// </para>
/// </summary>
public static class PropertyTransformers
{
    private static readonly List<PropertyTransformer> Registry = new();

    static PropertyTransformers()
    {
        Register(new NumberTransformer());
        Register(new BooleanTransformer());
        Register(new CharTransformer());
        Register(new StringTransformer());
        Register(new EnumTransformer());
        Register(new ArrayTransformer());
        Register(new PatternTransformer());
    }

    public static void Register(PropertyTransformer propertyTransformer) => Registry.Add(propertyTransformer);

    public static PropertyTransformer Get(Type targetType)
    {
        foreach (var transformer in Registry)
        {
            if (transformer.Matches(targetType))
                return transformer;
        }
        throw new ArgumentException("Transformer for " + targetType.Name + " is not registered.");
    }
}
