using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour {

    public GameObject prefab;
    public float timer = 0f;
    public float spawnTimer = 4f;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        timer += Time.deltaTime;
        if(timer >= spawnTimer) {
            timer = 0f;
            GameObject.Instantiate(prefab, this.transform.position, Quaternion.identity);
        }
	}
}
