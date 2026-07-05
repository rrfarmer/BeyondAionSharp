using System;
using System.Collections;
using System.Collections.Generic;

namespace Aion.GameServer.Utils;

/// <summary>
/// C# equivalent of java.util.concurrent.CopyOnWriteArrayList. A thread-safe list where every
/// mutating operation takes a lock, copies the backing array, mutates the copy, then publishes it
/// via a single volatile write. Reads are lock-free and enumeration walks the array snapshot
/// captured when the enumerator was created — so a concurrent Add/Remove never affects an
/// in-progress iteration and never throws "Collection was modified" (which a plain List does).
/// </summary>
public sealed class CopyOnWriteArrayList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly object _lock = new object();
    private volatile T[] _array;

    public CopyOnWriteArrayList()
    {
        _array = Array.Empty<T>();
    }

    // Java parity: new CopyOnWriteArrayList<>(Collection) — snapshot the source into the backing array.
    public CopyOnWriteArrayList(IEnumerable<T> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        _array = new List<T>(source).ToArray();
    }

    public int Count => _array.Length;

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get => _array[index];
        set
        {
            lock (_lock)
            {
                T[] copy = (T[])_array.Clone();
                copy[index] = value;
                _array = copy;
            }
        }
    }

    // Java parity: add(E)
    public void Add(T item)
    {
        lock (_lock)
        {
            T[] snapshot = _array;
            T[] copy = new T[snapshot.Length + 1];
            Array.Copy(snapshot, copy, snapshot.Length);
            copy[snapshot.Length] = item;
            _array = copy;
        }
    }

    // Java parity: remove(Object) — removes the first occurrence; returns whether it was present.
    public bool Remove(T item)
    {
        lock (_lock)
        {
            T[] snapshot = _array;
            int index = Array.IndexOf(snapshot, item);
            if (index < 0)
                return false;
            RemoveAtLocked(snapshot, index);
            return true;
        }
    }

    // Java parity: add(int, E)
    public void Insert(int index, T item)
    {
        lock (_lock)
        {
            T[] snapshot = _array;
            if (index < 0 || index > snapshot.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            T[] copy = new T[snapshot.Length + 1];
            Array.Copy(snapshot, 0, copy, 0, index);
            copy[index] = item;
            Array.Copy(snapshot, index, copy, index + 1, snapshot.Length - index);
            _array = copy;
        }
    }

    // Java parity: remove(int)
    public void RemoveAt(int index)
    {
        lock (_lock)
        {
            T[] snapshot = _array;
            if (index < 0 || index >= snapshot.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            RemoveAtLocked(snapshot, index);
        }
    }

    // Java parity: clear()
    public void Clear()
    {
        lock (_lock)
        {
            _array = Array.Empty<T>();
        }
    }

    // Java parity: contains(Object)
    public bool Contains(T item) => Array.IndexOf(_array, item) >= 0;

    // Java parity: indexOf(Object)
    public int IndexOf(T item) => Array.IndexOf(_array, item);

    public void CopyTo(T[] array, int arrayIndex) => _array.CopyTo(array, arrayIndex);

    // Java parity: iterator() — snapshot iterator; unaffected by later mutations.
    public IEnumerator<T> GetEnumerator()
    {
        T[] snapshot = _array;
        for (int i = 0; i < snapshot.Length; i++)
            yield return snapshot[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // Caller must hold _lock. Publishes a fresh array with the element at `index` removed.
    private void RemoveAtLocked(T[] snapshot, int index)
    {
        T[] copy = new T[snapshot.Length - 1];
        Array.Copy(snapshot, 0, copy, 0, index);
        Array.Copy(snapshot, index + 1, copy, index, snapshot.Length - index - 1);
        _array = copy;
    }
}
