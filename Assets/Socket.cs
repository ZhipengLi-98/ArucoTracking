using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine.UI;

public class Socket : MonoBehaviour
{
    Thread m_Thread;
    bool m_NetworkRunning;
    TcpListener m_Listener;
    TcpClient m_Client;
    Renderer m_Renderer;
    byte[] bufTotal;
    byte[] bufTotalTemp;
    public Image image;
    bool flag = false;

    Vector3 objectCamera_original_pos;
    Texture2D tex;
    UnityEngine.Rect rec;

    public GameObject objectCamera;
    public GameObject virtualBasket;
    public Vector3 objectOffset;

    private void Awake() 
    {
        UnityThread.initUnityThread();    
    }

    // Start is called before the first frame update
    void Start()
    {
        tex = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
        rec = new UnityEngine.Rect(0, 0, tex.width, tex.height);
        objectCamera_original_pos = virtualBasket.transform.position;
        // objectCamera_original_pos = virtualBasket.transform.position;
        m_Renderer = this.GetComponent<Renderer>();
        ThreadStart ts = new ThreadStart(GetInfo);
        m_Thread = new Thread(ts);
        m_Thread.Start();
    }

    // Update is called once per frame
    void Update()
    {
        // aruco
        // objectCamera.transform.rotation = Quaternion.Euler(90 - objectCam_rot_roll, -180 - objectCam_rot_yaw, -180 - objectCam_rot_pitch);
        // objectCamera.transform.position = objectCamera_original_pos - objectOffset + new Vector3(objectCam_pos_x, objectCam_pos_z, -objectCam_pos_y);

        // objectCamera.transform.position = objectCamera_original_pos + new Vector3(objectCam_pos_x, -objectCam_pos_y, -objectCam_pos_z);
        // objectCamera.transform.rotation = Quaternion.Euler(180 - objectCam_rot_roll, -180 - objectCam_rot_pitch, 180 + objectCam_rot_yaw);

        // chessboard
        // objectCamera.transform.position = objectCamera_original_pos + new Vector3(objectCam_pos_x, objectCam_pos_y, objectCam_pos_z);
        // objectCamera.transform.rotation = Quaternion.Euler(- objectCam_rot_roll, 180 + objectCam_rot_pitch, -180 - objectCam_rot_yaw);

        // qrcode
        if (flag)
        {
            // Texture2D tex = new Texture2D(640, 480, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(bufTotal);
            tex.Apply();
            // UnityEngine.Rect rec = new UnityEngine.Rect(0, 0, tex.width, tex.height);
            image.GetComponent<Image>().sprite=Sprite.Create(tex, rec, new Vector2(0.5f, 0.5f), 1);
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
        print(dataSize);

        int byLengthTotal = 0;
        if (dataSize < 50)
        {
            bufTotalTemp = new byte[dataSize];
        }
        else
        {
            bufTotal = new byte[dataSize];
        }

        while (true)
        {
            int leftSize = dataSize - byLengthTotal;

            byte[] bufTmp = new byte[leftSize];
            int byReadTmp = nwStream.Read(bufTmp, 0, leftSize);
            // print(Encoding.UTF8.GetString(bufTmp, 0, byReadTmp));

            if (dataSize < 60)
            {
                Array.Copy(bufTmp, 0, bufTotalTemp, byLengthTotal, byReadTmp);
            }
            else
            {
                Array.Copy(bufTmp, 0, bufTotal, byLengthTotal, byReadTmp);
            }
            byLengthTotal += byReadTmp;

            if (byLengthTotal == dataSize)
            {
                break;
            }
        }
        if (dataSize == 1280 * 720 * 3 && !flag)
        {
            flag = true;
        }
    }

    private void OnDestroy() 
    {
        m_NetworkRunning = false;
    }
}
