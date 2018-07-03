using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;


[Serializable]
public class SerializableVector3 {
    float x;
    float y;
    float z;

    public SerializableVector3 (float _x, float _y, float _z) {
        this.x = _x;
        this.y = _y;
        this.z = _z;
    }

    public static implicit operator Vector3 (SerializableVector3 _vec3) {
        return new Vector3(_vec3.x, _vec3.y, _vec3.z);
    }

    public static implicit operator SerializableVector3 (Vector3 _vec3) {
        return new SerializableVector3(_vec3.x, _vec3.y, _vec3.z);
    }
}

[Serializable]
public class SerializableVector2 {
    float x;
    float y;

    public SerializableVector2 (float _x, float _y) {
        this.x = _x;
        this.y = _y;
    }

    public static implicit operator Vector2 (SerializableVector2 _vec2) {
        return new Vector2(_vec2.x, _vec2.y);
    }

    public static implicit operator SerializableVector2 (Vector2 _vec2) {
        return new SerializableVector2(_vec2.x, _vec2.y);
    }
}