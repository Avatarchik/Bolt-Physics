using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PhysicsRewindData : MonoBehaviour {

    //store this as frames, because that's how bolt does it?
    public SortedDictionary<int, RigidbodyData> data = new SortedDictionary<int, RigidbodyData>(); //SortedDict so we can get data.Keys.First() to pop the oldest entry out, while still indexing by frame for quick access
    public int storeEveryFrame = 1; //1 - store every frame, 2 - store every second frame, etc..
    private int storeEveryFrameCounter = 0;

    public int dataCount = 0;
    public Vector2 dataRange = new Vector2();
    public int validatedStatesCount = 0;

    /// <summary>
    /// -1 = no max
    /// </summary>
    public int maxStatesStored = -1;

    private Rigidbody r;

    //we can't be sure all physics will have the same validation frame.  In my case we can... because we always send all validations.
    //but if we want to scale this to lots of physics instead of 4, we need a way to resim from the newest frame at all physics share.
    //or when we start from the oldest and every step check to see if each physics has a newer validated, and just snap to it
    //that way when we run out of validated states and need to sim that body will start where he was last validated?
    //public Dictionary<int, RigidbodyData> validatedStates = new Dictionary<int, RigidbodyData>();
    //public KeyValuePair<int, RigidbodyData> validatedState = new KeyValuePair<int, RigidbodyData>(-1, null);//we only need the latest validated state
    public int validatedStateFrame;
    public RigidbodyData validatedStateData;

    public BoltEntity entity;

    public int currentFramePosition = 0;

    public PhysicsProxy proxy;

	void Start () {
        r = this.GetComponent<Rigidbody>();
        PhysicsManager.instance.rewindables.Add(this);
        entity = this.GetComponent<BoltEntity>();

        if(proxy != null) proxy.transform.parent = null;
	}
	/// <summary>
    /// FixedUpdate is called before the interal physics calls
    /// </summary>
	void FixedUpdate () {
        //so we can see values in the inspector
        //dataCount = data.Count;
        //if(dataCount > 0) {
        //    dataRange.x = data.Keys.First();
        //    dataRange.y = data.Keys.Last();
        //}
        //validatedStatesCount = validatedStates.Count;
        //--
        //call store states through the PhysicsManager instead
	}

    public void StoreRigidbodyData(int frame) {
        if(data.ContainsKey(frame)) {
            data[frame] = (new RigidbodyData() { frame = frame, position = r.position, rotation = r.rotation, velocity = r.velocity, angularVelocity = r.angularVelocity });
        } else {
            data.Add(frame, new RigidbodyData() { frame = frame, position = r.position, rotation = r.rotation, velocity = r.velocity, angularVelocity = r.angularVelocity });
        }
    }

   

    public void RewindTo(int frame) {
        //find the closest frame 
        //check if first frame is > targetFrame (we are outside of the range)
        //check if last frame is < targetFrame (we are outside of the range)
        ///otherwise it's somewhere in there, need to find it
        //
        //DLog.Log("PRD::RewindTo(" + frame + ")");
        //what if we have no frames!
        if(data.Count == 0) return;
        int first = data.Keys.First();
        int last = data.Keys.Last();
        //DLog.Log(string.Format("first {0} - last {1}", first, last));
        int f = first;
        if(frame < first) {
            f = first; //don't have a frame earlier, so use the earliest
        } else if(frame > last) {
            f = last; //don't have a frame later, so use the last
        } else {
            f = frame; //it should be here
        }

        SetFromState(data[f]);
    }

    public void SetFromState(RigidbodyData data) {
        //DLog.Log("setting from state1: frame: " + data.frame + " - pos: " + data.position);
        
        r.velocity = data.velocity;
        r.position = data.position;
        r.rotation = data.rotation;
        r.angularVelocity = data.angularVelocity;

        this.transform.position = data.position;
        this.transform.rotation = data.rotation;
    }

    /// <summary>
    /// we send our current positions (as we just did a resim) along with the frame we resimed to, to everyone so they can
    /// use it in their local sims as a best guess starting point
    /// </summary>
    /// <param name="frame"></param>
    public void BroadcastValidation(int frame) {
        //could probably do entity events
        //lazy
        if(entity.isAttached) {
            RigidbodyDataEvent e = RigidbodyDataEvent.Create();
            e.entity = entity;
            e.position = this.transform.position;
            e.rotation = this.transform.rotation;
            e.velocity = GetRigidbody().velocity;
            e.angularVelocity = GetRigidbody().angularVelocity;
            e.frame = frame;
            e.Send();
            DLog.Log("BroadcastValidation: " + this.gameObject.name);
        }
    }

    public Rigidbody GetRigidbody() {
        if(r == null) {
            r = this.GetComponent<Rigidbody>();
        }
        return r;
    }

    /// <summary>
    /// Server sends this once this frame has been simulated, along with inputs.  This is the truth at this frame with all inputs applied up to this point
    /// Use this as a base and simulate forward to current time to get local predictions
    /// </summary>
    /// <param name="e"></param>
    public void ReceiveValidation(RigidbodyDataEvent e) {

        PhysicsManager.instance.t1.text = "";
        DLog.Log("ReceivedValidationEvent for frame: " + e.frame + " current frame: " + BoltNetwork.serverFrame);
        PhysicsManager.instance.t1.text = "Validation for frame: " + e.frame + " - currentFrame: " + BoltNetwork.serverFrame;
        if(BoltNetwork.isServer) return; //we don't need to validate server stuff, so we can skip all this jumk
        if(e.frame > BoltNetwork.serverFrame) {
            //this can happen because BoltNetwork.serverFrame isn't exact on clients.
            //was happening with 1000ms pings
            DLog.Log("ReceivedValidationEvent for a frame that hasn't been simulated locally yet??");
        }

        //we can go through and discard any state date older than this validation
       

        validatedStateFrame = e.frame;
        validatedStateData = new RigidbodyData() {
            frame = e.frame,
            position = e.position,
            rotation = e.rotation,
            velocity = e.velocity,
            angularVelocity = e.angularVelocity
        };
        //probably a better way to do this. Lazy
        SortedDictionary<int, RigidbodyData> temp = new SortedDictionary<int, RigidbodyData>();
        foreach(KeyValuePair<int, RigidbodyData> d in data) {
            if(d.Value.frame >= e.frame) {
                temp.Add(d.Key, d.Value);
            }
        }
        data = temp;

        NetworkedBubbleControllerBehaviour c = this.GetComponent<NetworkedBubbleControllerBehaviour>(); //this should be generalized ControllableRigidbody
        if(c != null) {
            c.CleanupOldInputs(e.frame);
        }

        ////when we receive a validation, check that against our local - stored values to see if our past frame was close enough to it.
        ////if we are not, we need to resync and resim.

        ////turning this on here just applies the states late as we get them, without any simulation
        //SetFromState(validatedStates[e.frame]);

        //do we even have a local frame data for this frame?
        bool hasLocalData = data.ContainsKey(e.frame);
        if(hasLocalData) {
            //is localData CLOSE to validatedData?
            if(StatesMatch(data[e.frame], validatedStateData)) {
                //don't do anythign, we're mostly accurate ?
                //if we ARE close though, could we not just snap anyways because
                //1) We're close, so it won't be an abrupt snap
                //2) It will make the next step be more likely to be similar
                //like fixing the small drifts rather than fixing the big ones every once in a while?
                //DLog.Log("PRD::RecValidation - Close enough!");
                PhysicsManager.instance.NeedsLocalResim(e.frame);
            } else {
                //too far apart, fix this.
                DLog.Log("PRD::RecValidation - states too far apart, marking for resim");
                PhysicsManager.instance.NeedsLocalResim(e.frame);
            }
        } else {
            DLog.Log("PRD::RecValidation - don't have frame data, marking for resim");
            //don't even have data, so we need to resim anyways.
            PhysicsManager.instance.NeedsLocalResim(e.frame);
            
        }
    }

    public bool StatesMatch(RigidbodyData a, RigidbodyData b) {
        //just do a position distance check for now..
        float distanceThreshold = 1f;
        float dt = Vector3.Distance(a.position, b.position);
        DLog.Log("--- StatesMatch:: " + (dt <= distanceThreshold));
        DLog.Log("local: " + a.position.ToString());
        DLog.Log("validated: " + b.position.ToString());
        if(dt <= distanceThreshold) {
            return true;
        } else {
            return false;
        }
    }

    public void OnDestroy() {
        PhysicsManager.instance.rewindables.Remove(this);
    }
}

[System.Serializable]
public class RigidbodyData {
    public int frame;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}
