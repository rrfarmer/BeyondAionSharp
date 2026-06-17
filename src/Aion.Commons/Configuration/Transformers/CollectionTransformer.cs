using System;
using System.Collections;
using System.Collections.Generic;

namespace Aion.Commons.Configuration.Transformers;

/// <summary>
/// Java parity: transformers/CollectionTransformer (Neon). Builds a <c>Collection</c> from the comma-separated
/// items, coercing each element with the transformer registered for the element (generic) type.
/// <para>
/// Java resolves the declared interface to a default implementation (<c>List</c> → <c>ArrayList</c>,
/// <c>Set</c> → <c>HashSet</c>) and instantiates concrete classes reflectively. The C# port mirrors this against
/// the closed-generic member type: <c>List&lt;T&gt;</c>/<c>IList&lt;T&gt;</c>/<c>ICollection&lt;T&gt;</c>/
/// <c>IEnumerable&lt;T&gt;</c> → <c>List&lt;T&gt;</c>; <c>ISet&lt;T&gt;</c>/<c>HashSet&lt;T&gt;</c> →
/// <c>HashSet&lt;T&gt;</c>; any other concrete <c>ICollection&lt;T&gt;</c> is created via its parameterless ctor.
/// Empty input yields an empty collection (never null), matching Java. Elements are added through the closed
/// <c>ICollection&lt;T&gt;.Add</c> so both <c>List&lt;T&gt;</c> and <c>HashSet&lt;T&gt;</c> behave like their Java
/// counterparts (sets de-duplicate).
/// </para>
/// </summary>
public sealed class CollectionTransformer : CommaSeparatedValueTransformer
{
    public override bool Matches(Type targetType)
    {
        if (!targetType.IsGenericType)
            return false;
        // Must be a generic collection of exactly one element type (List<T>, ISet<T>, HashSet<T>, ...).
        if (targetType.GetGenericArguments().Length != 1)
            return false;
        return typeof(IEnumerable).IsAssignableFrom(targetType);
    }

    protected override object? ParseObject(List<string> values, Type type)
    {
        Type elementType = type.GetGenericArguments()[0];
        object collection = CreateCollection(type, elementType);

        if (values.Count != 0)
        {
            PropertyTransformer pt = PropertyTransformers.Get(elementType);
            var add = typeof(ICollection<>).MakeGenericType(elementType).GetMethod("Add")!;
            foreach (var val in values)
                add.Invoke(collection, new[] { pt.Transform(val, elementType) });
        }

        return collection;
    }

    private static object CreateCollection(Type declaredType, Type elementType)
    {
        if (declaredType.IsInterface)
        {
            Type genericDef = declaredType.GetGenericTypeDefinition();
            if (genericDef == typeof(ISet<>))
                return Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType))!;
            if (genericDef == typeof(IList<>) || genericDef == typeof(ICollection<>)
                || genericDef == typeof(IEnumerable<>) || genericDef == typeof(IReadOnlyList<>)
                || genericDef == typeof(IReadOnlyCollection<>))
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            throw new NotSupportedException("No default implementation for " + declaredType
                + ", a non abstract/interface collection type must be declared.");
        }

        // Concrete class (e.g. List<T>, HashSet<T>, or a custom Collection) — instantiate via its parameterless ctor.
        var ctor = declaredType.GetConstructor(Type.EmptyTypes);
        if (ctor == null)
            throw new NotSupportedException(declaredType + " has no parameterless constructor.");
        return ctor.Invoke(Array.Empty<object>());
    }
}
