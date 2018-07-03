using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ComponentAttachmentPoint : MonoBehaviour {

    public Component rootComponent;

    public ComponentObject rootObject;

    public Vector3 location { get; private set; }

    public Vector3 normalDirection { get; private set; }

    public float attachmentSize;

    public ComponentAttachmentPoint attachedTo;


    public void SetLocation (Vector3 _loc) {
        this.location = _loc;
    }

    public void SetNormalDirection (Vector3 _dir) {
        this.normalDirection = _dir;
    }
}

[System.Serializable]
public class ComponentAttachmentPointData {

    public SerializableVector3 Location;
    public SerializableVector3 Normal;
    public float Size;

}
