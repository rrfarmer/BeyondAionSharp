using System;
using System.Collections;
using System.Collections.Generic;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/MapTransformer (Neon). Builds a map from the filtered key→raw-value pairs produced by
/// <see cref="ConfigurableProcessor"/> for a [Properties] field, coercing each key with the transformer registered
/// for the map's key (generic) type and each value with the transformer registered for the value type.
/// <para>
/// Java resolves the declared map interface (<c>Map</c>) to a default implementation (<c>EnumMap</c> for enum keys,
/// else <c>HashMap</c>) and instantiates concrete map classes reflectively. The C# port mirrors this against the
/// closed-generic member type: <c>Dictionary&lt;K,V&gt;</c> / <c>IDictionary&lt;K,V&gt;</c> /
/// <c>IReadOnlyDictionary&lt;K,V&gt;</c> → <c>Dictionary&lt;K,V&gt;</c>; any other concrete
/// <c>IDictionary&lt;K,V&gt;</c> is created via its parameterless ctor. Empty input yields an empty map (never null).
/// </para>
/// </summary>
public static class MapTransformer
{
    public static object Transform(IDictionary<string, string> values, Type mapType)
    {
        if (!mapType.IsGenericType || mapType.GetGenericArguments().Length != 2
            || !typeof(IEnumerable).IsAssignableFrom(mapType))
            throw new ArgumentException(mapType + " is not a Map type");

        Type[] args = mapType.GetGenericArguments();
        Type keyType = args[0];
        Type valueType = args[1];

        object map = CreateMap(mapType, keyType, valueType);
        var add = typeof(IDictionary<,>).MakeGenericType(keyType, valueType).GetMethod("set_Item")!;

        PropertyTransformer keyTransformer = PropertyTransformers.Get(keyType);
        PropertyTransformer valueTransformer = PropertyTransformers.Get(valueType);

        foreach (var kv in values)
        {
            object? key = keyTransformer.Transform(kv.Key, keyType);
            try
            {
                object? value = valueTransformer.Transform(kv.Value, valueType);
                add.Invoke(map, new[] { key, value });
            }
            catch (Exception e)
            {
                throw new TransformationException("Could not transform property: " + kv.Key, e);
            }
        }
        return map;
    }

    private static object CreateMap(Type declaredType, Type keyType, Type valueType)
    {
        if (declaredType.IsInterface)
        {
            Type genericDef = declaredType.GetGenericTypeDefinition();
            if (genericDef == typeof(IDictionary<,>) || genericDef == typeof(IReadOnlyDictionary<,>))
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
            throw new NotSupportedException("No default implementation for " + declaredType
                + ", a non abstract/interface map type must be declared.");
        }

        var ctor = declaredType.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
            throw new NotSupportedException(declaredType + " has no parameterless constructor.");
        return ctor.Invoke(Array.Empty<object>());
    }
}
