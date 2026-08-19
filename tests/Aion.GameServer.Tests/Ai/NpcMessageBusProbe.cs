using System;
using System.Collections.Generic;
using Aion.GameServer.Ai;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Tests.Ai;

/// <summary>
/// Records every message an NPC broadcasts, for encounters whose whole output is talk.
/// </summary>
/// <remarks>
/// See <see cref="NpcMessageBus.Observer"/> for why the seam exists. This wrapper exists so the
/// subscription is scoped to a <c>using</c> and cannot leak into the next pin — the delegate is a single
/// slot, so a test that left itself attached would silently steal another test's messages.
/// </remarks>
public sealed class NpcMessageBusProbe : IDisposable
{
	private NpcMessageBusProbe()
	{
	}

	/// <summary>Appends every broadcast message type to <paramref name="into"/> until disposed.</summary>
	public static NpcMessageBusProbe Watch(List<int> into)
	{
		NpcMessageBus.Observer = (_, messageType, _) => into.Add(messageType);
		return new NpcMessageBusProbe();
	}

	public void Dispose() => NpcMessageBus.Observer = null;
}
