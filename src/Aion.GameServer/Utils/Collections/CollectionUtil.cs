using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Utils.Collections;

/// <summary>Java parity: utils/collections/CollectionUtil — safe for-each with error logging.</summary>
public static class CollectionUtil
{
    private static readonly ILogger Log = NullLoggerFactory.Instance.CreateLogger(nameof(CollectionUtil));

    /// <summary>Java parity: forEach(Iterable, Consumer) — iterates without propagating per-element exceptions.</summary>
    public static void ForEach<T>(IEnumerable<T> iterable, Action<T> consumer) =>
        ForEach(iterable, consumer, null);

    /// <summary>Java parity: forEach(Iterable, Consumer, Supplier) — iterates; logs details on exception.</summary>
    public static void ForEach<T>(IEnumerable<T> iterable, Action<T> consumer, Func<string>? exceptionLogDetailsSupplier)
    {
        foreach (var obj in iterable)
        {
            try
            {
                consumer(obj);
            }
            catch (Exception e)
            {
                var details = exceptionLogDetailsSupplier == null ? "" : $" ({exceptionLogDetailsSupplier()})";
                Log.LogError(e, "Could not perform operation on {Object}{Details}", obj, details);
            }
        }
    }
}
