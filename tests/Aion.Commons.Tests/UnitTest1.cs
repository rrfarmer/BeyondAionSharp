using Aion.Commons.Network;

namespace Aion.Commons.Tests;

/// <summary>
/// Golden tests for packet buffer byte-level parity with Java.
/// These tests establish the canonical wire format expectations.
/// </summary>
public class PacketBufferParityTests
{
	[Fact]
	public void WriteC_ProducesSingleByte()
	{
		var buf = new PacketBuffer();
		buf.WriteC(0x42);

		var data = buf.ToArray();
		Assert.Single(data);
		Assert.Equal(0x42, data[0]);
	}

	[Fact]
	public void WriteH_ProducesLittleEndianUShort()
	{
		var buf = new PacketBuffer();
		buf.WriteH(0x1234);

		var data = buf.ToArray();
		Assert.Equal(2, data.Length);
		// Little-endian: 0x34, 0x12
		Assert.Equal(0x34, data[0]);
		Assert.Equal(0x12, data[1]);
	}

	[Fact]
	public void WriteD_ProducesLittleEndianInt()
	{
		var buf = new PacketBuffer();
		buf.WriteD(0x12345678);

		var data = buf.ToArray();
		Assert.Equal(4, data.Length);
		// Little-endian: 0x78, 0x56, 0x34, 0x12
		Assert.Equal(0x78, data[0]);
		Assert.Equal(0x56, data[1]);
		Assert.Equal(0x34, data[2]);
		Assert.Equal(0x12, data[3]);
	}

	[Fact]
	public void WriteQ_ProducesLittleEndianLong()
	{
		var buf = new PacketBuffer();
		buf.WriteQ(0x123456789ABCDEF0L);

		var data = buf.ToArray();
		Assert.Equal(8, data.Length);
		// Little-endian: F0, DE, BC, 9A, 78, 56, 34, 12
		Assert.Equal(0xF0, data[0]);
		Assert.Equal(0xDE, data[1]);
		Assert.Equal(0xBC, data[2]);
		Assert.Equal(0x9A, data[3]);
		Assert.Equal(0x78, data[4]);
		Assert.Equal(0x56, data[5]);
		Assert.Equal(0x34, data[6]);
		Assert.Equal(0x12, data[7]);
	}

	[Fact]
	public void WriteS_WritesNullTerminatedUtf16()
	{
		var buf = new PacketBuffer();
		buf.WriteS("Hi");

		var data = buf.ToArray();
		// UTF-16 LE: "Hi" = 0x48 0x00 0x69 0x00
		Assert.Equal(6, data.Length); // 4 bytes text + 2 byte terminator
		Assert.Equal(0x48, data[0]);
		Assert.Equal(0x00, data[1]);
		Assert.Equal(0x69, data[2]);
		Assert.Equal(0x00, data[3]);
		Assert.Equal(0x00, data[4]);
		Assert.Equal(0x00, data[5]);
	}

	[Fact]
	public void WriteB_WritesBytesDirectly()
	{
		var buf = new PacketBuffer();
		buf.WriteB(new byte[] { 0xAA, 0xBB, 0xCC });

		var data = buf.ToArray();
		Assert.Equal(3, data.Length);
		Assert.Equal(0xAA, data[0]);
		Assert.Equal(0xBB, data[1]);
		Assert.Equal(0xCC, data[2]);
	}

	[Fact]
	public void ReadC_ReadsAByte()
	{
		var data = new byte[] { 0x42 };
		var buf = new PacketBuffer(data);

		var value = buf.ReadC();
		Assert.Equal(0x42, value);
		Assert.Equal(1, buf.Position);
	}

	[Fact]
	public void ReadH_ReadsLittleEndianUShort()
	{
		var data = new byte[] { 0x34, 0x12 };
		var buf = new PacketBuffer(data);

		var value = buf.ReadH();
		Assert.Equal(0x1234, value);
		Assert.Equal(2, buf.Position);
	}

	[Fact]
	public void ReadD_ReadsLittleEndianInt()
	{
		var data = new byte[] { 0x78, 0x56, 0x34, 0x12 };
		var buf = new PacketBuffer(data);

		var value = buf.ReadD();
		Assert.Equal(0x12345678, value);
		Assert.Equal(4, buf.Position);
	}

	[Fact]
	public void ReadQ_ReadsLittleEndianLong()
	{
		var data = new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 };
		var buf = new PacketBuffer(data);

		var value = buf.ReadQ();
		Assert.Equal(0x123456789ABCDEF0L, value);
		Assert.Equal(8, buf.Position);
	}

	[Fact]
	public void ReadS_ReadsUtf16String()
	{
		// "Hi" = UTF-16 text followed by a 0 char terminator.
		var data = new byte[] { 0x48, 0x00, 0x69, 0x00, 0x00, 0x00 };
		var buf = new PacketBuffer(data);

		var value = buf.ReadS();
		Assert.Equal("Hi", value);
		Assert.Equal(6, buf.Position);
	}

	[Fact]
	public void RoundTrip_WriteAndRead_ProducesIdenticalValue()
	{
		var buf1 = new PacketBuffer();
		buf1.WriteC(42);
		buf1.WriteH(1234);
		buf1.WriteD(123456);
		buf1.WriteQ(123456789L);
		buf1.WriteS("Test");

		var data = buf1.ToArray();
		var buf2 = new PacketBuffer(data);

		Assert.Equal(42, buf2.ReadC());
		Assert.Equal(1234, buf2.ReadH());
		Assert.Equal(123456, buf2.ReadD());
		Assert.Equal(123456789L, buf2.ReadQ());
		Assert.Equal("Test", buf2.ReadS());
	}

	[Fact]
	public void ContentEquals_ComparesBuffersCorrectly()
	{
		var buf1 = new PacketBuffer();
		buf1.WriteD(42);

		var buf2 = new PacketBuffer();
		buf2.WriteD(42);

		var buf3 = new PacketBuffer();
		buf3.WriteD(43);

		Assert.True(buf1.ContentEquals(buf2));
		Assert.False(buf1.ContentEquals(buf3));
	}

	[Fact]
	public void ReadRemaining_ReturnsAllRemainingBytes()
	{
		var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
		var buf = new PacketBuffer(data);

		buf.ReadC(); // skip first byte
		var remaining = buf.ReadRemaining();

		Assert.Equal(4, remaining.Length);
		Assert.Equal(new byte[] { 0x02, 0x03, 0x04, 0x05 }, remaining);
	}

	[Fact]
	public void Rewind_ResetsPosition()
	{
		var buf = new PacketBuffer();
		buf.WriteD(42);
		buf.WriteD(43);

		Assert.Equal(8, buf.Position);
		buf.Rewind();
		Assert.Equal(0, buf.Position);

		Assert.Equal(42, buf.ReadD());
		Assert.Equal(43, buf.ReadD());
	}

	[Fact]
	public void Overflow_ThrowsWhenCapacityExceeded()
	{
		var buf = new PacketBuffer(2);
		buf.WriteH(0x1234);

		// This should throw because buffer is full
		Assert.Throws<InvalidOperationException>(() => buf.WriteD(0x12345678));
	}

	[Fact]
	public void ReadPastEnd_ThrowsEndOfStreamException()
	{
		var data = new byte[] { 0x01, 0x02 };
		var buf = new PacketBuffer(data);

		buf.ReadH();
		Assert.Throws<EndOfStreamException>(() => buf.ReadD());
	}

	[Fact]
	public void NonStrictReads_ReturnJavaStyleDefaultsOnUnderflow()
	{
		var buf = new PacketBuffer(new byte[] { 0x01, 0x00 }, strictReads: false);

		Assert.Equal((ushort)1, buf.ReadH());
		Assert.Equal(0, buf.ReadD());
		Assert.Equal(0, buf.ReadC());
		Assert.Equal("", buf.ReadS());
		Assert.Equal(new byte[4], buf.ReadB(4));
	}
}
