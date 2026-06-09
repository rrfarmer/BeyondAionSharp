using System;

namespace Aion.Commons.Scripting.Metadata;

/// <summary>
/// Java parity: commons/scripting/metadata/OnClassLoad (SoulKeeper).
/// Method marked as [OnClassLoad] will be called when class was loaded by script. It's a more useful
/// alternative for a static { ... } block. Only static methods with no arguments can be marked with this
/// annotation. This is only used if ScriptContext.GetClassListener() returns an instance of an
/// OnClassLoadUnloadListener subclass.
/// @Target(METHOD) @Retention(RUNTIME)→AttributeUsage(Method).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnClassLoad : Attribute
{
}
