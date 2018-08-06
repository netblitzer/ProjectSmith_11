﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttachmentPoint {

    // The component of this attachment point.
    private Component rootComponent;

    // The location this attachment point is at in local space.
    public Vector3 location { get; private set; }

    // The direction this attachment point faces in local space.
    public Vector3 normalDirection { get; private set; }

    // The size of the attachment point.
    public float attachmentSize;

    // Which attachment point this one is currently attached to for referencing.
    public AttachmentPoint attachedTo;

    public bool IsAttached;

    

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
        return this.rootComponent.gameObject.transform.localToWorldMatrix.MultiplyPoint3x4(this.location);
    }

    public Vector3 GetWorldDirection () {
        Matrix4x4 matrix = Matrix4x4.Rotate(this.rootComponent.transform.rotation);
        return matrix.MultiplyPoint(this.normalDirection);
    }

    public void AttachTo (AttachmentPoint _other) {
        this.attachedTo = _other;
        this.IsAttached = true;
    }

    public void UnAttach () {
        this.attachedTo = null;
        this.IsAttached = false;
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
