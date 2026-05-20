namespace Aion.GameServer.Utils.Concurrent;

public sealed class OrderedTaskExecutor<TKey> : IAsyncDisposable
	where TKey : notnull
{
	private readonly Dictionary<TKey, Queue<WorkItem>> _queues = [];
	private readonly object _lock = new();
	private bool _disposed;

	public Task EnqueueAsync(TKey key, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
	{
		// Java parity: packet processor preserves FIFO handling per AionConnection.
		ArgumentNullException.ThrowIfNull(action);

		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var workItem = new WorkItem(action, completion, cancellationToken);
		var startWorker = false;

		lock (_lock)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);

			if (!_queues.TryGetValue(key, out var queue))
			{
				queue = new Queue<WorkItem>();
				_queues[key] = queue;
				startWorker = true;
			}

			queue.Enqueue(workItem);
		}

		if (startWorker)
			_ = Task.Run(() => DrainQueueAsync(key));

		return completion.Task;
	}

	public int GetActiveQueueCount()
	{
		lock (_lock)
		{
			return _queues.Count;
		}
	}

	private async Task DrainQueueAsync(TKey key)
	{
		while (true)
		{
			WorkItem workItem;
			lock (_lock)
			{
				if (!_queues.TryGetValue(key, out var queue) || queue.Count == 0)
				{
					_queues.Remove(key);
					return;
				}

				workItem = queue.Dequeue();
			}

			try
			{
				if (workItem.CancellationToken.IsCancellationRequested)
				{
					workItem.Completion.SetCanceled(workItem.CancellationToken);
					continue;
				}

				await workItem.Action(workItem.CancellationToken).ConfigureAwait(false);
				workItem.Completion.SetResult();
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == workItem.CancellationToken)
			{
				workItem.Completion.SetCanceled(workItem.CancellationToken);
			}
			catch (Exception ex)
			{
				workItem.Completion.SetException(ex);
			}
		}
	}

	public ValueTask DisposeAsync()
	{
		lock (_lock)
		{
			_disposed = true;
			foreach (var queue in _queues.Values)
			{
				foreach (var workItem in queue)
					workItem.Completion.TrySetCanceled();
			}
			_queues.Clear();
		}

		return ValueTask.CompletedTask;
	}

	private sealed record WorkItem(Func<CancellationToken, Task> Action, TaskCompletionSource Completion, CancellationToken CancellationToken);
}
