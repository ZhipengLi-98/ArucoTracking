namespace PaperPlaneTools.AR 
{
	using System;
	using UnityEngine;
	using UnityEngine.UI;
	using OpenCvSharp;

	using System.IO;
	using System.Net;
	using System.Net.Sockets;
	using System.Text;
	using System.Threading;
	using System.Collections.Concurrent;

	// Many ideas are taken from http://answers.unity3d.com/questions/773464/webcamtexture-correct-resolution-and-ratio.html#answer-1155328

	/// <summary>
	/// Base WebCamera class that takes care about video capturing.
	/// Is intended to be sub-classed and partially overridden to get
	/// desired behavior in the user Unity script
	/// </summary>
	public abstract class WebCamera: MonoBehaviour
	{
		
		Thread m_Thread;
		bool m_NetworkRunning;
		TcpListener m_Listener;
		TcpClient m_Client;
		Renderer m_Renderer;
		byte[] bufTotal;
		byte[] bufTotalTemp;
		bool flag = false;
		Texture2D tex;
		UnityEngine.Rect rec;

		/// <summary>
		/// Target surface to render WebCam stream
		/// </summary>
		public GameObject Surface;

		protected Nullable<WebCamDevice> webCamDevice = null;
		protected WebCamTexture webCamTexture = null;
		protected Texture2D renderedTexture = null;

		/// <summary>
		/// A kind of workaround for macOS issue: MacBook doesn't state it's webcam as frontal
		/// </summary>
		protected bool forceFrontalCamera = true;
		protected bool preferRearCamera = false;

		/// <summary>
		/// WebCam texture parameters to compensate rotations, flips etc.
		/// </summary>
		protected Unity.TextureConversionParams TextureParameters { get; private set; }

		/// <summary>
		/// Camera device name, full list can be taken from WebCamTextures.devices enumerator
		/// </summary>
		public string DeviceName
		{
			get
			{
				return (webCamDevice != null) ? webCamDevice.Value.name : null;
			}
			set
			{
				// quick test
				if (value == DeviceName)
					return;

				if (null != webCamTexture && webCamTexture.isPlaying)
					webCamTexture.Stop();

				// get device index
				int cameraIndex = -1;
				for (int i = 0; i < WebCamTexture.devices.Length && -1 == cameraIndex; i++)
				{
					if (WebCamTexture.devices[i].name == value)
						cameraIndex = i;
				}

				// set device up
				if (-1 != cameraIndex)
				{
					webCamDevice = WebCamTexture.devices[cameraIndex];
					webCamTexture = new WebCamTexture(webCamDevice.Value.name);

					// read device params and make conversion map
					ReadTextureConversionParameters();

					webCamTexture.Play();
				}
				else
				{
					throw new ArgumentException(String.Format("{0}: provided DeviceName is not correct device identifier", this.GetType().Name));
				}
			}
		}

		/// <summary>
		/// This method scans source device params (flip, rotation, front-camera status etc.) and
		/// prepares TextureConversionParameters that will compensate all that stuff for OpenCV
		/// </summary>
		private void ReadTextureConversionParameters()
		{
			Unity.TextureConversionParams parameters = new Unity.TextureConversionParams();

			// frontal camera - we must flip around Y axis to make it mirror-like
			parameters.FlipHorizontally = forceFrontalCamera || webCamDevice.Value.isFrontFacing;
			
			// TODO:
			// actually, code below should work, however, on our devices tests every device except iPad
			// returned "false", iPad said "true" but the texture wasn't actually flipped

			// compensate vertical flip
			//parameters.FlipVertically = webCamTexture.videoVerticallyMirrored;
			
			// deal with rotation
			if (0 != webCamTexture.videoRotationAngle)
				parameters.RotationAngle = webCamTexture.videoRotationAngle; // cw -> ccw

			// apply
			TextureParameters = parameters;

			//UnityEngine.Debug.Log (string.Format("front = {0}, vertMirrored = {1}, angle = {2}", webCamDevice.isFrontFacing, webCamTexture.videoVerticallyMirrored, webCamTexture.videoRotationAngle));
		}

		/// <summary>
		/// Default initializer for MonoBehavior sub-classes
		/// </summary>
		protected virtual void Awake()
		{
        	UnityThread.initUnityThread();  
			tex = new Texture2D(640, 480, TextureFormat.RGB24, false);
			rec = new UnityEngine.Rect(0, 0, tex.width, tex.height);
			// objectCamera_original_pos = virtualBasket.transform.position;
			// objectCamera_original_pos = virtualBasket.transform.position;
			m_Renderer = this.GetComponent<Renderer>();
			ThreadStart ts = new ThreadStart(GetInfo);
			m_Thread = new Thread(ts);
			m_Thread.Start();  
			// if (WebCamTexture.devices.Length > 0)
			// 	DeviceName = WebCamTexture.devices[WebCamTexture.devices.Length - 1].name;
		}

		//		protected virtual void Awake()
//		{
//		
//			text.text = "Awake";
//			int cameraIndex = -1;
//			for (int i = 0; i < WebCamTexture.devices.Length; i++) {
//				WebCamDevice webCamDevice = WebCamTexture.devices [i];
//				if (webCamDevice.isFrontFacing == false) {
//					cameraIndex = i;
//					break;
//				}
//				if (cameraIndex < 0) {
//					cameraIndex = i;
//				}
//			}
//
//			if (cameraIndex >= 0) {
//				DeviceName = WebCamTexture.devices [cameraIndex].name;
//				//webCamDevice = WebCamTexture.devices [cameraIndex];
//			}
//		}


		void OnDestroy() 
		{
        	m_NetworkRunning = false;
			// if (webCamTexture != null)
			// {
			// 	if (webCamTexture.isPlaying)
			// 	{
			// 		webCamTexture.Stop();
			// 	}
			// 	webCamTexture = null;
			// }

			// if (webCamDevice != null) 
			// {
			// 	webCamDevice = null;
			// }
		}

		/// <summary>
		/// Updates web camera texture
		/// </summary>
		private void Update ()
		{
			// if (webCamTexture != null && webCamTexture.didUpdateThisFrame)
			// {
			// 	// this must be called continuously
			// 	ReadTextureConversionParameters();

			// 	// process texture with whatever method sub-class might have in mind
			// 	if (ProcessTexture(webCamTexture, ref renderedTexture))
			// 	{
			// 		RenderFrame();
			// 	}
			// }
			if (flag)
			{
				// Texture2D tex = new Texture2D(640, 480, TextureFormat.RGB24, false);
				tex.LoadRawTextureData(bufTotal);
				tex.Apply();
				if (ProcessTexture(tex, ref renderedTexture))
				{
					RenderFrame();
				}
				// UnityEngine.Rect rec = new UnityEngine.Rect(0, 0, tex.width, tex.height);
				// image.GetComponent<Image>().sprite=Sprite.Create(tex, rec, new Vector2(0.5f, 0.5f), 1);
				flag = false;
			}
		}

		void GetInfo()
		{
			m_Listener = new TcpListener(IPAddress.Any, 9999);
			m_Listener.Start();

			m_Client = m_Listener.AcceptTcpClient();
			m_NetworkRunning = true;
			while (m_NetworkRunning)
			{
				Receive();
			}
			m_Listener.Stop();
		}

		void Receive()
		{
			NetworkStream nwStream = m_Client.GetStream();
			byte[] bufLength = new byte[16];
			int byReadNumber = nwStream.Read(bufLength, 0, 16);
			string dataReceived = Encoding.UTF8.GetString(bufLength, 0, byReadNumber);
			int dataSize = int.Parse(dataReceived.Trim());

			int byLengthTotal = 0;
			bufTotal = new byte[dataSize];

			// print(dataSize);
			while (true)
			{
				int leftSize = dataSize - byLengthTotal;
           		byte[] bufTmp = new byte[leftSize];

				int byReadTmp = nwStream.Read(bufTmp, 0, leftSize);

				Array.Copy(bufTmp, 0, bufTotal, byLengthTotal, byReadTmp);
	
				byLengthTotal += byReadTmp;

				if (byLengthTotal == dataSize)
				{
					break;
				}
			}if (dataSize == 640 * 480 * 3 && !flag)
			{
				flag = true;
			}
		}

		/// <summary>
		/// Processes current texture
		/// This function is intended to be overridden by sub-classes
		/// </summary>
		/// <param name="input">Input WebCamTexture object</param>
		/// <param name="output">Output Texture2D object</param>
		/// <returns>True if anything has been processed, false if output didn't change</returns>
		protected abstract bool ProcessTexture(WebCamTexture input, ref Texture2D output);
		
		protected abstract bool ProcessTexture(Texture2D input, ref Texture2D output);

		/// <summary>
		/// Renders frame onto the surface
		/// </summary>
		private void RenderFrame()
		{
			if (renderedTexture != null)
			{
				// apply
				Surface.GetComponent<RawImage>().texture = renderedTexture;

				// Adjust image ration according to the texture sizes 
				Surface.GetComponent<RectTransform>().sizeDelta = new Vector2(renderedTexture.width, renderedTexture.height);
			}
		}
	}
}