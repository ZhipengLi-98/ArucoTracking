namespace PaperPlaneTools.AR {
	using OpenCvSharp;

	using UnityEngine;
	using System.Collections;
	using System.Runtime.InteropServices;
	using System;
	using System.Collections.Generic;
	using UnityEngine.UI;
	
	public class MainScript: WebCamera {
		[Serializable]
		public class MarkerObject
		{
			public int markerId;
			// public GameObject markerPrefab;
		}

		public class MarkerOnScene
		{
			public int bestMatchIndex = -1;
			public float destroyAt = -1f;
			public GameObject gameObject = null;
		}

		/// <summary>
		/// List of possible markers
		/// The list is set in Unity Inspector
		/// </summary>
		public List<MarkerObject> markers;

		/// <summary>
		/// The marker detector
		/// </summary>
		private MarkerDetector markerDetector;


		/// <summary>
		/// Objects on scene
		/// </summary>
		private Dictionary<int, List<MarkerOnScene>> gameObjects = new Dictionary<int, List<MarkerOnScene>>();

		private Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
		private Dictionary<int, Quaternion> rotations = new Dictionary<int, Quaternion>();

		public Vector3 posOffset;
		public Vector3 posOffset2;

		private Vector3 lastObjPos;
		private Quaternion lastObjRot;

		public GameObject obj;
		public GameObject plane;

		Texture2D texture;

		void Start () {
			markerDetector = new MarkerDetector ();

			foreach (MarkerObject markerObject in markers) {
				gameObjects.Add(markerObject.markerId, new List<MarkerOnScene>());
			}
			texture = new Texture2D(640, 480);
		}

		protected override void Awake() {
			base.Awake();
			texture = new Texture2D(640, 480);
			// int cameraIndex = -1;
			// for (int i = 0; i < WebCamTexture.devices.Length; i++) {
			// 	WebCamDevice webCamDevice = WebCamTexture.devices [i];
			// 	if (webCamDevice.isFrontFacing == false) {
			// 		cameraIndex = i;
			// 		break;
			// 	}
			// 	if (cameraIndex < 0) {
			// 		cameraIndex = i;
			// 	}
			// }

			// if (cameraIndex >= 0) {
			// 	DeviceName = WebCamTexture.devices [cameraIndex].name;
			// 	//webCamDevice = WebCamTexture.devices [cameraIndex];
			// }
		}

		protected override bool ProcessTexture(Texture2D input, ref Texture2D output) {
			// var texture = new Texture2D(input.width, input.height);
            texture = input;
            var img = Unity.TextureToMat(input, Unity.TextureConversionParams.Default180);
            ProcessFrame(img, img.Cols, img.Rows);
			Mat rgbMat = new Mat ();
			Cv2.CvtColor (img, rgbMat, ColorConversionCodes.BGR2RGB);
            output = Unity.MatToTexture(rgbMat, output);

            return true;
		}

		protected override bool ProcessTexture(WebCamTexture input, ref Texture2D output) {
			// var texture = new Texture2D(input.width, input.height);
            texture.SetPixels(input.GetPixels());
            var img = Unity.TextureToMat(input, Unity.TextureConversionParams.Default);
            ProcessFrame(img, img.Cols, img.Rows);
            output = Unity.MatToTexture(img, output);

            return true;
		}

		private void ProcessFrame (Mat mat, int width, int height) {
			List<int> markerIds = markerDetector.Detect (mat, width, height);

			int count = 0;
			foreach (MarkerObject markerObject in markers) {
				List<int> foundedMarkers = new List<int>();
				for (int i=0; i<markerIds.Count; i++) {
					if (markerIds[i] == markerObject.markerId) {
						foundedMarkers.Add(i);
						count++;
					}
				}
				ProcessMarkesWithSameId(markerObject, gameObjects[markerObject.markerId], foundedMarkers);
			}
			if (positions.Count == 0 || rotations.Count == 0)
			{
				return;
			}
			Vector3 posTemp = new Vector3(0, 0, 0);
			foreach (KeyValuePair<int, Vector3> pos in positions)
			{
				posTemp += pos.Value;
				// print(pos.Key + " " + pos.Value);
			}
			posTemp /= positions.Count;
			
			Quaternion rotTemp = new Quaternion(0, 0, 0, 0);
			int amount = 0;
			foreach (KeyValuePair<int, Quaternion> rot in rotations)
			{    
				amount++;			
				rotTemp = Quaternion.Slerp(rotTemp, rot.Value, 1 / amount);
			}
			if (lastObjPos != null)
			{
				posTemp = (posTemp + lastObjPos) / 2;
			}
			if (lastObjRot != null)
			{
				rotTemp = Quaternion.Slerp(rotTemp, lastObjRot, 1 / 2);
			}
			lastObjPos = posTemp;
			lastObjRot = rotTemp;
			obj.transform.localPosition = posTemp;
			plane.transform.localPosition = posTemp + posOffset2;
			// Quaternion.Euler(90, 0, 0) * 
			obj.transform.localRotation = rotTemp;
			plane.transform.localRotation = Quaternion.Euler(-rotTemp.eulerAngles[0], 0, 0);
		}

		private void ProcessMarkesWithSameId(MarkerObject markerObject, List<MarkerOnScene> gameObjects, List<int> foundedMarkers) {
			int index = 0;
		
			index = gameObjects.Count - 1;
			while (index >= 0) {
				MarkerOnScene markerOnScene = gameObjects[index];
				markerOnScene.bestMatchIndex = -1;
				if (markerOnScene.destroyAt > 0 && markerOnScene.destroyAt < Time.fixedTime) {
					Destroy(markerOnScene.gameObject);
					gameObjects.RemoveAt(index);
				}
				--index;
			}

			index = foundedMarkers.Count - 1;

			// Match markers with existing gameObjects
			while (index >= 0) {
				int markerIndex = foundedMarkers[index];
				Matrix4x4 transforMatrix = markerDetector.TransfromMatrixForIndex(markerIndex);
				Vector3 position = MatrixHelper.GetPosition(transforMatrix);

				float minDistance = float.MaxValue;
				int bestMatch = -1;
				for (int i=0; i<gameObjects.Count; i++) {
					MarkerOnScene markerOnScene = gameObjects [i];
					if (markerOnScene.bestMatchIndex >= 0) {
						continue;
					}
					float distance = Vector3.Distance(markerOnScene.gameObject.transform.position, position);
					if (distance<minDistance) {
						bestMatch = i;
					}
				}

				if (bestMatch >=0) {
					gameObjects[bestMatch].bestMatchIndex = markerIndex;
					foundedMarkers.RemoveAt(index);
				} 
				--index;
			}

			//Destroy excessive objects
			index = gameObjects.Count - 1;
			while (index >= 0) {
				MarkerOnScene markerOnScene = gameObjects[index];
				if (markerOnScene.bestMatchIndex < 0) {
					if (markerOnScene.destroyAt < 0) {
						markerOnScene.destroyAt = Time.fixedTime + 0.2f;
					}
				} else {
					markerOnScene.destroyAt = -1f;
					int markerIndex = markerOnScene.bestMatchIndex;
					Matrix4x4 transforMatrix = markerDetector.TransfromMatrixForIndex(markerIndex);
					Matrix4x4 matrixY = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, -1, 1));
					Matrix4x4 matrixZ = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, 1, -1));
					Matrix4x4 matrix = matrixY * transforMatrix * matrixZ;
					positions[markerObject.markerId] = MatrixHelper.GetPosition (matrix);
					rotations[markerObject.markerId] = MatrixHelper.GetQuaternion (matrix);
					// PositionObject(markerOnScene.gameObject, transforMatrix);
				}
				index--;
			}

			//Create objects for markers not matched with any game object
			foreach (int markerIndex in foundedMarkers) {
				// GameObject gameObject = Instantiate(markerObject.markerPrefab);
				// MarkerOnScene markerOnScene = new MarkerOnScene() {
				// 	gameObject = gameObject
				// };
				// gameObjects.Add(markerOnScene);
				Matrix4x4 transforMatrix = markerDetector.TransfromMatrixForIndex(markerIndex);
				Matrix4x4 matrixY = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, -1, 1));
				Matrix4x4 matrixZ = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, 1, -1));
				Matrix4x4 matrix = matrixY * transforMatrix * matrixZ;
				
				if (!positions.ContainsKey(markerObject.markerId))
				{
					positions.Add(markerObject.markerId, MatrixHelper.GetPosition (matrix));
				}
				else 
				{
					positions[markerObject.markerId] = MatrixHelper.GetPosition (matrix);
				}
				if (!rotations.ContainsKey(markerObject.markerId))
				{
					rotations.Add(markerObject.markerId, MatrixHelper.GetQuaternion (matrix));
				}
				else 
				{
					rotations[markerObject.markerId] = MatrixHelper.GetQuaternion (matrix);
				}
				// PositionObject(markerOnScene.gameObject, transforMatrix);
			}
		}

		private void PositionObject(GameObject gameObject, Matrix4x4 transforMatrix) {
			Matrix4x4 matrixY = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, -1, 1));
			Matrix4x4 matrixZ = Matrix4x4.TRS (Vector3.zero, Quaternion.identity, new Vector3 (1, 1, -1));
			Matrix4x4 matrix = matrixY * transforMatrix * matrixZ;

			gameObject.transform.localPosition = MatrixHelper.GetPosition (matrix);
			gameObject.transform.localRotation = MatrixHelper.GetQuaternion (matrix);
			gameObject.transform.localScale = MatrixHelper.GetScale (matrix);
		}
	}
}
