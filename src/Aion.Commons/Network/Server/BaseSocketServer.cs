using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Aion.Commons.Network;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Network.Server
{
	/// <summary>
	/// Base class for socket servers (Login, Chat, Game).
	/// Handles NIO-style connection acceptance and graceful shutdown.
	/// </summary>
	public abstract class BaseSocketServer : IAsyncDisposable
	{
		protected ILogger _logger;
		protected TcpListener? _listener;
		protected CancellationTokenSource _shutdownTokenSource;
		protected string _serverName;
		protected IPAddress _bindAddress;
		protected int _port;
		protected int _maxConnections;
		protected int _activeConnections;

		protected BaseSocketServer(ILogger logger, string serverName, int port, int maxConnections = 10000)
			: this(logger, serverName, IPAddress.Any, port, maxConnections)
		{
		}

		protected BaseSocketServer(ILogger logger, string serverName, IPAddress bindAddress, int port, int maxConnections = 10000)
		{
			_logger = logger;
			_serverName = serverName;
			_bindAddress = bindAddress;
			_port = port;
			_maxConnections = maxConnections;
			_activeConnections = 0;
			_shutdownTokenSource = new CancellationTokenSource();
		}

		/// <summary>
		/// Start the server and begin accepting connections.
		/// </summary>
		public virtual async Task StartAsync()
		{
			try
			{
				_listener = new TcpListener(_bindAddress, _port);
				_listener.Start();

				_logger.LogInformation("{Server} listening on {Address}:{Port}", _serverName, _bindAddress, _port);

				// Accept connections until shutdown requested
				await AcceptConnectionsAsync(_shutdownTokenSource.Token);
			}
			catch (Exception ex)
			{
				_logger.LogCritical(ex, "{Server} startup failed", _serverName);
				throw;
			}
		}

		/// <summary>
		/// Accept incoming connections and spawn handlers for each.
		/// </summary>
		protected virtual async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					var client = await _listener!.AcceptTcpClientAsync(cancellationToken);

					if (_activeConnections >= _maxConnections)
					{
						_logger.LogWarning("Connection rejected: max connections ({Max}) reached", _maxConnections);
						client.Close();
						continue;
					}

					Interlocked.Increment(ref _activeConnections);

					// Spawn handler for this connection (fire-and-forget with error tracking)
					_ = HandleConnectionAsync(client, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					// Expected during shutdown
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error accepting connection");
				}
			}
		}

		/// <summary>
		/// Handle a single client connection (to be implemented by subclasses).
		/// </summary>
		protected abstract Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken);

		/// <summary>
		/// Gracefully shutdown the server with grace period for in-flight requests.
		/// </summary>
		public virtual async Task StopAsync(TimeSpan gracePeriod = default)
		{
			if (gracePeriod == default)
				gracePeriod = TimeSpan.FromSeconds(2);

			_logger.LogInformation("Shutting down {Server}...", _serverName);

			try
			{
				// Signal shutdown
				_shutdownTokenSource.Cancel();

				// Stop accepting new connections
				_listener?.Stop();

				// Wait for active connections to close gracefully
				var deadline = DateTime.UtcNow.Add(gracePeriod);
				while (_activeConnections > 0 && DateTime.UtcNow < deadline)
				{
					await Task.Delay(100);
				}

				if (_activeConnections > 0)
				{
					_logger.LogWarning("Timeout waiting for {Count} connections to close", _activeConnections);
				}

				_logger.LogInformation("{Server} stopped", _serverName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during {Server} shutdown", _serverName);
			}
		}

		/// <summary>
		/// Decrement active connection count.
		/// </summary>
		protected void ConnectionClosed()
		{
			Interlocked.Decrement(ref _activeConnections);
		}

		/// <summary>
		/// Get current count of active connections.
		/// </summary>
		public int GetActiveConnections() => _activeConnections;

		/// <summary>
		/// Cleanup resources.
		/// </summary>
		public virtual async ValueTask DisposeAsync()
		{
			await StopAsync();
			_listener?.Stop();
			_listener?.Dispose();
			_shutdownTokenSource.Dispose();
		}
	}

	/// <summary>
	/// Base class for handling individual client connections.
	/// Manages packet reading/writing lifecycle.
	/// </summary>
	public abstract class BaseClientConnection : IAsyncDisposable
	{
		protected ILogger _logger;
		protected TcpClient _client;
		protected NetworkStream _stream;
		protected string _clientId;
		protected bool _isConnected;

		protected BaseClientConnection(ILogger logger, TcpClient client, string clientId)
		{
			_logger = logger;
			_client = client;
			_clientId = clientId;
			_stream = client.GetStream();
			_isConnected = true;
		}

		/// <summary>
		/// Read data from the network stream with timeout.
		/// </summary>
		protected async Task<int> ReadAsync(byte[] buffer, int offset, int length, TimeSpan timeout)
		{
			using (var cts = new CancellationTokenSource(timeout))
			{
				try
				{
					return await _stream.ReadAsync(buffer, offset, length, cts.Token);
				}
				catch (OperationCanceledException)
				{
					throw new TimeoutException($"Read timeout on connection {_clientId}");
				}
			}
		}

		/// <summary>
		/// Write data to the network stream.
		/// </summary>
		protected async Task WriteAsync(byte[] buffer, int offset, int length)
		{
			await _stream.WriteAsync(buffer, offset, length);
			await _stream.FlushAsync();
		}

		/// <summary>
		/// Read a complete packet (frame).
		/// Override in subclasses to define packet format.
		/// </summary>
		protected abstract Task<PacketBuffer?> ReadPacketAsync();

		/// <summary>
		/// Process a received packet.
		/// Override in subclasses to handle game-specific logic.
		/// </summary>
		protected abstract Task ProcessPacketAsync(PacketBuffer packet);

		/// <summary>
		/// Main connection loop.
		/// </summary>
		public virtual async Task RunAsync()
		{
			try
			{
				_logger.LogInformation("Connection accepted: {ClientId}", _clientId);

				while (_isConnected)
				{
					var packet = await ReadPacketAsync();
					if (packet == null)
					{
						// Connection closed
						break;
					}

					await ProcessPacketAsync(packet);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error on connection {ClientId}", _clientId);
			}
			finally
			{
				await CloseAsync();
			}
		}

		/// <summary>
		/// Close the connection gracefully.
		/// </summary>
		public virtual async Task CloseAsync()
		{
			if (!_isConnected)
				return;

			_isConnected = false;

			try
			{
				_stream.Close();
				_client.Close();
			}
			catch
			{ /* Ignore cleanup errors */
			}

			_logger.LogInformation("Connection closed: {ClientId}", _clientId);
		}

		public virtual async ValueTask DisposeAsync()
		{
			await CloseAsync();
			_stream?.Dispose();
			_client?.Dispose();
		}
	}
}
