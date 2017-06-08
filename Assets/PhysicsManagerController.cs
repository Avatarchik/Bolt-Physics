using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PhysicsManagerController : MonoBehaviour {

    public bool autoSim = false;

	void Start () {
		
	}

    void Update() {
        //testing to make sure rewind/resim works properly in the same frame and isn't one frame off or something
        int stepSize = 100;
        if(Input.GetKeyDown(KeyCode.A)) {
            //rewind
            DLog.Log("Stepping back 100 frames");
            int f = Mathf.Max(0, PhysicsManager.instance.currentFrame - stepSize);
            PhysicsManager.instance.RewindPhysics(f);
            PhysicsManager.instance.currentFrame = f;
        } else if(Input.GetKeyDown(KeyCode.D)) {
            DLog.Log("Stepping forward 100 frames");
            for(int i = 0; i < stepSize; i++) {
                PhysicsManager.instance.StoreRewindablesState(PhysicsManager.instance.currentFrame);
                PhysicsManager.instance.StepPhysics(Time.fixedDeltaTime);
                PhysicsManager.instance.currentFrame++;
            }
        } else if(Input.GetKeyDown(KeyCode.S)) {
            DLog.Log("Stepping back then forward 100 frames");

            int f = Mathf.Max(0, PhysicsManager.instance.currentFrame - stepSize);
            PhysicsManager.instance.RewindPhysics(f);
            PhysicsManager.instance.currentFrame = f;

            for(int i = 0; i < stepSize; i++) {
                PhysicsManager.instance.StoreRewindablesState(PhysicsManager.instance.currentFrame);
                PhysicsManager.instance.StepPhysics(Time.fixedDeltaTime);
                PhysicsManager.instance.currentFrame++;
            }
        }
        if(Input.GetKeyDown(KeyCode.Space)) {
            autoSim = !autoSim;
        }

        if(autoSim) {
            PhysicsManager.instance.StoreRewindablesState(PhysicsManager.instance.currentFrame);
            PhysicsManager.instance.StepPhysics(Time.fixedDeltaTime);
            PhysicsManager.instance.currentFrame++;
        }
    }
}
