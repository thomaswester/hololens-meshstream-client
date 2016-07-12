using UnityEngine;
using System.Collections;

public class TextMeshUpdate : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}

    void Update()
    {
        Vector3 v = Camera.main.transform.position - transform.position;
        v.x = v.z = 0.0f;

        transform.LookAt(Camera.main.transform.position - v);
        transform.rotation = new Quaternion(Camera.main.transform.rotation.x, 0, 0, Camera.main.transform.rotation.w); // Take care about camera rotation
       
    }
}
