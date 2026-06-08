using System;

namespace Aion.GameServer.Commons.Nio;

/// <summary>
/// A container for data of a specific primitive type. Faithful minimal port of java.nio.Buffer
/// (the abstract base of ByteBuffer/ShortBuffer/FloatBuffer), used by the geoEngine .geo loader.
/// A buffer has: capacity ≥ limit ≥ position ≥ 0, plus an optional mark.
/// </summary>
public abstract class Buffer
{
    private int _mark = -1;
    private int _position = 0;
    private int _limit;
    private int _capacity;

    internal Buffer(int mark, int pos, int lim, int cap)
    {
        if (cap < 0)
            throw new ArgumentException("Negative capacity: " + cap);
        _capacity = cap;
        SetLimit(lim);
        SetPosition(pos);
        if (mark >= 0)
        {
            if (mark > pos)
                throw new ArgumentException("mark > position: (" + mark + " > " + pos + ")");
            _mark = mark;
        }
    }

    /// <summary>Java parity: capacity().</summary>
    public int Capacity()
    {
        return _capacity;
    }

    /// <summary>Java parity: position().</summary>
    public int Position()
    {
        return _position;
    }

    /// <summary>Java parity: position(int newPosition).</summary>
    public Buffer SetPosition(int newPosition)
    {
        if (newPosition > _limit || newPosition < 0)
            throw new ArgumentException("position out of bounds: " + newPosition);
        _position = newPosition;
        if (_mark > _position)
            _mark = -1;
        return this;
    }

    /// <summary>Java parity: limit().</summary>
    public int Limit()
    {
        return _limit;
    }

    /// <summary>Java parity: limit(int newLimit).</summary>
    public Buffer SetLimit(int newLimit)
    {
        if (newLimit > _capacity || newLimit < 0)
            throw new ArgumentException("limit out of bounds: " + newLimit);
        _limit = newLimit;
        if (_position > _limit)
            _position = _limit;
        if (_mark > _limit)
            _mark = -1;
        return this;
    }

    /// <summary>Java parity: mark().</summary>
    public Buffer Mark()
    {
        _mark = _position;
        return this;
    }

    /// <summary>Java parity: reset().</summary>
    public Buffer Reset()
    {
        int m = _mark;
        if (m < 0)
            throw new InvalidOperationException("Invalid mark");
        _position = m;
        return this;
    }

    /// <summary>Java parity: clear().</summary>
    public Buffer Clear()
    {
        _position = 0;
        _limit = _capacity;
        _mark = -1;
        return this;
    }

    /// <summary>Java parity: flip().</summary>
    public Buffer Flip()
    {
        _limit = _position;
        _position = 0;
        _mark = -1;
        return this;
    }

    /// <summary>Java parity: rewind().</summary>
    public Buffer Rewind()
    {
        _position = 0;
        _mark = -1;
        return this;
    }

    /// <summary>Java parity: remaining().</summary>
    public int Remaining()
    {
        return _limit - _position;
    }

    /// <summary>Java parity: hasRemaining().</summary>
    public bool HasRemaining()
    {
        return _position < _limit;
    }

    public abstract bool IsReadOnly();

    // --- internal helpers (Java parity: Buffer.nextGetIndex/nextPutIndex/checkIndex) ---

    internal int NextGetIndex()
    {
        if (_position >= _limit)
            throw new InvalidOperationException("BufferUnderflow");
        return _position++;
    }

    internal int NextGetIndex(int nb)
    {
        if (_limit - _position < nb)
            throw new InvalidOperationException("BufferUnderflow");
        int p = _position;
        _position += nb;
        return p;
    }

    internal int NextPutIndex()
    {
        if (_position >= _limit)
            throw new InvalidOperationException("BufferOverflow");
        return _position++;
    }

    internal int NextPutIndex(int nb)
    {
        if (_limit - _position < nb)
            throw new InvalidOperationException("BufferOverflow");
        int p = _position;
        _position += nb;
        return p;
    }

    internal int CheckIndex(int i)
    {
        if (i < 0 || i >= _limit)
            throw new IndexOutOfRangeException("index out of bounds: " + i);
        return i;
    }

    internal int CheckIndex(int i, int nb)
    {
        if (i < 0 || nb > _limit - i)
            throw new IndexOutOfRangeException("index out of bounds: " + i);
        return i;
    }
}
