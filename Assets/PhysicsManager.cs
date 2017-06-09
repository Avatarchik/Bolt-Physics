using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PhysicsManager : MonoBehaviour {

    /// <summary>
    /// Everything here (PhysicsManager.fixedUpdate) should happen BEFORE everything elses fixedUpdate.  
    /// just because we need to rewind the sim sometimes, then after all that we will want to apply this frames input, but I guess it probably doesn't matter.  
    /// if we do it after when we sim-forward again everything we be caught up in the end
    /// </summary>

    public static PhysicsManager instance;
    public List<Rigidbody> bodies = new List<Rigidbody>();
    public List<PhysicsRewindData> rewindables = new List<PhysicsRewindData>();

    public PhysicsSimulationMode simulationMode = PhysicsSimulationMode.NoSimulate;

    public float currentSimTime = 0f; //the time the simulation is actually at.  If currentSimeTime < simTime, we need to step
    public float simTime = 0f; //the time the simulation should be at
    public float step = 1 / 50f;
    public float stepTime = 0f;

    public float timer = 0f;

    //keep track of our frame here, and always try and step towards BoltNetwork.serverFrame;
    //currentFrame is incremented AFTER Physics.Step is done.
    public int currentFrame = 0;

    //debug texts so we don't have to clutter up the log with redundant stuff
    public UnityEngine.UI.Text text;
    public UnityEngine.UI.Text t1;
    public UnityEngine.UI.Text t2;
    public UnityEngine.UI.Text t3;


    public int waitForPlayerInputs = 4;
    public Dictionary<int, List<PhysicsInputState>> playerInputs = new Dictionary<int, List<PhysicsInputState>>(); //dict, key - frame num, value - PhysicsInputState
    /// <summary>
    /// Notes:
    /// -- REQUIREMENTS --
    /// 1) Physics need to stay in sync across all clients, without relying on the host (for the most part).  
    /// 2) Client-side prediction for player controlled physics objects (moveable spheres) need to be responsive
    /// 3) Knockback needs to not feel weird
    /// 4) Extrapolation - So that clients can "play" forward even though we won't have any inputs for them from lastInputTime -> currentTime because they are still in transit
    /// 
    /// 1 - We could "confirm" states by the server and somehow replicate them back to everyone saying "HEY THE SERVER THINKS THIS IS THE MOst ACCURATE WORLD STATE AT THIS FRAME"
    /// Might be really heavy though...
    /// 
    /// 2 - Client side prediction is easy enough, Just apply forces instantly.
    /// What happens if we just send these values and apply them as the clients get them.  If we start from the same position will they
    /// stay in sync (somewhat) at least? NOPE
    /// 
    /// 3 - knockback is the tricky thing.  We need to have "shared" ownership in order for this to work.  If we try and knockback a physics object
    /// we do not own (can not modify pos of) we must wait until the owner thinks he got hit to apply his own knockback.  This is RTT lag.
    /// Instead we could apply it immediately on the player who rams into him and send an event?  Player hit Player at frame X with force Y?  Or do we assume
    /// both players will feel it "eventually" once the state is replicated to everyone?
    /// 
    /// So, we run physics on everyone.  PhysicsManager handles a list of inputs (events) sorted by frame time.  
    /// when we recieve an (old) input we roll back physics to that time (for everyone), apply those inputs, and then step forward applying any other inputs we've recieved
    /// back to currentTime.  
    /// 
    /// When we roll back is there a way we can roll back to the closest "server validated" state of everything? 
    /// Can we send pre-input positions with input events? so we know "WHERE" someone thinks they are before they apply inputs?
    /// Then if it's not where you think they are we can do some lerping to that spot quickly or something??
    /// 
    /// SYNC
    /// Send PhysicsStateData inputs from the OWNER to the SERVER.  Once the SERVER has received all (4) player inputs from that frame 
    /// we can validate that frame.  (eg, rewind to the last validated state, simulate forward to validation frame, applying any and all inputs as we go)
    /// Once we have that, we can assign stateData (bolt state, so replicated, priority, just not attached to any transforms/callbacks or anything)
    /// This data will act as the "most recent validated state" and be replicated to everyone and used for local-prediction.  What happens if it takes too long for a player input to reach the server?
    /// 
    /// PREDICTION
    /// We can only predict rigidbodies we are controlling (via input), otherwise the non-controlling rigidbodies will just step forward with no input (this is fine?)
    /// We can NOT predict other players rigidbody controllers past just simulating forward from the last valid state.
    /// 
    /// Will the aboce Sync + Prediction work for knockback? It should since we are never explictly setting the state, just simulating physics with our best known guesses, 
    /// and that should mean physics/collsion resolution should work too, and even if they come in late the server should still detect them in the past
    /// 
    /// In the case of a correction that our local prediction was wrong we need to lerp our corrections
    /// Server fr 10, local frame 20
    /// update comes in for fr 11, local frame 21 (A)
    /// rewind to frame 11, apply update, resim forward with local inputs back to frame 21(B)
    /// if frame21(A) is different (largely) from frame21(B) we need to lerp to our new position so we don't get jerky changing
    /// </summary>

    public int lastServerValidatedFrame = 0;
    private bool needsLocalResim = false;
    private int needsResimFrom = 0;

    private void Awake() {
        instance = this;
    }

    void Start () {
        DLog.Log("PhysicsManager Initialized");
        Physics.autoSimulation = false;
        //if(BoltNetwork.isClient) {
        //    simMode = PhysicsSimulationMode.NoSimulate;
        //}
        DLog.LogF("{0}:{1} words {2}", 1, 2, 3);
	}

    public void Update() {
        //currentFrame++;
        waitForPlayerInputs = PhysicsNetworkCallbacks.totalPlayers;
    }

    public void FixedUpdate() {
        //displaying bolt pinga nd frame stuff on our debug ui
        if(BoltNetwork.isRunning) {
            string p = "";
            float pingTime = 0f;
            if(BoltNetwork.isClient) {
                if(BoltNetwork.server != null) {
                    pingTime = BoltNetwork.server.PingNetwork * 1000f;
                    p = "ping: " + pingTime + "ms";
                }
            } else {
                p = "ping: (host)";
            }
            text.text = p + " - serverFrame: " + BoltNetwork.serverFrame;
        }
        //--


        if(needsLocalResim) {
            needsLocalResim = false;
            if(currentFrame > needsResimFrom) { //somehow I got marked for resim from a frame that was < needsResimFrom, which doens't make sense so just chcek...
                //we know we just got a validation state from the server, and we know our local state wasn't close enough to this validation in the past
                //so we need to rewind to that frame, apply the validation state to that frame the best we know (for all objects if we can)
                //and then resimulate forward, every frame making sure our validation is as up to date as possible
                //eg, every step check to see if we have a validation state for that body, and if we do override that body to snap to that state
                //I think?
                needsResimFrom--;
                DLog.Log("PM:NeedLocalResim start", 1);
                DLog.Log("resimFrom: " + needsResimFrom + " - currentFrame: " + currentFrame);
                RewindPhysics(needsResimFrom); //restores us to the latest "best guess" of the server
                int lastF = needsResimFrom;
                while(lastF <= currentFrame) {
                    //this should be adaptive too..so long we don't miss any snapping to validated states or stored inputs?? if we need to resim a lot though..
                    //ApplyPhysicsLocalInputState(lastF);
                    //only want to apply local inputs, not all inputs recieved from everyone
                    //ApplyLocalPhysicsInputState(lastF);

                    //StepPhysics(Time.fixedDeltaTime);
                    //lastF++;
                    DLog.Log("Stepping frame - " + lastF, 1);
                    //StoreRewindablesState(lastF);
                    ApplyLocalPhysicsInputState(lastF);
                    StepPhysics(Time.fixedDeltaTime);
                    lastF++;
                    SyncValidated(lastF); //we broadcast validations AFTER Step and frame increment, so we should sync after that too?
                }
                DLog.Log("PM:NeedLocalResim done", 1);

            }
        }

        PhysicsLoop();
    }

    public void NeedsLocalResim(int fromFrame) {
        needsLocalResim = true;
        needsResimFrom = fromFrame;
    }

    /// <summary>
    /// goes through and sees if we have any validatiosn for this frame
    /// </summary>
    /// <param name="frame"></param>
    public void SyncValidated(int frame) {
        for(int i = 0; i < rewindables.Count; i++) {
            if(rewindables[i] != null) {
                if(rewindables[i].validatedStateFrame == frame) {
                    //we have a validated state for this frame, so snap to it
                    rewindables[i].SetFromState(rewindables[i].validatedStateData);
                }
            }
        }
    }

    /// <summary>
    /// This loop is called automatically (usually) and makes sure we're simulating the physics in real time
    /// this is NEVER used during rewind/resim, we handle that not through the PhysicsLoop (manually, before it)
    /// In most cases, this should only step the physics once per FixedUpdate
    /// </summary>
    public void PhysicsLoop(bool pollInputs = true) {
        DLog.Log("PM::PhysicsLoop - start");
        if(simulationMode == PhysicsSimulationMode.AutoSimulate) {
            Physics.autoSimulation = true;
        } else if(simulationMode == PhysicsSimulationMode.ManualSimulate) {

            Physics.autoSimulation = false;
            int startFrame = currentFrame;
            int deltaSteps = BoltNetwork.serverFrame - currentFrame;
            //need adapative stepsize so in the case that we're 10000 steps behind we can resim pretty quickly
            //to get a somewhat accurate state?
            //figure out how to break this into multiple larger steps without going over serverFrame


            //doing a quick catchup if we're REALLY far behind
            //we can't simulate more than like 100 frames per frame in the best case.
            //in my usecase we can just freeze everything when we're really far behind or just jump to the latest frame without
            if(false) { //don't snap
                int timestepScale = 200;
                if(deltaSteps > timestepScale) {
                    DLog.Log("PM::PhysicsLoop - simulating frame catchup: numSteps: " + timestepScale);
                    StoreRewindablesState(currentFrame);
                    if(pollInputs) PollPhysicsInputs(currentFrame); //here to ensure we got the input for this frame before simulating (instead of putting it in another components fixedupdate and messing with execution order junk)
                    StepPhysics(Time.fixedDeltaTime * timestepScale);
                    currentFrame += timestepScale; ;
                    return;
                }
                timestepScale = 100;
                if(deltaSteps > timestepScale) {
                    DLog.Log("PM::PhysicsLoop - simulating frame catchup: numSteps: " + timestepScale);
                    StoreRewindablesState(currentFrame);
                    if(pollInputs) PollPhysicsInputs(currentFrame); //here to ensure we got the input for this frame before simulating (instead of putting it in another components fixedupdate and messing with execution order junk)
                    StepPhysics(Time.fixedDeltaTime * timestepScale);
                    currentFrame += timestepScale; ;
                    return;
                }
            } else {
                //snap
                DLog.Log("PM::PhysicsLoop - Snapping to the current frame and not simulating up to it because we're {" + deltaSteps + "} frames behind");
                //and that's too many frames to simulate forward
                int snapThreshold = 500;
                if(deltaSteps > snapThreshold) {
                    currentFrame = BoltNetwork.serverFrame;
                }
            }
            
            while(currentFrame < BoltNetwork.serverFrame) {
                DLog.Log("PM::PhysicsLoop - simulating frame: " + currentFrame);
                StoreRewindablesState(currentFrame);
                if(pollInputs) PollPhysicsInputs(currentFrame); //here to ensure we got the input for this frame before simulating (instead of putting it in another components fixedupdate and messing with execution order junk)
                StepPhysics(Time.fixedDeltaTime);
                currentFrame++; 
            }
            int endFrame = currentFrame;
            t1.text = "stepped [" + startFrame + "->" + endFrame + "]";
        } else {
            //nosim
            Physics.autoSimulation = false;
        }
        DLog.Log("PM::PhysicsLoop - done");
    }

    /// <summary>
    /// Just calls Physics.Simulate with specified timestep, use Time.FixedDeltaTime for one frame
    /// </summary>
    /// <param name="timeStep"></param>
    public void StepPhysics(float timeStep) {
        //DLog.Log("PhysicsManager::StepPhysics - Stepping physics foward manually: " + currentFrame);
        Physics.Simulate(timeStep);
    }

    public void StoreRewindablesState(int frame) {
        //we want to overwrite any state data here as this is used in the validation simulate step
        for(int i = 0; i < rewindables.Count; i++) {
            rewindables[i].StoreRigidbodyData(frame);
        }
    }

    public void PollPhysicsInputs(int frame) {
        DLog.Log("PM::PollPhysicsInputs(" + frame + ")");
        for(int i = 0; i < rewindables.Count; i++) {
            rewindables[i].GetComponent<NetworkedBubbleControllerBehaviour>().PollPhysicsInputs(frame); // <--- BAD!!!!!!!!!!!!! But ok for now ᕕ( ᐛ )ᕗ
        }
    }

    //public void PollPhysicsInputs(int frame) {
    //    for(int i = 0; i < rewindables.Count; i++) {
    //        rewindables[i].PollPhysicsInputs(frame);
    //    }
    //}

   /// <summary>
   /// Restores physics to the simulated state at toFrame (locally), this is not validated by anyone. 
   /// This frame has NOT been simulated.  This is the state we were at before sim was called for this frame
   /// This also doesn't change any simTime, simFrame or any values.  So if you want to rewind in time don't use this
   /// </summary>
   /// <param name="toFrame"></param>
    public void RewindPhysics(int toFrame) {
        DLog.Log("PM::RewindPhysics(" + toFrame + ") - currentFrame: " + currentFrame, 1);
        if(toFrame > currentFrame) Debug.LogError(string.Format("Can not rewind to a frame in the future! cur{0} -> to{1}",currentFrame, toFrame));
        for(int i = 0; i < rewindables.Count; i++) {
            rewindables[i].RewindTo(toFrame);
        }
    }

    public void AddBody(Rigidbody r) {
        bodies.Add(r);
    }
    
    public void RemoveBody(Rigidbody r) {
        bodies.Remove(r);
    }

    //only the server should ever get this..
    //this should happen before fixedupdate was called for this frame.
    public void ReceiveInput(PhysicsInputCommand e) {
        
        //keep track of ALL inputs from ALL players (for rigidbody players)
        //we need this so we can apply their inputs and simulate that frame to validate it
        if(!playerInputs.ContainsKey(e.frame)) {
            playerInputs.Add(e.frame, new List<PhysicsInputState>());
        }
        playerInputs[e.frame].Add(new PhysicsInputState() { onlineIndex = e.onlineIndex, inputDir = e.inputDir, dash = e.dash, frame = e.frame });

        
        if(playerInputs[e.frame] != null) {
            if(playerInputs[e.frame].Count == waitForPlayerInputs) { //we dynamically increase waitForPlayerInputs whenever a player connects.  
                                                                    //If we do not and run the server physics step it never gets validated before
                                                                    //they connect, which means if they take 10,000 frames to connect,
                                                                    //validate tries to validate (and step) [0->10,000] which causes hangs.

                //we have all the inputs for this frame, validate it now!
                DLog.Log("Received all inputs for frame: " + e.frame);
                t2.text = "Rec all inputs for frame " + e.frame;
                ValidatePhysics(e.frame);
            }
        }
    }

    /// <summary>
    /// We include [frame] and it's inputs when we simulate forward
    /// </summary>
    /// <param name="frame"></param>
    public void ValidatePhysics(int frame) {
        t2.text = ("Validating Physics up to frame [" + frame + "]");
        DLog.Log("Validating Physics up to frame [" + frame + "]");
        //--rewind to lastValid
        RewindPhysics(lastServerValidatedFrame);
        currentFrame = lastServerValidatedFrame;
        //--simulate forward, applying all player inputs (which should all be full)
        //int lastFrame = lastServerValidatedFrame;
        while(currentFrame < frame) {
            //this should be adaptive.  We don't want to simulate 100000 steps here if we're really behind, so do a max of 100 or something
            DLog.Log("ValidatePhysics Stepping frame - " + currentFrame);
            StoreRewindablesState(currentFrame);
            ApplyPhysicsInputState(currentFrame);
            StepPhysics(Time.fixedDeltaTime);
            currentFrame++;
        }

        DLog.Log("Validated up to frame: " + frame);

        //clean up any old stored inputs (before currentFrame), we don't need them anymore, and don't want
        //the list to build up too big when simulation runs for a while
        List<int> keysToDestroy = new List<int>();
        foreach(KeyValuePair<int, List<PhysicsInputState>> k in playerInputs) {
            if(k.Key < currentFrame) {
                keysToDestroy.Add(k.Key);
            }
        }

        for(int i = 0; i < keysToDestroy.Count; i++) {
            playerInputs.Remove(keysToDestroy[i]);
        }

        BroadcastValidation(currentFrame);
        ResimToCurrent();
        //need to resim to current time now

        //sanity check.
        #region
        //ValidatePhysics(0), we just got all inputs for frame 0.
        //RewindPhysics to frame 0. Restores the state to pre-sim frame 0.
        //lastServerValidatedFrame = 0 (this is our first validation)
        //while(lastFrame <= frame) 0 <= 0
        //Apply inputs for frame (0)
        //StepPhysics one step
        //lastFrame++; we are now at the BEGINING of frame 1.
        //Broadcast.  lastServerValidatedFrame = 1.  This name is confusing.  we've validated up to the beinning of frame 1

        //ValidatePhysics(1), we just got all inputs for frame 1.
        //RewindPhysics to lastServerValidatedFrame, 1.
        //lastFrame = 1
        //while(1 <= 1){
        //ApplyStateInputs for frame (1)
        //StepPhysics once
        //lastFrame++  (2)
        //broadcast.  We're validated up to the beginning of frame 2

        //that makes sense to me. (seems to be working in the tests too)
        #endregion
        //need to resim to current time?
    }

    /// <summary>
    /// use this after a rewindToLastValidated -> simTONewValid -> ResimToCurrent.
    /// This is bringing us back to the current time
    /// </summary>
    public void ResimToCurrent() {
        while(currentFrame < BoltNetwork.serverFrame) {
            DLog.Log("PM::ResimToCurrent - simulating frame: " + currentFrame);
            StoreRewindablesState(currentFrame);
            ApplyLocalPhysicsInputState(currentFrame);
            //if(pollInputs) PollPhysicsInputs(currentFrame); //here to ensure we got the input for this frame before simulating (instead of putting it in another components fixedupdate and messing with execution order junk)
            StepPhysics(Time.fixedDeltaTime);
            currentFrame++;
        }
    }

    /// <summary>
    /// this applies all inputs recieved by the server. 
    /// </summary>
    /// <param name="frame"></param>
    public void ApplyPhysicsInputState(int frame) {
        if(playerInputs.ContainsKey(frame)){
            for(int i = 0; i < playerInputs[frame].Count; i++) {
                PhysicsInputState s = playerInputs[frame][i];
                NetworkedBubbleControllerBehaviour.players[s.onlineIndex].ApplyServerInput(s);
            }
        } else {
            //Debug.LogError("Cant apply physics input state because this frame {" + frame + "} has no data!!");
            //this happens because we don't start keeping track of inputs input a few second after the server starts,
            //so we never have inputs for serverFrame = 0,  safe to just ignore.
        }
    }

    /// <summary>
    /// this applies all local inputs.  Used when resimulating the local physics only
    /// </summary>
    /// <param name="frame"></param>
    public void ApplyLocalPhysicsInputState(int frame) {
        for(int i = 0; i < NetworkedBubbleControllerBehaviour.players.Count; i++) {
            if(NetworkedBubbleControllerBehaviour.players[i] != null) {
                NetworkedBubbleControllerBehaviour.players[i].ApplyLocalInput(frame);
            }
        }
    }

    /// <summary>
    /// we need to send out events to all physics involved (based on priority or something if we want this to scale)
    /// to tell it the server validated it's position.  These are just events that say 
    /// HEY AT FRAME# THE SERVER HAS SIMULATED ME (and all players inputs) AND SAYS I SHOULD BE HERE
    /// 
    /// frame has NOT been simulated/validated.  frame-1 has though.  
    /// This is the frame number we return to, apply inputs, and then simulate forward
    /// </summary>
    public void BroadcastValidation(int frame) {
        //we probably don't need to broadcast every validated frame... that means 60 validation frames per second
        //could do something on a timer here
        lastServerValidatedFrame = frame;
        DLog.Log("PM::BroadcastValidation:: " + frame);
        //for now we just need to send events to each physics controller
        for(int i = 0; i < NetworkedBubbleControllerBehaviour.players.Count; i++) {
            if(NetworkedBubbleControllerBehaviour.players[i] != null) {
                PhysicsRewindData r = NetworkedBubbleControllerBehaviour.players[i].GetComponent<PhysicsRewindData>();
                if(r != null) {
                    r.BroadcastValidation(frame);
                }
            }
        }
    }
}
[System.Serializable]
public class PhysicsInputState {
    public int onlineIndex;
    public Vector2 inputDir;
    public bool dash;
    public int frame;
}

public enum PhysicsSimulationMode {
    AutoSimulate = 0, //Let Unity handle physics internally
    ManualSimulate = 1, //Disable internal physics, and let PhysicsManager handle stepping manually 
    NoSimulate = 2 //physics is never stepped (unless from somewhere outside this class)
}
