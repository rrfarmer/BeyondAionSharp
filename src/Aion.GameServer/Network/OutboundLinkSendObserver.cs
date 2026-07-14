using Microsoft.Extensions.Logging;

namespace Aion.GameServer.Network;

internal static class OutboundLinkSendObserver
{
	public static void Observe(
		Func<Task> sendFactory,
		ILogger logger,
		string linkName,
		string packetName)
	{
		Task sendTask;
		try
		{
			sendTask = sendFactory();
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to send {Packet} to {Link}", packetName, linkName);
			return;
		}

		if (sendTask.IsCompletedSuccessfully)
			return;
		_ = ObserveAsync(sendTask, logger, linkName, packetName);
	}

	internal static async Task ObserveAsync(
		Task sendTask,
		ILogger logger,
		string linkName,
		string packetName)
	{
		try
		{
			await sendTask;
		}
		catch (OperationCanceledException ex)
		{
			logger.LogDebug(ex, "Send of {Packet} to {Link} was canceled", packetName, linkName);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to send {Packet} to {Link}", packetName, linkName);
		}
	}
}
