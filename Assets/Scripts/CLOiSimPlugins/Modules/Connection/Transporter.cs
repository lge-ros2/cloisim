/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System;
using System.Threading;
using NetMQ;

public class Transporter : IDisposable
{
	private Dictionary<ushort, NetMQSocket> transportList = new();
	private int _disposed;

	public T Get<T>(in ushort targetPort) where T : class
	{
		return transportList.ContainsKey(targetPort) ? transportList[targetPort] as T : null;
	}

	public bool InitializePublisher(in ushort targetPort, in ulong hash)
	{
		var publisher = new Publisher(hash);

		if (publisher.Initialize(targetPort))
		{
			transportList.Add(targetPort, publisher);
			return true;
		}
		return false;
	}

	public bool InitializeSubscriber(in ushort targetPort, in ulong hash)
	{
		var subscriber = new Subscriber(hash);

		if (subscriber.Initialize(targetPort))
		{
			transportList.Add(targetPort, subscriber);
			return true;
		}
		return false;
	}

	public bool InitializeResponsor(in ushort targetPort, in ulong hash)
	{
		var responsor = new Responsor(hash);

		if (responsor.Initialize(targetPort))
		{
			transportList.Add(targetPort, responsor);
			return true;
		}
		return false;
	}

	public bool InitializeRequester(in ushort targetPort, in ulong hash)
	{

		var requestor = new Requestor(hash);

		if (requestor.Initialize(targetPort))
		{
			transportList.Add(targetPort, requestor);
			return true;
		}
		return false;
	}

	public bool ReinitializeRequester(in ushort targetPort)
	{
		if (!transportList.TryGetValue(targetPort, out var existing))
			return false;

		var hash = (existing as Requestor)?.Hash ?? 0;
		transportList.Remove(targetPort);
		try { existing.Dispose(); } catch (Exception) { }
		return InitializeRequester(targetPort, hash);
	}

	~Transporter()
	{
		Dispose(false);
	}

	public virtual void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
		{
			return;
		}

		if (!disposing)
		{
			return;
		}

		var currentTransportList = transportList;
		transportList = new Dictionary<ushort, NetMQSocket>();

		foreach (var item in currentTransportList)
		{
			var transporter = item.Value;
			try
			{
				transporter?.Dispose();
			}
			catch (ObjectDisposedException)
			{
				// On app/play-mode exit, Main.OnDestroy may run NetMQConfig.Cleanup()
				// before this transport is disposed. Closing a socket against the
				// already-terminated context throws when its internal signaler socket
				// is gone. The socket resources are already reclaimed, so this is safe
				// to ignore; scoping the catch per-socket keeps the rest cleaning up.
			}
		}
		currentTransportList.Clear();
	}

	public static string GetAddress(in ushort port)
	{
		return GetAddress() + ":" + port;
	}

	public static string GetAddress()
	{
		return "tcp://*";
	}

}