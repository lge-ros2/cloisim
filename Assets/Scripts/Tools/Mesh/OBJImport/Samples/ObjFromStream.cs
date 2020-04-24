using Dummiesman;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ObjFromStream : MonoBehaviour
{
	void Start()
	{
		var request = new UnityWebRequest();
		var webRequest = UnityWebRequest.Get("https://people.sc.fsu.edu/~jburkardt/data/obj/lamp.obj");
		while (!webRequest.isDone)
		{
			System.Threading.Thread.Sleep(1);
		}

		//create stream and load
		var textStream = new MemoryStream(Encoding.UTF8.GetBytes(webRequest.downloadHandler.text));
		var loadedObj = new OBJLoader().Load(textStream);
	}
}