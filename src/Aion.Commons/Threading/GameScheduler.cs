using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Aion.Commons.Threading
{
	/// <summary>
	/// Task scheduler for periodic operations (saves, world updates, etc.)
	/// Replaces Java's ScheduledExecutorService with .NET equivalents.
	/// </summary>
	public class GameScheduler : IAsyncDisposable
	{
		private readonly ILogger _logger;
		private readonly Dictionary<string, ScheduledTask> _tasks;
		private readonly CancellationTokenSource _shutdownToken;
		private readonly object _taskLock = new object();

		public GameScheduler(ILogger logger)
		{
			_logger = logger;
			_tasks = new Dictionary<string, ScheduledTask>();
			_shutdownToken = new CancellationTokenSource();
		}

		/// <summary>
		/// Schedule a task to run periodically at fixed rate.
		/// </summary>
		public void ScheduleAtFixedRate(string taskName, Func<Task> action, TimeSpan initialDelay, TimeSpan period)
		{
			lock (_taskLock)
			{
				if (_tasks.ContainsKey(taskName))
					throw new InvalidOperationException($"Task '{taskName}' is already scheduled");

				var task = new ScheduledTask
				{
					Name = taskName,
					Action = action,
					Period = period,
					IsPeriodic = true,
				};

				_tasks[taskName] = task;

				// Fire off the scheduled loop
				_ = ScheduleTaskLoopAsync(task, initialDelay);
			}
		}

		/// <summary>
		/// Schedule a task to run once after a delay.
		/// </summary>
		public void ScheduleOnce(string taskName, Func<Task> action, TimeSpan delay)
		{
			lock (_taskLock)
			{
				if (_tasks.ContainsKey(taskName))
					throw new InvalidOperationException($"Task '{taskName}' is already scheduled");

				var task = new ScheduledTask
				{
					Name = taskName,
					Action = action,
					IsPeriodic = false,
				};

				_tasks[taskName] = task;

				// Fire off the one-time task
				_ = ScheduleTaskOnceAsync(task, delay);
			}
		}

		private async Task ScheduleTaskLoopAsync(ScheduledTask task, TimeSpan initialDelay)
		{
			try
			{
				// Initial delay
				if (initialDelay > TimeSpan.Zero)
					await Task.Delay(initialDelay, _shutdownToken.Token);

				while (!_shutdownToken.Token.IsCancellationRequested)
				{
					var stopwatch = System.Diagnostics.Stopwatch.StartNew();

					try
					{
						await task.Action();
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error executing scheduled task '{TaskName}'", task.Name);
					}

					stopwatch.Stop();
					task.LastExecutionTime = stopwatch.Elapsed;

					// Wait until next period
					var nextDelay = task.Period - stopwatch.Elapsed;
					if (nextDelay > TimeSpan.Zero)
					{
						await Task.Delay(nextDelay, _shutdownToken.Token);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Expected during shutdown
			}
			finally
			{
				lock (_taskLock)
				{
					_tasks.Remove(task.Name);
				}
			}
		}

		private async Task ScheduleTaskOnceAsync(ScheduledTask task, TimeSpan delay)
		{
			try
			{
				if (delay > TimeSpan.Zero)
					await Task.Delay(delay, _shutdownToken.Token);

				var stopwatch = System.Diagnostics.Stopwatch.StartNew();

				try
				{
					await task.Action();
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error executing one-time task '{TaskName}'", task.Name);
				}

				stopwatch.Stop();
				task.LastExecutionTime = stopwatch.Elapsed;
			}
			catch (OperationCanceledException)
			{
				// Expected during shutdown
			}
			finally
			{
				lock (_taskLock)
				{
					_tasks.Remove(task.Name);
				}
			}
		}

		/// <summary>
		/// Cancel a scheduled task immediately.
		/// </summary>
		public bool CancelTask(string taskName)
		{
			lock (_taskLock)
			{
				return _tasks.Remove(taskName);
			}
		}

		/// <summary>
		/// Get information about a scheduled task.
		/// </summary>
		public ScheduledTaskInfo? GetTaskInfo(string taskName)
		{
			lock (_taskLock)
			{
				if (_tasks.TryGetValue(taskName, out var task))
				{
					return new ScheduledTaskInfo
					{
						Name = task.Name,
						Period = task.Period,
						LastExecutionTime = task.LastExecutionTime,
						IsPeriodic = task.IsPeriodic,
					};
				}
				return null;
			}
		}

		/// <summary>
		/// Get count of active scheduled tasks.
		/// </summary>
		public int GetActiveTaskCount()
		{
			lock (_taskLock)
			{
				return _tasks.Count;
			}
		}

		/// <summary>
		/// Shutdown scheduler and wait for all tasks to complete (with timeout).
		/// </summary>
		public async Task ShutdownAsync(TimeSpan timeout = default)
		{
			if (timeout == default)
				timeout = TimeSpan.FromSeconds(10);

			_shutdownToken.Cancel();

			var deadline = DateTime.UtcNow.Add(timeout);
			while (GetActiveTaskCount() > 0 && DateTime.UtcNow < deadline)
			{
				await Task.Delay(100);
			}

			if (GetActiveTaskCount() > 0)
			{
				_logger.LogWarning("Timeout waiting for {Count} scheduled tasks to complete", GetActiveTaskCount());
			}
		}

		public async ValueTask DisposeAsync()
		{
			await ShutdownAsync();
			_shutdownToken.Dispose();
		}
	}

	/// <summary>
	/// Information about a scheduled task.
	/// </summary>
	public class ScheduledTaskInfo
	{
		public string Name { get; set; } = "";
		public TimeSpan Period { get; set; }
		public TimeSpan LastExecutionTime { get; set; }
		public bool IsPeriodic { get; set; }
	}

	/// <summary>
	/// Internal representation of a scheduled task.
	/// </summary>
	internal class ScheduledTask
	{
		public string Name { get; set; } = "";
		public Func<Task> Action { get; set; } = null!;
		public TimeSpan Period { get; set; }
		public TimeSpan LastExecutionTime { get; set; }
		public bool IsPeriodic { get; set; }
	}
}
