/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System;
using NetMQ;

public class Transporter : IDisposable
{
	private Dictionary<ushort, NetMQSocket> transportList = new Dictionary<ushort, NetMQSocket>();

	public T Get<T>(in ushort targetPort) where T : class
	{
		return (transportList.ContainsKey(targetPort)) ? transportList[targetPort] as T : null;
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

	~Transporter()
	{
		Dispose();
	}

	public virtual void Dispose()
	{
		// Console.WriteLine("Destruct DestroyTransporter");
		foreach (var item in transportList)
		{
			var transporter = item.Value;
			transporter.Close();
		}
		System.GC.SuppressFinalize(this);
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