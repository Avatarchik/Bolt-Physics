using UnityEngine;
using System.Collections;

[BoltGlobalBehaviour(BoltNetworkModes.Host, "clickToMoveServerAuth")]
public class clickToMoveCallbacks : Bolt.GlobalEventListener
{

    public override void SceneLoadLocalDone(string map)
    {
        BoltEntity player = BoltNetwork.Instantiate(BoltPrefabs.clickToMovePlayer);
        player.TakeControl();
    }


    public override void SceneLoadRemoteDone(BoltConnection connection)
    {
        BoltEntity player = BoltNetwork.Instantiate(BoltPrefabs.clickToMovePlayer);
        player.AssignControl(connection);
    }
}
