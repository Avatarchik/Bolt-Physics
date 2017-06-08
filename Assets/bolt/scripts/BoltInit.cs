using UnityEngine;
using System.Collections;
using System;
using UdpKit;
using UnityEngine.SceneManagement;


public class BoltInit : Bolt.GlobalEventListener
{
    enum State
    {
        SelectMode,
        SelectMap,
        EnterServerIp,
        StartServer,
        StartClient,
        Started,
    }

    State state;

    string map;
    string serverAddress = "127.0.0.1";

    int serverPort = 25000;

    void Awake()
    {
        serverPort = BoltRuntimeSettings.instance.debugStartPort;
    }

    void OnGUI()
    {
        Rect tex = new Rect(10, 10, 140, 75);
        Rect area = new Rect(10, 90, Screen.width - 20, Screen.height - 100);

        GUI.Box(tex, Resources.Load("BoltLogo") as Texture2D);
        GUILayout.BeginArea(area);

        switch (state)
        {
            case State.SelectMode: State_SelectMode(); break;
            case State.SelectMap: State_SelectMap(); break;
            case State.EnterServerIp: State_EnterServerIp(); break;
            case State.StartClient: State_StartClient(); break;
            case State.StartServer: State_StartServer(); break;
        }

        GUILayout.EndArea();
    }

    private void State_EnterServerIp()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label("Server IP: ");
        serverAddress = GUILayout.TextField(serverAddress);

        if (GUILayout.Button("Connect"))
        {
            state = State.StartClient;
        }

        GUILayout.EndHorizontal();
    }


    void State_SelectMode()
    {
        if (ExpandButton("Server"))
        {
            state = State.SelectMap;
        }
        if (ExpandButton("Client"))
        {
            state = State.EnterServerIp;
        }
    }

    void State_SelectMap()
    {
        foreach (string value in BoltScenes.AllScenes)
        {
			
			
            if (SceneManager.GetActiveScene().name != value)			
            {
                if (ExpandButton(value))
                {
                    map = value;
                    state = State.StartServer;
                }
            }
        }
    }

    void State_StartServer()
    {
        BoltLauncher.StartServer(new UdpEndPoint(UdpIPv4Address.Any, (ushort)serverPort));

        state = State.Started;
    }

    void State_StartClient()
    {
        BoltLauncher.StartClient(UdpEndPoint.Any);
        state = State.Started;
    }

    bool ExpandButton(string text)
    {
        return GUILayout.Button(text, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
    }
    public override void BoltStartDone()
    {
        if(BoltNetwork.isClient)
        {
            BoltNetwork.Connect(new UdpEndPoint(UdpIPv4Address.Parse(serverAddress), (ushort)serverPort));

        }
        else
        {
            BoltNetwork.LoadScene(map);

        }
    }

}
