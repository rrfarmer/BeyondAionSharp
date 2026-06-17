using System;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: commons/configuration/transformers/PropertyTransformer. Coerces a string property value to an
/// instance of the target type. The public <see cref="Transform"/> wraps parsing failures in a
/// <see cref="TransformationException"/> exactly as the Java base does.
/// </summary>
public abstract class PropertyTransformer
{
    public object? Transform(string value, Type type)
    {
        try
        {
            return ParseObject(value, type);
        }
        catch (Exception e)
        {
            throw new TransformationException("Error parsing \"" + value + "\" as " + type.Name, e);
        }
    }

    public abstract bool Matches(Type targetType);

    protected abstract object? ParseObject(string value, Type type);
}
