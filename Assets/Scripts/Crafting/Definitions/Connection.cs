using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Connection {

    // The first vertex in this connection.
    public Vertex First;

    // The second vertex in this connection.
    public Vertex Second;

    // What type of edge this connection is.
    public EdgeType Edge;

    // Whether the connection can be modified/deleted.
    public bool IsConnectionLocked;

    public bool ConnectionCanBeAttachedTo { get; private set; }

    // The direction of this connection. (normalized)
    public Vector2 Direction;

    // The normal directon of this connection. (normalized)
    public Vector2 Normal;

    // The point in between the two vertices.
    public Vector2 MidPoint;

    // The length of this connection.
    public float Length;

    public bool SetConnection (Vertex _f, Vertex _s, EdgeType _t) {
        if (_f.IsVertexFilled() || _s.IsVertexFilled())
            return false;

        this.First = _f;
        this.Second = _s;
        this.Edge = _t;

        this.First.AddConnection(this);
        this.Second.AddConnection(this);

        this.CalculateConnection();

        return true;
    }

    public void LockConnection (bool _lock) {
        this.IsConnectionLocked = _lock;
    }

    public bool ContainsVertex (Vertex _v) {
        if (this.First == _v || this.Second == _v)
            return true;

        return false;
    }

    public void DestroyConnection () {
        if (this.First != null)
            this.First.RemoveConnection(this);

        if (this.Second != null)
            this.Second.RemoveConnection(this);
    }

    public void CalculateConnection () {
        if (this.First == null || this.Second == null)
            return;

        this.Direction = this.First.Location - this.Second.Location;
        this.Length = this.Direction.magnitude;
        this.Direction.Normalize();
        this.Normal = new Vector2(this.Direction.y, -this.Direction.x);

        this.MidPoint = (this.First.Location + this.Second.Location) / 2f;

        if (this.Length >= 1f && this.Edge == EdgeType.FLAT)
            this.ConnectionCanBeAttachedTo = true;
        else
            this.ConnectionCanBeAttachedTo = false;
    }

    public void FlipConnection () {
        if (this.First != null && this.Second != null) {
            Vertex temp = this.Second;
            this.Second = this.First;
            this.First = temp;
        }
    }

    public bool CheckIfCanBeAttachedTo () {

        if (this.Length >= 1f && this.Edge == EdgeType.FLAT)
            this.ConnectionCanBeAttachedTo = true;
        else
            this.ConnectionCanBeAttachedTo = false;

        return this.ConnectionCanBeAttachedTo;
    }
}

[System.Serializable]
public class ConnectionData {
    public int First;
    public int Second;
    public int Type;
}
