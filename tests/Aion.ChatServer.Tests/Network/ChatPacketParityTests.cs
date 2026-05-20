using System.Net;
using System.Text;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network;
using Aion.ChatServer.Network.Handlers;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.GameServer;
using Aion.ChatServer.Network.Packets.Server;
using Aion.ChatServer.Services;
using Aion.Commons.Network;
using ClientSmPlayerAuthResponse = Aion.ChatServer.Network.Packets.Server.SmPlayerAuthResponse;
using GsSmPlayerAuthResponse = Aion.ChatServer.Network.Packets.GameServer.SmPlayerAuthResponse;

namespace Aion.ChatServer.Tests.Network;

public class ChatPacketParityTests
{
	[Fact]
	public void ClientFactory_ParsesConnectedPacketsFromJavaLayout()
	{
		var chatIni = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmChatIni>(
			ClientPacketFactory.Create(
				Packet(
					w => w.C(ClientPacketFactory.CmChatIni).C(0x40).H(0).D(1).D(2).D(3)),
				ChatClientConnectionState.Connected));
		Assert.Equal(0x40, chatIni.UnknownC);
		Assert.Equal(1, chatIni.UnknownD1);
		Assert.Equal(2, chatIni.UnknownD2);
		Assert.Equal(3, chatIni.UnknownD3);

		var token = Enumerable.Range(1, 48).Select(i => (byte)i).ToArray();
		var playerAuth = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmPlayerAuth>(
			ClientPacketFactory.Create(BuildClientPlayerAuthPayload(token), ChatClientConnectionState.Connected));
		Assert.Equal(0x10203040, playerAuth.PlayerId);
		Assert.Equal("AION", playerAuth.GameName);
		Assert.Equal("account", playerAuth.AccountName);
		Assert.Equal("Daeva", playerAuth.CharacterName);
		Assert.Equal(token, playerAuth.Token);
		Assert.Equal("Daeva@\u0001public_ALL\u00011.0.AION.KOR", Encoding.Unicode.GetString(playerAuth.Identifier));
	}

	[Fact]
	public void ClientFactory_ParsesAuthedPacketsFromJavaLayout()
	{
		var identifier = "@\u0001public_ALL\u00011.0.AION.KOR";
		var channelCreate = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmChannelCreate>(
			ClientPacketFactory.Create(
				Packet(
					w => w.C(ClientPacketFactory.CmChannelCreate).C(0x40).H(0).D(11).Bytes(new byte[16])
						.Utf16LengthBytes(identifier).Bytes(new byte[7]).Utf16LengthBytes("pw").H(0xFFFF)),
				ChatClientConnectionState.Authed));
		Assert.Equal(11, channelCreate.ChannelRequestId);
		Assert.Equal(identifier, channelCreate.ChannelIdentifier);
		Assert.Equal("pw", channelCreate.Password);
		Assert.Equal(0xFFFF, channelCreate.FinalMarker);

		var channelJoin = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmChannelJoin>(
			ClientPacketFactory.Create(
				Packet(
					w => w.C(ClientPacketFactory.CmChannelJoin).C(0x40).H(0).D(12).Bytes(new byte[16])
						.Utf16LengthBytes(identifier).Utf16LengthBytes("pw")),
				ChatClientConnectionState.Authed));
		Assert.Equal(12, channelJoin.ChannelRequestId);
		Assert.Equal(identifier, channelJoin.ChannelIdentifier);

		var channelRequest = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmChannelRequest>(
			ClientPacketFactory.Create(
				Packet(
					w => w.C(ClientPacketFactory.CmChannelRequest).C(0x40).H(0).D(13).Bytes(new byte[16])
						.Utf16LengthBytes(identifier).D(0)),
				ChatClientConnectionState.Authed));
		Assert.Equal(13, channelRequest.ChannelRequestId);
		Assert.Equal(identifier, channelRequest.ChannelIdentifier);

		var leave = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmChannelLeave>(
			ClientPacketFactory.Create(
				Packet(w => w.C(ClientPacketFactory.CmChannelLeave).C(0).H(0).Bytes(new byte[16]).D(0x11223344)),
				ChatClientConnectionState.Authed));
		Assert.Equal(0x11223344, leave.ChannelId);

		var messageText = "Hello";
		var message = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmChannelMessage>(
			ClientPacketFactory.Create(
				Packet(
					w => w.C(ClientPacketFactory.CmChannelMessage).H(0).C(0).D(1).D(2).D(3).D(4).D(0x55667788)
						.C(0).Utf16LengthBytes(messageText)),
				ChatClientConnectionState.Authed));
		Assert.Equal(0x55667788, message.ChannelId);
		Assert.Equal(Encoding.Unicode.GetBytes(messageText), message.Content);

		var playerInfo = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmPlayerInfo>(
			ClientPacketFactory.Create(
				Packet(w => w.C(ClientPacketFactory.CmPlayerInfo).C(0).H(0).C(4).D(0).D(65).Bytes(Enumerable.Repeat((byte)0xAB, 135).ToArray())),
				ChatClientConnectionState.Authed));
		Assert.Equal(4, playerInfo.ClassId);
		Assert.Equal(65, playerInfo.Level);
		Assert.Equal(135, playerInfo.UnknownBytes.Length);

		var ping = Assert.IsType<Aion.ChatServer.Network.Packets.Client.CmPing>(
			ClientPacketFactory.Create(
				Packet(w => w.C(ClientPacketFactory.CmPing).C(0).H(0).Bytes(new byte[16])),
				ChatClientConnectionState.Authed));
		Assert.Equal(16, ping.Padding.Length);
	}

	[Fact]
	public void ClientFacingServerPackets_SerializeWithJavaPayloads()
	{
		Assert.Equal(
			Bytes(0x02, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x22, 0x08),
			new ClientSmPlayerAuthResponse().SerializePayload());
		Assert.Equal(
			Bytes(0x0C, 0x00, 0x02, 0x40, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x22, 0x08),
			new ClientSmPlayerAuthResponse().SerializeFrame());

		Assert.Equal(
			Bytes(0x31, 0x40, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00),
			new SmChatIni().SerializePayload());

		Assert.Equal(
			Bytes(0x11, 0x40, 0x88, 0x77, 0x66, 0x55, 0x00, 0x00, 0x44, 0x33, 0x22, 0x11),
			new SmChannelResponse(0x11223344, 0x55667788).SerializePayload());
	}

	[Fact]
	public void ChannelMessagePacket_SerializesIdentifierAndTextLikeJava()
	{
		var sender = new ChatClient(0x10203040, new byte[48], "account", "Daeva", Race.Elyos, 0);
		var identifier = Encoding.Unicode.GetBytes("Daeva@\u0001public_ALL\u00011.0.AION.KOR");
		sender.AttachConnection(identifier, new FakeChatClientConnection());
		var channel = new RegionChannel(1, Race.Elyos, "ALL");
		var message = new Message(channel, Encoding.Unicode.GetBytes("Hello"), sender);

		var expected = new ByteWriter()
			.C(0x1A)
			.C(0)
			.D(0)
			.D(0)
			.D(channel.ChannelId)
			.D(sender.ClientId)
			.D(0)
			.C(0)
			.H(identifier.Length / 2)
			.Bytes(identifier)
			.H(message.Size / 2)
			.Bytes(message.Text)
			.ToArray();

		Assert.Equal(expected, new SmChannelMessage(message).SerializePayload());
	}

	[Fact]
	public void GameServerFactory_ParsesPacketsFromJavaLayout()
	{
		var auth = Assert.IsType<CmChatServerAuth>(
			GsPacketFactory.Create(Packet(w => w.C(GsPacketFactory.CmChatServerAuth).C(7).S("secret")), GameServerConnectionState.Connected));
		Assert.Equal(7, auth.GameServerId);
		Assert.Equal("secret", auth.Password);

		var playerAuth = Assert.IsType<CmPlayerAuth>(
			GsPacketFactory.Create(
				Packet(w => w.C(GsPacketFactory.CmPlayerAuth).D(0x10203040).S("account").S("Daeva").D(1).C(2)),
				GameServerConnectionState.Authed));
		Assert.Equal(0x10203040, playerAuth.PlayerId);
		Assert.Equal("account", playerAuth.AccountName);
		Assert.Equal("Daeva", playerAuth.Nickname);
		Assert.Equal(1, playerAuth.RaceId);
		Assert.Equal(2, playerAuth.AccessLevel);

		var logout = Assert.IsType<CmPlayerLogout>(
			GsPacketFactory.Create(Packet(w => w.C(GsPacketFactory.CmPlayerLogout).D(0x11223344)), GameServerConnectionState.Authed));
		Assert.Equal(0x11223344, logout.PlayerId);

		var gag = Assert.IsType<CmPlayerGag>(
			GsPacketFactory.Create(Packet(w => w.C(GsPacketFactory.CmPlayerGag).D(0x11223344).Q(0x0102030405060708)), GameServerConnectionState.Authed));
		Assert.Equal(0x11223344, gag.PlayerId);
		Assert.Equal(0x0102030405060708, gag.GagTimeMillis);
	}

	[Fact]
	public void GameServerPackets_SerializeWithJavaPayloads()
	{
		var options = new ChatServerOptions { ClientConnectEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10241) };
		Assert.Equal(
			Bytes(0x00, 0x00, 0x04, 0x7F, 0x00, 0x00, 0x01, 0x01, 0x28),
			new SmGameServerAuthResponse(GsAuthResponse.Authed, options).SerializePayload());
		Assert.Equal(
			Bytes(0x00, 0x01),
			new SmGameServerAuthResponse(GsAuthResponse.NotAuthed, options).SerializePayload());

		var token = Enumerable.Range(0, 48).Select(i => (byte)(0x80 + i)).ToArray();
		var expected = new ByteWriter()
			.C(0x01)
			.D(0x11223344)
			.C(token.Length)
			.Bytes(token)
			.ToArray();

		Assert.Equal(expected, new GsSmPlayerAuthResponse(0x11223344, token).SerializePayload());
	}

	private static PacketBuffer BuildClientPlayerAuthPayload(byte[] token)
	{
		const string identifier = "Daeva@\u0001public_ALL\u00011.0.AION.KOR";
		return Packet(
			w => w.C(ClientPacketFactory.CmPlayerAuth)
				.Utf16Bytes("@")
				.C(0)
				.D(1)
				.Utf16LengthBytes("AION")
				.D(27)
				.D(1)
				.D(0)
				.D(0x10203040)
				.D(0)
				.D(0)
				.D(0)
				.Utf16LengthBytes(identifier)
				.Utf16LengthBytes("account")
				.H(token.Length)
				.Bytes(token));
	}

	private static PacketBuffer Packet(Action<ByteWriter> write)
	{
		var writer = new ByteWriter();
		write(writer);
		return new PacketBuffer(writer.ToArray());
	}

	private static byte[] Bytes(params int[] values)
	{
		return values.Select(value => (byte)value).ToArray();
	}

	private sealed class FakeChatClientConnection : IChatClientConnection
	{
		public Task SendPacketAsync(AbstractServerPacket packet, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class ByteWriter
	{
		private readonly List<byte> _bytes = [];

		public ByteWriter C(int value)
		{
			_bytes.Add((byte)value);
			return this;
		}

		public ByteWriter H(int value)
		{
			_bytes.Add((byte)value);
			_bytes.Add((byte)(value >> 8));
			return this;
		}

		public ByteWriter D(int value)
		{
			_bytes.Add((byte)value);
			_bytes.Add((byte)(value >> 8));
			_bytes.Add((byte)(value >> 16));
			_bytes.Add((byte)(value >> 24));
			return this;
		}

		public ByteWriter Q(long value)
		{
			for (var i = 0; i < 8; i++)
				_bytes.Add((byte)(value >> (i * 8)));
			return this;
		}

		public ByteWriter Bytes(byte[] bytes)
		{
			_bytes.AddRange(bytes);
			return this;
		}

		public ByteWriter Utf16Bytes(string value)
		{
			_bytes.AddRange(Encoding.Unicode.GetBytes(value));
			return this;
		}

		public ByteWriter Utf16LengthBytes(string value)
		{
			H(value.Length);
			return Utf16Bytes(value);
		}

		public ByteWriter S(string value)
		{
			Utf16Bytes(value);
			H(0);
			return this;
		}

		public byte[] ToArray() => _bytes.ToArray();
	}
}
