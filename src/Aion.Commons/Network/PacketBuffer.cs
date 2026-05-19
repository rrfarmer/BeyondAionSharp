using System;
using System.Buffers.Binary;
using System.Text;

namespace Aion.Commons.Network
{
	/// <summary>
	/// Little-endian packet buffer for reading/writing binary data.
	/// Matches Java's ByteBuffer read/write semantics exactly for parity validation.
	/// All integer types are little-endian to match wire format.
	/// </summary>
	public class PacketBuffer : IDisposable
	{
		private byte[] _buffer;
		private int _position;
		private int _capacity;
		private readonly bool _strictReads;

		/// <summary>
		/// Create a new buffer with the specified capacity.
		/// </summary>
		public PacketBuffer(int capacity = 8192)
		{
			_buffer = new byte[capacity];
			_capacity = capacity;
			_position = 0;
			_strictReads = true;
		}

		/// <summary>
		/// Create a buffer from existing byte array (read mode).
		/// </summary>
		public PacketBuffer(byte[] data, bool strictReads = true)
		{
			_buffer = data ?? throw new ArgumentNullException(nameof(data));
			_capacity = data.Length;
			_position = 0;
			_strictReads = strictReads;
		}

		/// <summary>
		/// Current read/write position in the buffer.
		/// </summary>
		public int Position
		{
			get => _position;
			set
			{
				if (value < 0 || value > _capacity)
					throw new ArgumentOutOfRangeException(nameof(value));
				_position = value;
			}
		}

		/// <summary>
		/// Number of bytes remaining from current position to end.
		/// </summary>
		public int Remaining => _capacity - _position;

		/// <summary>
		/// Total buffer capacity.
		/// </summary>
		public int Capacity => _capacity;

		/// <summary>
		/// Underlying buffer data for serialization.
		/// </summary>
		public byte[] GetBuffer() => _buffer;

		/// <summary>
		/// Get the buffer slice that contains actual data (position 0 to limit).
		/// </summary>
		public byte[] ToArray() => new ArraySegment<byte>(_buffer, 0, _position).ToArray();

		// Write methods (little-endian)

		/// <summary>
		/// Write a single byte. (writeC in Java)
		/// </summary>
		public void WriteC(byte value)
		{
			EnsureCapacity(1);
			_buffer[_position++] = value;
		}

		/// <summary>
		/// Write the low byte of an integer. (writeC in Java)
		/// </summary>
		public void WriteC(int value)
		{
			WriteC((byte)value);
		}

		/// <summary>
		/// Write a 16-bit unsigned integer in little-endian. (writeH in Java)
		/// </summary>
		public void WriteH(ushort value)
		{
			EnsureCapacity(2);
			BinaryPrimitives.WriteUInt16LittleEndian(new Span<byte>(_buffer, _position, 2), value);
			_position += 2;
		}

		/// <summary>
		/// Write a 16-bit integer in little-endian. (writeH in Java)
		/// </summary>
		public void WriteH(int value)
		{
			WriteH((ushort)value);
		}

		/// <summary>
		/// Write a 32-bit signed integer in little-endian. (writeD in Java)
		/// </summary>
		public void WriteD(int value)
		{
			EnsureCapacity(4);
			BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_buffer, _position, 4), value);
			_position += 4;
		}

		/// <summary>
		/// Write a 64-bit signed integer in little-endian. (writeQ in Java)
		/// </summary>
		public void WriteQ(long value)
		{
			EnsureCapacity(8);
			BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(_buffer, _position, 8), value);
			_position += 8;
		}

		/// <summary>
		/// Write a 32-bit float in little-endian. (writeF in Java)
		/// </summary>
		public void WriteF(float value)
		{
			EnsureCapacity(4);
			var intValue = BitConverter.SingleToInt32Bits(value);
			BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(_buffer, _position, 4), intValue);
			_position += 4;
		}

		/// <summary>
		/// Write a 64-bit double in little-endian. (writeDF in Java)
		/// </summary>
		public void WriteDF(double value)
		{
			EnsureCapacity(8);
			var longValue = BitConverter.DoubleToInt64Bits(value);
			BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(_buffer, _position, 8), longValue);
			_position += 8;
		}

		/// <summary>
		/// Write a null-terminated UTF-16 string. (writeS in Java)
		/// </summary>
		public void WriteS(string? value)
		{
			if (value == null)
			{
				WriteH(0);
				return;
			}

			var charCount = value.Length;
			var byteCount = charCount * 2; // UTF-16, 2 bytes per char
			EnsureCapacity(byteCount + 2);

			// Write UTF-16 LE bytes
			var bytes = Encoding.Unicode.GetBytes(value);
			Array.Copy(bytes, 0, _buffer, _position, bytes.Length);
			_position += bytes.Length;

			// Java ByteBuffer.putChar('\0') terminator.
			WriteH(0);
		}

		/// <summary>
		/// Write raw bytes. (writeB in Java)
		/// </summary>
		public void WriteB(byte[] data)
		{
			if (data == null || data.Length == 0)
				return;

			EnsureCapacity(data.Length);
			Array.Copy(data, 0, _buffer, _position, data.Length);
			_position += data.Length;
		}

		// Read methods (little-endian)

		/// <summary>
		/// Read a single byte. (readC in Java)
		/// </summary>
		public byte ReadC()
		{
			if (Remaining < 1)
			{
				if (!_strictReads)
					return 0;
				throw new EndOfStreamException("Not enough data to read byte");
			}
			return _buffer[_position++];
		}

		/// <summary>
		/// Read a 16-bit unsigned integer in little-endian. (readH in Java)
		/// </summary>
		public ushort ReadH()
		{
			if (Remaining < 2)
			{
				if (!_strictReads)
					return 0;
				throw new EndOfStreamException("Not enough data to read ushort");
			}
			var value = BinaryPrimitives.ReadUInt16LittleEndian(new Span<byte>(_buffer, _position, 2));
			_position += 2;
			return value;
		}

		/// <summary>
		/// Read a 32-bit signed integer in little-endian. (readD in Java)
		/// </summary>
		public int ReadD()
		{
			if (Remaining < 4)
			{
				if (!_strictReads)
					return 0;
				throw new EndOfStreamException("Not enough data to read int");
			}
			var value = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(_buffer, _position, 4));
			_position += 4;
			return value;
		}

		/// <summary>
		/// Read a 64-bit signed integer in little-endian. (readQ in Java)
		/// </summary>
		public long ReadQ()
		{
			if (Remaining < 8)
			{
				if (!_strictReads)
					return 0;
				throw new EndOfStreamException("Not enough data to read long");
			}
			var value = BinaryPrimitives.ReadInt64LittleEndian(new Span<byte>(_buffer, _position, 8));
			_position += 8;
			return value;
		}

		/// <summary>
		/// Read a 32-bit float in little-endian. (readF in Java)
		/// </summary>
		public float ReadF()
		{
			if (Remaining < 4)
			{
				if (!_strictReads)
					return 0;
				throw new EndOfStreamException("Not enough data to read float");
			}
			var intValue = BinaryPrimitives.ReadInt32LittleEndian(new Span<byte>(_buffer, _position, 4));
			_position += 4;
			return BitConverter.Int32BitsToSingle(intValue);
		}

		/// <summary>
		/// Read a 64-bit double in little-endian. (readDF in Java)
		/// </summary>
		public double ReadDF()
		{
			if (Remaining < 8)
			{
				if (!_strictReads)
					return 0;
				throw new EndOfStreamException("Not enough data to read double");
			}
			var longValue = BinaryPrimitives.ReadInt64LittleEndian(new Span<byte>(_buffer, _position, 8));
			_position += 8;
			return BitConverter.Int64BitsToDouble(longValue);
		}

		/// <summary>
		/// Read a null-terminated UTF-16 string. (readS in Java)
		/// </summary>
		public string ReadS()
		{
			var sb = new StringBuilder();
			while (true)
			{
				if (!_strictReads && Remaining < 2)
					return sb.ToString();
				var ch = ReadH();
				if (ch == 0)
					return sb.ToString();
				sb.Append((char)ch);
			}
		}

		/// <summary>
		/// Read raw bytes and return them as a new array.
		/// </summary>
		public byte[] ReadB(int length)
		{
			if (length < 0)
				throw new EndOfStreamException("Invalid negative byte array length");
			var result = new byte[length];
			ReadB(result, 0, length);
			return result;
		}

		/// <summary>
		/// Read raw bytes into destination array.
		/// </summary>
		public void ReadB(byte[] destination, int offset, int length)
		{
			if (length < 0)
				throw new EndOfStreamException("Invalid negative byte array length");
			if (Remaining < length)
			{
				if (!_strictReads)
					return;
				throw new EndOfStreamException("Not enough data to read bytes");
			}
			Array.Copy(_buffer, _position, destination, offset, length);
			_position += length;
		}

		/// <summary>
		/// Read all remaining bytes from current position.
		/// </summary>
		public byte[] ReadRemaining()
		{
			var result = new byte[Remaining];
			ReadB(result, 0, Remaining);
			return result;
		}

		// Utility methods

		/// <summary>
		/// Reset position to beginning (for re-reading).
		/// </summary>
		public void Rewind()
		{
			_position = 0;
		}

		/// <summary>
		/// Compare this buffer's contents with another for parity testing.
		/// </summary>
		public bool ContentEquals(PacketBuffer other)
		{
			if (other == null || _position != other._position)
				return false;

			for (int i = 0; i < _position; i++)
			{
				if (_buffer[i] != other._buffer[i])
					return false;
			}
			return true;
		}

		private void EnsureCapacity(int needed)
		{
			if (_position + needed > _capacity)
			{
				throw new InvalidOperationException($"Buffer overflow: need {needed} bytes, but only {Remaining} remaining");
			}
		}

		public void Dispose()
		{
			_buffer = null!;
		}
	}
}
