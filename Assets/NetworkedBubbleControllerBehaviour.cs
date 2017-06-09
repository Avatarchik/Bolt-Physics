using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class NetworkedBubbleControllerBehaviour : Bolt.EntityBehaviour<INetworkedBubbleController> {

    public int onlineIndex = -1;

    public Vector2 inputVector;
    public bool dash = false;

    public Rigidbody r;
    public float moveSpeed = 10f;
    
    //static hack because we need to reproduce our normal projects setup but I'm lazy
    public static Dictionary<int, NetworkedBubbleControllerBehaviour> players = new Dictionary<int, NetworkedBubbleControllerBehaviour>();
    public bool receivedFirstState = false;

    public Dictionary<int, PhysicsInputState> localInputs = new Dictionary<int, PhysicsInputState>(); //this needs to be cleaned

    public override void Attached() {
        base.Attached();
        onlineIndex = (entity.attachToken as OnlineIndexToken).onlineIndex;
        r = this.GetComponent<Rigidbody>();
        players.Add(onlineIndex, this);

        float alpha = 0.4f;
        switch(onlineIndex) {
            case 0: this.GetComponent<Renderer>().material.color = new Color(1f, 0f, 0f, alpha); break;
            case 1: this.GetComponent<Renderer>().material.color = new Color(0f, 1f, 0f, alpha); break;
            case 2: this.GetComponent<Renderer>().material.color = new Color(0f, 0f, 1f, alpha); break;
            case 3: this.GetComponent<Renderer>().material.color = new Color(0f, 1f, 1f, alpha); break;
            case 4: this.GetComponent<Renderer>().material.color = new Color(1f, 0f, 1f, alpha); break;
            case 5: this.GetComponent<Renderer>().material.color = new Color(1f, 1f, 0f, alpha); break;
        }

        Renderer proxym = this.GetComponent<PhysicsRewindData>().proxy.GetComponent<Renderer>();
        alpha = 1f;
        switch(onlineIndex) {
            case 0: proxym.material.color = new Color(1f, 0f, 0f, alpha); break;
            case 1: proxym.material.color = new Color(0f, 1f, 0f, alpha); break;
            case 2: proxym.material.color = new Color(0f, 0f, 1f, alpha); break;
            case 3: proxym.material.color = new Color(0f, 1f, 1f, alpha); break;
            case 4: proxym.material.color = new Color(1f, 0f, 1f, alpha); break;
            case 5: proxym.material.color = new Color(1f, 1f, 0f, alpha); break;
        }

        PhysicsProxy p = this.GetComponent<PhysicsRewindData>().proxy;
        if(!entity.isOwner) {
            p.posLerp = 0.1f;
            p.rotLerp = 0.1f;
        }
    }

    //this needs to be called before this frame is simulated?
    public void FixedUpdate() {
        #region Moved to PollPhysicsInputs, which is called through PhysicsManager directly so we have better control
        //DLog.Log("NBCB::FixedUpdate - onlineIndex: " + onlineIndex);
        //if(!entity.isOwner) return; //only want to grab and send inputs if we're the owner of this object.
        //Vector2 v = new Vector2();

        //if(Input.GetKey(KeyCode.W)) {
        //    v.y = -1f;
        //} else if(Input.GetKey(KeyCode.S)) {
        //    v.y = 1f;
        //}

        //if(Input.GetKey(KeyCode.A)) {
        //    v.x = 1f;
        //} else if(Input.GetKey(KeyCode.D)) {
        //    v.x = -1f;
        //}

        //PhysicsInputCommand s = PhysicsInputCommand.Create(Bolt.GlobalTargets.OnlyServer);
        //s.onlineIndex = this.onlineIndex;
        //s.frame = BoltNetwork.serverFrame;
        //s.inputDir = v;
        //s.dash = false;
        //s.Send(); //this is received and stored on PhysicsManager.instance.playerInputs[frame][player].  Also store a local list of this bodies inputs here too

        //StoreLocalInput(s);
        //ApplyLocalInput(s);
        //DLog.Log("NBCB::FIxedUpdate done");
        ////we have an InputCommand for this frame (which should not have been simulated)

        ////if(BoltNetwork.isServer) ApplyLocalInput(s); //doing this here causes input to be trigered twice?  It's called again from the cache when we simulate forward
        ////StoreLocalInput(s);
        ////send that only to the server, but also apply it here locally (and store it so we can use it in the case of rollbacks)
        #endregion


    }

    /// <summary>
    /// Checks for any inputs, packs them into an event and sends them to the server (PhysicsManager) to store
    /// also stores them locally on this objects localInputs
    /// </summary>
    /// <param name="frame"></param>
    public void PollPhysicsInputs(int frame) {
        if(!entity.isOwner) return; //only want to grab and send inputs if we're the owner of this object.
        Vector2 v = new Vector2();

        if(Input.GetKey(KeyCode.W)) {
            v.y = -1f;
        } else if(Input.GetKey(KeyCode.S)) {
            v.y = 1f;
        }

        if(Input.GetKey(KeyCode.A)) {
            v.x = 1f;
        } else if(Input.GetKey(KeyCode.D)) {
            v.x = -1f;
        }

        PhysicsInputCommand s = PhysicsInputCommand.Create(Bolt.GlobalTargets.OnlyServer);
        s.onlineIndex = this.onlineIndex;
        s.frame = PhysicsManager.instance.currentFrame;
        s.inputDir = v;
        s.dash = false;
        s.Send(); //this is received and stored on PhysicsManager.instance.playerInputs[frame][player].  Also store a local list of this bodies inputs here too

        StoreLocalInput(s); //stores it to the localInputs list
        ApplyLocalInput(s); //adds force
    }

    public void ApplyLocalInput(PhysicsInputCommand e) {
        //here we apply commands as we generate them, while they're in trasit to the server for validation.
        //doing this gives us LOCAL PREDICTION 
        DLog.Log(string.Format("Applying local input {1} - frame: {0}", e.frame, e.onlineIndex));
        Vector2 input = e.inputDir;
        r.AddForce(new Vector3(input.x, 0f, input.y) * moveSpeed);
    }

    public void ApplyLocalInput(int frame) {
        if(!localInputs.ContainsKey(frame)) return;
        PhysicsInputState e = localInputs[frame];
        DLog.Log(string.Format("Applying local input {1}- frame: {0}", e.frame, e.onlineIndex));
        Vector2 input = e.inputDir;
        r.AddForce(new Vector3(input.x, 0f, input.y) * moveSpeed);
    }

    public void ApplyLocalInput(PhysicsInputState e) {
        DLog.Log(string.Format("Applying local input {1}- frame: {0}", e.frame, e.onlineIndex));
        Vector2 input = e.inputDir;
        r.AddForce(new Vector3(input.x, 0f, input.y) * moveSpeed);
    }

    public void ApplyServerInput(PhysicsInputState e) {
        DLog.Log(string.Format("ApplyingServerInput for player {1} - frame: {0}", e.frame, e.onlineIndex));
        Vector2 input = e.inputDir;
        r.AddForce(new Vector3(input.x, 0f, input.y) * moveSpeed);
    }

    public void StoreLocalInput(PhysicsInputCommand e) {
        //here we store commands that get sent to the server for validation.  These are our inputs, and can not be wrong.
        //We need these if we do a rewind, to re-apply them when we sim forward
        if(localInputs.ContainsKey(e.frame)) {
            //this key already exists
            DLog.Log("Key already exists: " + e.frame);
        } else {
            localInputs.Add(e.frame, new PhysicsInputState() { onlineIndex = e.onlineIndex, inputDir = e.inputDir, dash = e.dash, frame = e.frame });
        }
    }

    //lazyyyyyy
    public void CleanupOldInputs(int frame) {
        Dictionary<int, PhysicsInputState> temp = new Dictionary<int, PhysicsInputState>();
        foreach(KeyValuePair<int, PhysicsInputState> d in localInputs) {
            if(d.Value.frame >= frame) {
                temp.Add(d.Key, d.Value);
            }
        }
        localInputs = temp;
    }
}
