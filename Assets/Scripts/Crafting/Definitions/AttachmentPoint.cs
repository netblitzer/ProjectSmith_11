using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttachmentPoint {

    private Component rootComponent;

    public Vector3 location { get; private set; }

    public Vector3 normalDirection { get; private set; }

    public float attachmentSize;

    public AttachmentPoint attachedTo;

    public void SetComponent (Component _root) {
        this.rootComponent = _root;
    }

    public void SetLocation (Vector3 _loc) {
        this.location = _loc;
    }

    public void SetNormalDirection (Vector3 _dir) {
        this.normalDirection = _dir;
    }

    public Vector3 GetWorldPosition () {
        return this.location + this.rootComponent.gameObject.transform.position;
    }

    public Vector3 GetWorldDirection () {
        Matrix4x4 matrix = Matrix4x4.Rotate(this.rootComponent.transform.rotation);
        return matrix.MultiplyPoint(this.normalDirection);
    }

    private Vector3 CorrectEulerAngle (Vector3 _euler) {
        for (int i = 0; i < 3; i++) {
            if (_euler[i] > 180f)
                _euler[i] -= 360f;
            if (_euler[i] < -180f)
                _euler[i] += 360f;
        }

        return _euler;
    }
}

[System.Serializable]
public class AttachmentPointData {

    public SerializableVector3 Location;
    public SerializableVector3 Normal;
    public float Size;

}
