using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Vertex {
    #region Variables and Properties
    private Vector2 location;
    public Vector2 Location
    {
        get { return this.location; }
        set
        {
            this.location = value;
            this.RecalculateConnections();
        }
    }

    public Connection[] Connections;

    public bool Selected { get; set; }

    // The angle this Vertex is (every vertex is a corner and should have an "angle" if the two
    // edges are set). This is used to determine how the mesh should be created on this corner.
    public float Angle { get; private set; }
    // The vector bisecting this corner.
    public Vector2 Bisect { get; private set; }

    // Whether this vertex is locked from being moved or deleted.
    public bool IsVertexLocked;
    #endregion

    /// <summary>
    /// Constructors for vertices.
    /// Allows for throwing in no params, only floats, or a vec2.
    /// </summary>
    public Vertex () : this(0, 0) { }
    public Vertex (Vector2 _l) : this(_l.x, _l.y) { }
    public Vertex (float x, float y) {
        Connections = new Connection[2];

        this.Location = new Vector2(x, y);
    }

    public override string ToString () {
        return Location.ToString();
    }

    public void LockVertex (bool _lock) {
        this.IsVertexLocked = _lock;
    }

    public bool IsVertexFilled () {
        if (this.Connections[0] != null && this.Connections[1] != null)
            return true;

        return false;
    }

    public void AddConnection (Connection _newConnect) {
        if (this.IsVertexFilled())
            return;

        if (this.Connections[0] == null)
            this.Connections[0] = _newConnect;
        else if (this.Connections[1] == null)
            this.Connections[1] = _newConnect;
    }

    public void RemoveConnection (Connection _c) {
        if (this.Connections.Length == 0)
            return;

        if (this.Connections[0] == _c) {
            this.Connections[0] = null;
            return;
        }
        else if (this.Connections[1] == _c) {
            this.Connections[1] = null;
            return;
        }

    }
    
    public void RecalculateConnections () {
        bool isCorner = true;

        if (this.Connections[0] != null)
            this.Connections[0].CalculateConnection();
        else
            isCorner = false;

        if (this.Connections[1] != null)
            this.Connections[1].CalculateConnection();
        else
            isCorner = false;

        // If this vertex is a corner, we can caculate the angle and bisect.
        if (isCorner) {
            // Determine the angle of this vertex between its left and its right.
            this.Bisect = this.BisectAngle(this.Connections[0].Direction, this.Connections[1].Direction);

            // Keep the angle between 0 and 180.
            if (this.Angle > 180f)
                this.Angle = 360f - this.Angle;
        }
    }

    public void FlipConnections () {
        Connection temp = this.Connections[0];
        this.Connections[0] = this.Connections[1];
        this.Connections[1] = temp;
    }

    /// <summary>
    /// A function to get the cross product of two Vector2s.
    /// </summary>
    /// <param name="_v">The first Vector2.</param>
    /// <param name="_w">The second Vector2.</param>
    /// <returns>The cross product of the two Vector2s.</returns>
    float CrossVector2 (Vector2 _v, Vector2 _w) {
        return ((_v.x * _w.y) - (_v.y * _w.x));
    }

    /// <summary>
    /// Returns the bisecting Vector2 on a corner with a shared point.
    /// </summary>
    /// <param name="_a">The point forming the first line between this point and _b.</param>
    /// <param name="_b">The shared point of the corner.</param>
    /// <param name="_c">The point forming the second line between this point and _b.</param>
    /// <returns>The Vector2 representing the bisecting angle of the corner.</returns>
    Vector2 BisectAngle (Vector2 _a, Vector2 _b, Vector2 _c) {
        // Find the lines of this corner.
        Vector2 line1 = (_b - _a).normalized;
        Vector2 line2 = (_b - _c).normalized;

        // Find the bisect.
        Vector2 bisect = (line1 + line2).normalized;

        // Find the angle of the corner.
        this.Angle = Mathf.Acos(Vector2.Dot(line1, line2)) * Mathf.Rad2Deg;
        // Find the cross vector. If this is negative, then the angle is convex (<180). If it is positive,
        // then the angle is concave (>180).
        float cross = this.CrossVector2(line1, line2);

        if (cross > 0) {
            this.Angle = 360f - this.Angle;
            bisect *= -1f;
        }

        return bisect;
    }

    Vector2 BisectAngle (Vector2 _line1, Vector2 _line2) {
        // Find the lines of this corner.
        Vector2 line1 = _line1.normalized;
        Vector2 line2 = _line2.normalized;

        // Find the bisect.
        Vector2 bisect = (line1 + line2).normalized;

        // Find the angle of the corner.
        this.Angle = Mathf.Acos(Vector2.Dot(line1, line2)) * Mathf.Rad2Deg;
        // Find the cross vector. If this is negative, then the angle is convex (<180). If it is positive,
        // then the angle is concave (>180).
        float cross = this.CrossVector2(line1, line2);

        if (cross > 0) {
            this.Angle = 360f - this.Angle;
            bisect *= -1f;
        }

        return bisect;
    }
}

[System.Serializable]
public class VertexData {
    public SerializableVector2 Location;
    public bool Locked;
}