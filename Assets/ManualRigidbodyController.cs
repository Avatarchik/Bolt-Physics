using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManualRigidbodyController : MonoBehaviour {

    // Use this for initialization
    private Rigidbody rb;

    private void Awake() {
        
    }

    void Start () {
        rb = this.GetComponent<Rigidbody>();
        PhysicsManager.instance.AddBody(rb);
    }
	
	void Update () {
		
	}

    private void OnDestroy() {
        PhysicsManager.instance.RemoveBody(rb);
    }
}
