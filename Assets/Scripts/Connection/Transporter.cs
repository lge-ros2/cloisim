/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Transporter : MonoBehaviour
{
	private Publisher publisher = null;
	private Subscriber subscriber = null;
	private Requestor requestor = null;
	private Responsor responsor = null;

	protected Publisher Publisher => this.publisher;
	protected Subscriber Subscriber => this.subscriber;
	protected Requestor Requestor => this.requestor;
	protected Responsor Responsor => this.responsor;

	protected bool InitializePublisher(in ushort targetPort, in ulong hash)
	{
		publisher = new Publisher();
		publisher.SetHash(hash);
		return publisher.Initialize(targetPort);
	}

	protected bool InitializeSubscriber(in ushort targetPort, in ulong hash)
	{
		subscriber = new Subscriber();
		subscriber.SetHash(hash);
		return subscriber.Initialize(targetPort);
	}

	protected bool InitializeResponsor(in ushort targetPort, in ulong hash)
	{
		responsor = new Responsor();
		responsor.SetHash(hash);
		return responsor.Initialize(targetPort);
	}

	protected bool InitializeRequester(in ushort targetPort, in ulong hash)
	{

		requestor = new Requestor();
		requestor.SetHash(hash);
		return requestor.Initialize(targetPort);
	}

	protected void DestroyTransporter()
	{
		// Debug.Log("DestroyTransporter");
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