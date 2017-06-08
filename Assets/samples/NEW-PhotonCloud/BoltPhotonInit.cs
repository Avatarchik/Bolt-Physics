using UnityEngine;
using System.Collections;
using System;
using UdpKit;

// sample script for Bolt on top of PhotonPlatform
public class BoltPhotonInit : Bolt.GlobalEventListener {

  public class RoomProtocolToken : Bolt.IProtocolToken {
    public String ArbitraryData;

    public void Read(UdpPacket packet) {
      ArbitraryData = packet.ReadString();
    }

    public void Write(UdpPacket packet) {
      packet.WriteString(ArbitraryData);
    }
  }

   

  // helper enum and attribute to hold which mode application is running on
  enum State {
    SelectMode,
    ModeServer,
    ModeClient
  }
  State _state;

  void Awake() {

    // Set Bolt to use Photon as transport layer
    // this will connect to Photon using config values from Bolt's settings window
    BoltLauncher.SetUdpPlatform(new PhotonPlatform());

    // Optionally, you may want to config the Photon transport layer programatically:

    //		BoltLauncher.SetUdpPlatform (new PhotonPlatform (new PhotonPlatformConfig {
    //			AppId = "67d57778-0998-44f2-8c74-8f9544bda46e",
    //			RegionMaster = "usw"
    //		}));
  }

  public override void BoltStartDone() {
    BoltNetwork.RegisterTokenClass<RoomProtocolToken>();
    BoltNetwork.RegisterTokenClass<OnlineIndexToken>();
  }

  void OnGUI() {
    switch (_state) {
      // starting Bolt is the same regardless of the transport layer
      case State.SelectMode:
        if (GUILayout.Button("Start Client")) {
          BoltLauncher.StartClient();
          _state = State.ModeClient;
        }

        if (GUILayout.Button("Start Server")) {
          BoltLauncher.StartServer();
          _state = State.ModeServer;
        }

        break;

      // Publishing a session into the matchmaking server
      case State.ModeServer:
        if (BoltNetwork.isRunning && BoltNetwork.isServer) {
          if (GUILayout.Button("Publish HostInfo And Load Map")) {
            BoltNetwork.SetHostInfo("MyPhotonGame", new RoomProtocolToken { ArbitraryData = "(MyCustomData)" });
            BoltNetwork.LoadScene("NetworkedPhysicsTest");
          }
        }
        break;
      // for the client, after Bolt is innitialized, we should see the list
      // of available sessions and join one of them
      case State.ModeClient:

        if (BoltNetwork.isRunning && BoltNetwork.isClient) {
          GUILayout.Label("Session List");

          foreach (var session in BoltNetwork.SessionList) {
            var token = session.Value.GetProtocolToken() as RoomProtocolToken;
            if (GUILayout.Button(session.Value.Source + " / " + session.Value.HostName + " (" + session.Value.Id + ")" + (token != null ? token.ArbitraryData : ""))) {
              BoltNetwork.Connect(session.Value);
            }
          }
        }
        break;
    }

  }
}
public class OnlineIndexToken:Bolt.IProtocolToken {
    public int onlineIndex;

    public void Read(UdpPacket packet) {
        onlineIndex = packet.ReadInt();
    }
    public void Write(UdpPacket packet) {
        packet.WriteInt(onlineIndex);
    }
}