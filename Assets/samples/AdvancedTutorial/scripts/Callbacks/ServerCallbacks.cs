using System.Linq;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Bolt.AdvancedTutorial {

	[BoltGlobalBehaviour(BoltNetworkModes.Host, "Level1")]
	public class ServerCallbacks : Bolt.GlobalEventListener {
	  public static bool ListenServer = true;

	  void Awake() {
	    if (ListenServer) {
	      Player.CreateServerPlayer();
	      Player.serverPlayer.name = "SERVER";
	    }
	  }

	  void FixedUpdate() {
	    foreach (Player p in Player.allPlayers) {
	      // if we have an entity, it's dead but our spawn frame has passed
	      if (p.entity && p.state.Dead && p.state.respawnFrame <= BoltNetwork.serverFrame) {
	        p.Spawn();
	      }
	    }
	  }

	  public override void ConnectRequest(UdpKit.UdpEndPoint endpoint, Bolt.IProtocolToken token) {
	    BoltNetwork.Accept(endpoint);
	  }

	  public override void Connected(BoltConnection c) {
	    c.UserData = new Player();
	    c.GetPlayer().connection = c;
	    c.GetPlayer().name = "CLIENT:" + c.RemoteEndPoint.Port;

	    c.SetStreamBandwidth(1024 * 1024);
	  }

	  public override void SceneLoadRemoteDone(BoltConnection connection) {
	    connection.GetPlayer().InstantiateEntity();             
	  }

	  public override void SceneLoadLocalDone(string map) {
	    if (Player.serverIsPlaying) {
	      Player.serverPlayer.InstantiateEntity();
	    }
	  }

	  public override void SceneLoadLocalBegin(string map) {
	    foreach (Player p in Player.allPlayers) {
	      p.entity = null;
	    }
	  }
	}
}
