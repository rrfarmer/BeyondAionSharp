using System;
using System.Collections.Generic;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/ArrayTransformer (Neon). Builds a typed array from the comma-separated items,
/// coercing each element with the transformer registered for the component type.
/// </summary>
public sealed class ArrayTransformer : CommaSeparatedValueTransformer
{
    public override bool Matches(Type targetType) => targetType.IsArray;

    protected override object? ParseObject(List<string> values, Type type)
    {
        Type arrayType = type.GetElementType()!;
        Array array = Array.CreateInstance(arrayType, values.Count);
        if (values.Count != 0)
        {
            PropertyTransformer pt = PropertyTransformers.Get(arrayType);
            for (int i = 0; i < values.Count; i++)
                array.SetValue(pt.Transform(values[i], arrayType), i);
        }
        return array;
    }
}
