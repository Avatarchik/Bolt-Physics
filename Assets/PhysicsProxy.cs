using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicsProxy : MonoBehaviour {

    //we don't want to have renderables directly on physics objects, because we don't want to use their pos/rot explictly 
    //instead we use this as a proxy and smooth to the physics location

    public float posLerp = 0.9f; //could use adaptive values here depending on *something*, ping?
    public float rotLerp = 0.9f;

    public Rigidbody rigidbodyTarget;
    
	void Start () {
		
	}
	
	void LateUpdate () {
		if(rigidbodyTarget != null) {
            this.transform.position = Vector3.Lerp(this.transform.position, rigidbodyTarget.transform.position, posLerp);
            this.transform.rotation = Quaternion.Slerp(this.transform.rotation, rigidbodyTarget.transform.rotation, rotLerp);
        }
    }
}
