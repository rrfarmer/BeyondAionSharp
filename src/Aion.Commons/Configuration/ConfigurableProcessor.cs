using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Aion.Commons.Configuration.Transformers;

namespace Aion.Commons.Configuration;

/// <summary>
/// Java parity: commons/configuration/ConfigurableProcessor (SoulKeeper). Reflects over the [Property]-annotated
/// members of the given holder type(s) and binds values from the loaded properties — applying the same key /
/// default-value semantics, the DO_NOT_OVERWRITE sentinel, the literal-empty ("") rule, ${...} placeholder
/// substitution, and per-type coercion via <see cref="PropertyTransformers"/>.
/// <para>
/// C# note: Java processes static fields of classes (and walks superclasses + interfaces). The C# config holders
/// are static classes whose members are static fields or static properties; this processor binds both. Instance
/// processing is supported symmetrically (pass an object). The Java @Properties (key-pattern map) annotation is
/// not yet ported — no migrated holder uses it; see Full-Parity-Backlog §C.
/// </para>
/// </summary>
public static class ConfigurableProcessor
{
    private static readonly Regex PropertyPattern = new(@"\$\{([^}]+)\}");

    /// <summary>
    /// Java parity: process(Properties, Object...). Returns the set of property keys that were not consumed by any
    /// bound member (i.e. present in <paramref name="properties"/> but with no matching [Property] key, or whose
    /// value equaled the field default and the key was absent).
    /// </summary>
    public static ISet<string> Process(JavaProperties properties, params object[] objectsOrClasses)
    {
        var unused = new HashSet<string>(properties.StringPropertyNames());
        foreach (var o in objectsOrClasses)
        {
            if (o is Type t)
                Process(t, null, properties, unused);
            else
                Process(o.GetType(), o, properties, unused);
        }
        return unused;
    }

    private static void Process(Type type, object? obj, JavaProperties props, ISet<string> unused)
    {
        ProcessMembers(type, obj, props, unused);
        // Walk the base-type chain (Java walks superclass up to Object and interfaces; C# static config holders
        // do not use interfaces/inheritance for [Property] members, but the base walk keeps the contract general).
        var baseType = type.BaseType;
        if (baseType != null && baseType != typeof(object) && baseType != typeof(ValueType))
            Process(baseType, obj, props, unused);
    }

    private static void ProcessMembers(Type type, object? obj, JavaProperties props, ISet<string> unused)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(flags))
        {
            bool isStatic = field.IsStatic;
            if (isStatic && obj != null) continue;   // static field skipped when processing an instance
            if (!isStatic && obj == null) continue;  // instance field skipped when processing a class

            var attr = field.GetCustomAttribute<PropertyAttribute>(inherit: true);
            if (attr == null) continue;
            if (field.IsInitOnly)
                throw new InvalidOperationException($"Can't process readonly field {field.Name} of class {type.FullName}");
            BindField(field, attr, obj, props, unused);
        }

        foreach (var prop in type.GetProperties(flags))
        {
            var accessor = prop.GetGetMethod(true) ?? prop.GetSetMethod(true);
            if (accessor == null) continue;
            bool isStatic = accessor.IsStatic;
            if (isStatic && obj != null) continue;
            if (!isStatic && obj == null) continue;

            var attr = prop.GetCustomAttribute<PropertyAttribute>(inherit: true);
            if (attr == null) continue;
            if (!prop.CanWrite)
                throw new InvalidOperationException($"Can't process get-only property {prop.Name} of class {type.FullName}");
            BindProperty(prop, attr, obj, props, unused);
        }
    }

    private static void BindField(FieldInfo field, PropertyAttribute attr, object? obj, JavaProperties props, ISet<string> unused)
    {
        try
        {
            string value = GetValue(attr.Key, attr.DefaultValue, props, unused);
            if (!PropertyAttribute.DEFAULT_VALUE.Equals(value))
                field.SetValue(obj, Transform(value, field.FieldType));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Error modifying field {field.Name} of {(object?)obj ?? field.DeclaringType}", e);
        }
    }

    private static void BindProperty(PropertyInfo prop, PropertyAttribute attr, object? obj, JavaProperties props, ISet<string> unused)
    {
        try
        {
            string value = GetValue(attr.Key, attr.DefaultValue, props, unused);
            if (!PropertyAttribute.DEFAULT_VALUE.Equals(value))
                prop.SetValue(obj, Transform(value, prop.PropertyType));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Error modifying property {prop.Name} of {(object?)obj ?? prop.DeclaringType}", e);
        }
    }

    /// <summary>Java parity: ConfigurableProcessor.transform(String, Field) — resolves the transformer by member type.</summary>
    public static object? Transform(string value, Type targetType)
        => PropertyTransformers.Get(targetType).Transform(value, targetType);

    /// <summary>Java parity: getValue — default fallback, unused-key tracking, "" literal-empty, and ${...} substitution.</summary>
    private static string GetValue(string key, string defaultValue, JavaProperties props, ISet<string>? unused)
    {
        string value = props.GetProperty(key, defaultValue);
        if (unused != null && (!Equals(value, defaultValue) || props.GetProperty(key) != null))
            unused.Remove(key);
        if (value.Trim() == "\"\"")
            value = "";
        else
            value = ReplacePropertyPlaceholders(value, props);
        return value;
    }

    private static string ReplacePropertyPlaceholders(string value, JavaProperties props)
    {
        var matches = PropertyPattern.Matches(value);
        foreach (Match m in matches)
        {
            string completeToken = m.Value;       // ${property.name}
            string token = m.Groups[1].Value;     // property.name
            string? replacement = props.GetProperty(token);
            value = value.Replace(completeToken, replacement ?? "");
        }
        return value;
    }
}
