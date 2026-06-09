using System;

namespace Aion.Commons.Scripting.Metadata;

/// <summary>
/// Java parity: commons/scripting/metadata/OnClassUnload (SoulKeeper).
/// Method marked as [OnClassUnload] will be called when there is a script reload or shutdown. Only static
/// methods with no arguments can be marked with this annotation. This is only used if
/// ScriptContext.GetClassListener() returns an instance of an OnClassLoadUnloadListener subclass.
/// @Target(METHOD) @Retention(RUNTIME)→AttributeUsage(Method).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnClassUnload : Attribute
{
}
