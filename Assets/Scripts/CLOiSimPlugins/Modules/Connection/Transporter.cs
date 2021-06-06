/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public class Transporter : IDisposable
{
	private Publisher publisher = null;
	private Subscriber subscriber = null;
	private Requestor requestor = null;
	private Responsor responsor = null;

	public Publisher Publisher => this.publisher;
	public Subscriber Subscriber => this.subscriber;
	public Requestor Requestor => this.requestor;
	public Responsor Responsor => this.responsor;

	public bool InitializePublisher(in ushort targetPort, in ulong hash)
	{
		publisher = new Publisher();
		publisher.SetHash(hash);
		return publisher.Initialize(targetPort);
	}

	public bool InitializeSubscriber(in ushort targetPort, in ulong hash)
	{
		subscriber = new Subscriber();
		subscriber.SetHash(hash);
		return subscriber.Initialize(targetPort);
	}

	public bool InitializeResponsor(in ushort targetPort, in ulong hash)
	{
		responsor = new Responsor();
		responsor.SetHash(hash);
		return responsor.Initialize(targetPort);
	}

	public bool InitializeRequester(in ushort targetPort, in ulong hash)
	{

		requestor = new Requestor();
		requestor.SetHash(hash);
		return requestor.Initialize(targetPort);
	}

	~Transporter()
	{
		Dispose();
	}

	public virtual void Dispose()
	{
		// Console.WriteLine("Destruct DestroyTransporter");
		DestroyTransporter();
		System.GC.SuppressFinalize(this);
	}

	public void DestroyTransporter()
	{
		if (publisher != null)
		{
			publisher.Destroy();
		}

		if (subscriber != null)
		{
			subscriber.Destroy();
		}

		if (requestor != null)
		{
			requestor.Destroy();
		}

		if (responsor != null)
		{
			responsor.Destroy();
		}
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