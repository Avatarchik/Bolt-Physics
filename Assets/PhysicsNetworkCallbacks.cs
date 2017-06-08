using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[BoltGlobalBehaviour]
public class PhysicsNetworkCallbacks : Bolt.GlobalEventListener {


    public override void SceneLoadLocalDone(string map) {
        base.SceneLoadLocalDone(map);
        DLog.Log("Scene Load Local Done");
        BoltConsole.Write("SceneLoadLocalDone");
        //create bubble controller on every connection
        if(BoltNetwork.isServer) {
            BoltNetwork.Instantiate(BoltPrefabs.NetworkedBubbleController, new OnlineIndexToken() { onlineIndex = 0 });
            totalPlayers++;
        }
    }

    public static int totalPlayers =0;

    public override void SceneLoadRemoteDone(BoltConnection connection) {
        BoltConsole.Write("SceneLoadRemoteDone");
        base.SceneLoadRemoteDone(connection);
        //scene is loaded on the client, send him an event to create a test player with a specific onlineIndex
        if(BoltNetwork.isServer) {
            CreatePlayerEvent e = CreatePlayerEvent.Create(connection);
            e.onlineIndex = totalPlayers;
            totalPlayers++;
            e.Send();
        }
    }

    public override void OnEvent(CreatePlayerEvent evnt) {
        base.OnEvent(evnt);
        //rec

        BoltNetwork.Instantiate(BoltPrefabs.NetworkedBubbleController, new OnlineIndexToken() { onlineIndex = evnt.onlineIndex });
    }

    //public override void OnEvent(CreatePlayerEvent evnt) {
    //    base.OnEvent(evnt);
    //}

    public override void OnEvent(RewindableState evnt) {
        base.OnEvent(evnt);
        //NetworkedBubbleControllerBehaviour.players.ElementAt(evnt.id).Value.ApplyState(evnt);
    }

    //these are inputs from players.  We need to store these in a list somewhere to we can validate the state
    public override void OnEvent(PhysicsInputCommand evnt) {
        base.OnEvent(evnt);
        PhysicsManager.instance.ReceiveInput(evnt);
    }

    public override void OnEvent(RigidbodyDataEvent evnt) {
        base.OnEvent(evnt);
        //everyone should get this
        if(evnt.entity != null) {
            evnt.entity.GetComponent<PhysicsRewindData>().ReceiveValidation(evnt);
        }
    }

}
