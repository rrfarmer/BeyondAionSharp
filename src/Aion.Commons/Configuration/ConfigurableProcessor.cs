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
/// ported via <see cref="PropertiesAttribute"/> + <see cref="Transformers.MapTransformer"/> — a member may carry
/// [Property] XOR [Properties], exactly as Java.
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
            var mapAttr = field.GetCustomAttribute<PropertiesAttribute>(inherit: true);
            if (attr == null && mapAttr == null) continue;
            if (field.IsInitOnly)
                throw new InvalidOperationException($"Can't process readonly field {field.Name} of class {type.FullName}");
            BindMember(field.FieldType, v => field.SetValue(obj, v), field.Name, type, attr, mapAttr, obj, props, unused);
        }

        foreach (var prop in type.GetProperties(flags))
        {
            var accessor = prop.GetGetMethod(true) ?? prop.GetSetMethod(true);
            if (accessor == null) continue;
            bool isStatic = accessor.IsStatic;
            if (isStatic && obj != null) continue;
            if (!isStatic && obj == null) continue;

            var attr = prop.GetCustomAttribute<PropertyAttribute>(inherit: true);
            var mapAttr = prop.GetCustomAttribute<PropertiesAttribute>(inherit: true);
            if (attr == null && mapAttr == null) continue;
            if (!prop.CanWrite)
                throw new InvalidOperationException($"Can't process get-only property {prop.Name} of class {type.FullName}");
            BindMember(prop.PropertyType, v => prop.SetValue(obj, v), prop.Name, type, attr, mapAttr, obj, props, unused);
        }
    }

    /// <summary>
    /// Java parity: processField — a member may be annotated with [Property] XOR [Properties] (not both). For
    /// [Property], bind a single key/default (skipping the DO_NOT_OVERWRITE sentinel). For [Properties], scan all
    /// loaded keys, filter by the key pattern (capturing-group → map key), and coerce into the map field.
    /// </summary>
    private static void BindMember(Type memberType, Action<object?> setter, string memberName, Type declaringType,
        PropertyAttribute? attr, PropertiesAttribute? mapAttr, object? obj, JavaProperties props, ISet<string> unused)
    {
        try
        {
            if (attr != null)
            {
                if (mapAttr != null)
                    throw new NotSupportedException("Field can only be annotated with [Property] or [Properties], not both.");
                string value = GetValue(attr.Key, attr.DefaultValue, props, unused);
                if (!PropertyAttribute.DEFAULT_VALUE.Equals(value))
                    setter(Transform(value, memberType));
            }
            else
            {
                var pattern = new Regex(mapAttr!.KeyPattern);
                IDictionary<string, string> values = FilterProperties(pattern, props, unused);
                setter(Transformers.MapTransformer.Transform(values, memberType));
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Error modifying field {memberName} of {(object?)obj ?? declaringType}", e);
        }
    }

    /// <summary>Java parity: ConfigurableProcessor.transform(String, Field) — resolves the transformer by member type.</summary>
    public static object? Transform(string value, Type targetType)
        => PropertyTransformers.Get(targetType).Transform(value, targetType);

    /// <summary>
    /// Java parity: filterProperties — for every loaded key, if the pattern matches (Java <c>Matcher.find()</c>),
    /// the map key is the first capturing group when present, otherwise the whole key; the value is resolved via
    /// getValue (default "", placeholder substitution, unused-key tracking).
    /// </summary>
    private static IDictionary<string, string> FilterProperties(Regex pattern, JavaProperties props, ISet<string> unused)
    {
        var input = new Dictionary<string, string>();
        foreach (var k in props.StringPropertyNames())
        {
            Match m = pattern.Match(k);
            if (m.Success)
            {
                string key = m.Groups.Count > 1 ? m.Groups[1].Value : k;
                string value = GetValue(k, "", props, unused);
                input[key] = value;
            }
        }
        return input;
    }

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
