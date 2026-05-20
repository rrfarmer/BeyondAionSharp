using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Aion.GameServer.Configuration;
using Aion.GameServer.Network.Aion;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

public class GameClientSocketServerSmokeTests
{
	[Fact]
	public async Task GameClientSocketServer_SendsSmKeyOnConnectAndStopsCleanly()
	{
		var server = CreateServer();
		var serverTask = Task.Run(() => server.StartAsync());

		try
		{
			var endpoint = await WaitForEndpointAsync(() => server.LocalEndPoint);
			using var client = new TcpClient();
			await client.ConnectAsync(endpoint.Address, endpoint.Port);
			await using var stream = client.GetStream();

			var frame = await ReadFrameAsync(stream);

			Assert.Equal(11, frame.Length);
			Assert.Equal(11, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(0, 2)));
			Assert.Equal(Convert.FromHexString("C8014437FE"), frame[2..7]);

			await server.StopAsync(TimeSpan.FromSeconds(1));
			await AssertClientClosedAsync(stream);
			Assert.Equal(0, server.GetActiveConnections());
			await AssertTaskCompletedAsync(serverTask);
		}
		finally
		{
			await server.StopAsync(TimeSpan.FromSeconds(1));
		}
	}

	[Fact]
	public async Task GameClientSocketServer_IgnoresFirstBadClientFrameWithoutCrashing()
	{
		var server = CreateServer();
		var serverTask = Task.Run(() => server.StartAsync());

		try
		{
			var endpoint = await WaitForEndpointAsync(() => server.LocalEndPoint);
			using var client = new TcpClient();
			await client.ConnectAsync(endpoint.Address, endpoint.Port);
			await using var stream = client.GetStream();
			await ReadFrameAsync(stream);

			await stream.WriteAsync(GamePacketFrameCodec.CreateFrame(new byte[] { 1, 2, 3, 4, 5 }));
			await Task.Delay(100);

			Assert.Equal(1, server.GetActiveConnections());
			await server.StopAsync(TimeSpan.FromSeconds(1));
			await AssertClientClosedAsync(stream);
			await AssertTaskCompletedAsync(serverTask);
		}
		finally
		{
			await server.StopAsync(TimeSpan.FromSeconds(1));
		}
	}

	private static GameClientSocketServer CreateServer()
	{
		var options = new GameServerOptions
		{
			Network = new GameServerNetworkOptions
			{
				ClientEndPoint = new IPEndPoint(IPAddress.Loopback, 0),
				MaxOnlinePlayers = 10,
			},
		};
		var processor = new GamePacketProcessor<string>((packet, cancellationToken) => Task.CompletedTask);
		return new GameClientSocketServer(NullLogger<GameClientSocketServer>.Instance, options, processor);
	}

	private static async Task<IPEndPoint> WaitForEndpointAsync(Func<IPEndPoint?> getEndpoint)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		while (!cts.IsCancellationRequested)
		{
			var endpoint = getEndpoint();
			if (endpoint is { Port: > 0 })
				return endpoint;
			await Task.Delay(20, cts.Token);
		}

		throw new TimeoutException("Socket server did not bind before timeout.");
	}

	private static async Task<byte[]> ReadFrameAsync(NetworkStream stream)
	{
		var header = await ReadExactAsync(stream, 2);
		var length = BinaryPrimitives.ReadUInt16LittleEndian(header);
		var payload = await ReadExactAsync(stream, length - 2);
		var frame = new byte[length];
		header.CopyTo(frame, 0);
		payload.CopyTo(frame.AsSpan(2));
		return frame;
	}

	private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int length)
	{
		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		var buffer = new byte[length];
		var offset = 0;
		while (offset < length)
		{
			var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cts.Token);
			if (read == 0)
				throw new EndOfStreamException("Socket closed before the expected frame was read.");
			offset += read;
		}

		return buffer;
	}

	private static async Task AssertClientClosedAsync(NetworkStream stream)
	{
		var buffer = new byte[1];
		using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
		var read = await stream.ReadAsync(buffer, timeout.Token);
		Assert.Equal(0, read);
	}

	private static async Task AssertTaskCompletedAsync(Task task)
	{
		var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(task, completed);
		await task;
	}
}
