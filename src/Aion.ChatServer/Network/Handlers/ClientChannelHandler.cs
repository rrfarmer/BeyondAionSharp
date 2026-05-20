using System.Buffers.Binary;
using System.Net.Sockets;
using Aion.ChatServer.Configuration;
using Aion.ChatServer.Data.Repositories;
using Aion.ChatServer.Models;
using Aion.ChatServer.Models.Channels;
using Aion.ChatServer.Network.Packets;
using Aion.ChatServer.Network.Packets.Client;
using Aion.ChatServer.Network.Packets.Server;
using Aion.ChatServer.Services;
using Aion.Commons.Network;
using Aion.Commons.Network.Server;
using Microsoft.Extensions.Logging;

namespace Aion.ChatServer.Network.Handlers;

public sealed class ClientChannelHandler : BaseClientConnection, IChatClientConnection
{
	private readonly IChatService _chatService;
	private readonly ChatChannels _channels;
	private readonly IBroadcastService _broadcastService;
	private readonly IChatLogRepository _chatLogRepository;
	private readonly ChatServerOptions _options;
	private readonly SemaphoreSlim _sendLock = new(1, 1);
	private readonly SemaphoreSlim _closeLock = new(1, 1);
	private ChatClientConnectionState _state = ChatClientConnectionState.Connected;
	private ChatClient? _chatClient;

	public ClientChannelHandler(
		ILogger logger,
		TcpClient client,
		string clientId,
		IChatService chatService,
		ChatChannels channels,
		IBroadcastService broadcastService,
		IChatLogRepository chatLogRepository,
		ChatServerOptions options)
		: base(logger, client, clientId)
	{
		_chatService = chatService;
		_channels = channels;
		_broadcastService = broadcastService;
		_chatLogRepository = chatLogRepository;
		_options = options;
	}

	protected override async Task<PacketBuffer?> ReadPacketAsync()
	{
		var header = await ReadExactOrNullAsync(2);
		if (header == null)
			return null;

		var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(header);
		if (frameLength < 3)
			return null;

		var payload = await ReadExactOrNullAsync(frameLength - 2);
		return payload == null ? null : new PacketBuffer(payload, strictReads: false);
	}

	protected override async Task ProcessPacketAsync(PacketBuffer packet)
	{
		var parsed = ClientPacketFactory.Create(packet, _state);
		switch (parsed)
		{
			case CmChatIni:
				await SendPacketAsync(new SmChatIni());
				break;
			case CmPlayerAuth auth:
				await HandlePlayerAuthAsync(auth);
				break;
			case CmChannelRequest request:
				await HandleChannelRequestAsync(request);
				break;
			case CmChannelLeave leave:
				HandleChannelLeave(leave);
				break;
			case CmChannelMessage message:
				await HandleChannelMessageAsync(message);
				break;
			case CmChannelCreate or CmChannelJoin or CmPlayerInfo or CmPing:
				break;
			case null:
				_logger.LogWarning("Unknown chat client packet from {ClientId} in state {State}", _clientId, _state);
				break;
			default:
				_logger.LogDebug("Parsed chat client packet 0x{Opcode:X2} in state {State}", parsed.OpCode, _state);
				break;
		}
	}

	public async Task SendPacketAsync(AbstractServerPacket packet, CancellationToken cancellationToken = default)
	{
		await _sendLock.WaitAsync(cancellationToken);
		try
		{
			if (!_isConnected)
				return;

			var frame = packet.SerializeFrame();
			await WriteAsync(frame, 0, frame.Length);
		}
		finally
		{
			_sendLock.Release();
		}
	}

	public async Task CloseAsync(CancellationToken cancellationToken = default)
	{
		await CloseAsync();
	}

	public override async Task CloseAsync()
	{
		await _closeLock.WaitAsync();
		try
		{
			if (!_isConnected)
				return;

			_state = ChatClientConnectionState.Disconnected;
			if (_chatClient != null)
				_broadcastService.RemoveClient(_chatClient);

			await _sendLock.WaitAsync();
			try
			{
				await base.CloseAsync();
			}
			finally
			{
				_sendLock.Release();
			}
		}
		finally
		{
			_closeLock.Release();
		}
	}

	private async Task HandlePlayerAuthAsync(CmPlayerAuth packet)
	{
		var attached = _chatService.RegisterPlayerConnection(packet.PlayerId, packet.Token, packet.Identifier, packet.CharacterName, packet.AccountName, this);
		if (!attached)
			return;

		_chatClient = _chatService.GetPlayer(packet.PlayerId);
		_state = ChatClientConnectionState.Authed;
		await SendPacketAsync(new SmPlayerAuthResponse());
	}

	private async Task HandleChannelRequestAsync(CmChannelRequest packet)
	{
		if (_chatClient == null)
			return;

		if (_options.LogChannelRequests)
			_logger.LogInformation("{Client} requested channel: {Identifier}", _chatClient, packet.ChannelIdentifier);

		var channel = _chatService.RegisterPlayerWithChannel(_chatClient, packet.ChannelRequestId, packet.ChannelIdentifier);
		if (channel != null)
			await SendPacketAsync(new SmChannelResponse(channel.ChannelId, packet.ChannelRequestId));
	}

	private void HandleChannelLeave(CmChannelLeave packet)
	{
		if (_chatClient == null)
			return;

		var channel = _channels.GetChannelById(packet.ChannelId);
		if (!_chatClient.RemoveChannel(channel))
			_logger.LogWarning("{Client}, could not leave channel id {ChannelId}", _chatClient, packet.ChannelId);
	}

	private async Task HandleChannelMessageAsync(CmChannelMessage packet)
	{
		if (_chatClient == null)
			return;

		var channel = _channels.GetChannelById(packet.ChannelId);
		if (channel == null)
			return;

		var message = new Message(channel, packet.Content, _chatClient);
		if (_chatClient.IsGagged())
		{
			var gagTimeMinutes = (_chatClient.GagTime - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) / 1000 / 60;
			message.SetText($"You have been gagged for {gagTimeMinutes} minutes.");
			await SendPacketAsync(new SmChannelMessage(message));
			return;
		}

		var floodProtectionTime = _chatClient.NextMessageTimeSeconds(channel.ChannelType);
		if (floodProtectionTime > 0)
		{
			message.SetText($"You can chat again in this channel in {floodProtectionTime} second{(floodProtectionTime == 1 ? "." : "s.")}");
			await SendPacketAsync(new SmChannelMessage(message));
			return;
		}

		_chatClient.UpdateLastMessageTime(channel.ChannelType);
		await _broadcastService.BroadcastMessageAsync(message);

		if (_options.LogChat)
			_logger.LogInformation("[{Channel}] {Sender}: {Message}", message.Channel.Name(), message.Sender.Name, message.TextString);
		if (_options.LogChatToDatabase)
			await _chatLogRepository.InsertChatLogAsync(message.Sender.Name, message.TextString, message.Channel.Name());
	}

	private async Task<byte[]?> ReadExactOrNullAsync(int length)
	{
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await ReadAsync(buffer, offset, length - offset, TimeSpan.FromSeconds(30));
			if (read == 0)
				return null;
			offset += read;
		}

		return buffer;
	}
}
