using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComponentAttachmentPoint : MonoBehaviour {

    public ComponentObject rootObject;

    public Vector3 location { get; private set; }

    public Vector3 normalDirection { get; private set; }

    public float attachmentSize;

    public ComponentAttachmentPoint attachedTo;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
