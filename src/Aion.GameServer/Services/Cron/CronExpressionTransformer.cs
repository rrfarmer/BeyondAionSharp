using System;
using Aion.Commons.Configuration.Transformers;
using Quartz;

namespace Aion.GameServer.Services.Cron;

/// <summary>
/// Java parity: services/cron/CronExpressionTransformer (Neon). A <see cref="PropertyTransformer"/> that coerces a
/// configuration string into a Quartz <see cref="CronExpression"/> (the REAL Quartz.NET type the codebase already
/// uses — see CronService/CronExpressions), interning through <see cref="CronExpressions.GetOrCreate"/> exactly as
/// Java does. An empty string yields <c>null</c> (Java: <c>value.isEmpty() ? null : CronExpressions.getOrCreate(value)</c>).
/// <para>
/// C# note: Java registers this transformer in <c>GameServer.main</c> (it lives in game-server, not commons, because
/// it depends on the Quartz CronExpression type). The C# port mirrors that — it is NOT in the commons
/// <see cref="PropertyTransformers"/> default registry; instead <see cref="EnsureRegistered"/> idempotently adds it,
/// and the game-server config entrypoint (<see cref="Aion.GameServer.Configs.Config"/> static ctor) calls it before
/// any holder is processed.
/// </para>
/// </summary>
public sealed class CronExpressionTransformer : PropertyTransformer
{
    private static readonly object RegistrationLock = new();
    private static bool registered;

    /// <summary>
    /// Idempotently registers a single <see cref="CronExpressionTransformer"/> instance into the global
    /// <see cref="PropertyTransformers"/> registry. Safe to call repeatedly (mirrors the Java one-time
    /// <c>PropertyTransformers.register(new CronExpressionTransformer())</c> in GameServer.main, but guarded so test
    /// fixtures that bypass full boot still bind CronExpression fields).
    /// </summary>
    public static void EnsureRegistered()
    {
        if (registered)
            return;
        lock (RegistrationLock)
        {
            if (registered)
                return;
            PropertyTransformers.Register(new CronExpressionTransformer());
            registered = true;
        }
    }

    public override bool Matches(Type targetType) => targetType == typeof(CronExpression);

    protected override object? ParseObject(string value, Type type)
        => value.Length == 0 ? null : CronExpressions.GetOrCreate(value);
}
