using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class DesignerManager : MonoBehaviour {

    public Camera designCamera;

    public Camera viewCamera;

    public GameObject renderedComponent;

    public GameObject renderedPrefab;

    public GameObject rootObjectForRendering;

    // The vertices that make up the completed shape starting with the starting point.
    private List<Vertex> polygonVertices;

    /// <summary>
    /// The connections that make up the completed shape starting with the starting point.
    ///  This list is a list of all RIGHT connections (meaning the current point to the next).
    ///  If this list is 4 long, that means the first connection is from 0 to 1.
    /// </summary>
    private List<Connection> polygonConnections;

    // The script that will expand the polygon shape into a mesh. This will just feed back a list
    //  of final points and indices for us to work with.
    private MeshExpansion expander;

    private List<int> meshIndices;

    private List<Vector3> meshVertices;

    private string componentBasePath = "/SaveData/Components/";

    public Component currentComponent;

    private bool changesSinceLastSave;

    // Whether the component has been expanded into 3D space or not.
    private bool isCurrentlyExpanded;

    // The vertex that was last placed in the scene.
    private Vertex lastPlacedVertex;

    // The currently selected vertex for movement.
    private Vertex selectedVertex;

    // Whether the grid is enabled or not.
    private bool isGridEnabled;

    // How many steps the grid has. Each step is twice the size of the previous step.
    public int gridMaxSteps = 4;

    // The starting size of the grid (minimum).
    public float gridStartingSize = 0.25f;

    // The current step that the grid is on (1 is minimum).
    private int currentGridStep = 3;

    // The current size the grid is.
    private float currentGridSize = 1;

    // The starting vertex of the shape.
    private Vertex startingVertex;

    // The ending vertex of the shape.
    private Vertex endingVertex;

    // Use this for initialization
    void Start () {
        // Set up the initial mesh info.
        this.expander = new MeshExpansion();
        this.meshIndices = new List<int>();
        this.meshVertices = new List<Vector3>();
        this.currentComponent = new Component();

        // Intitialize the grid.
        this.isGridEnabled = false;
        this.currentGridSize = 4;

        this.isCurrentlyExpanded = false;
        this.changesSinceLastSave = false;

        // Initialize the starting points.
        this.startingVertex = new Vertex(-0.5f, -2f);
        this.endingVertex = new Vertex(0.5f, -2f);
        this.startingVertex.LockVertex(true);
        this.endingVertex.LockVertex(true);
        this.currentComponent.vertices.Add(this.startingVertex);
        this.currentComponent.vertices.Add(this.endingVertex);

        // Set up initial connections.
        Connection startingConnection = new Connection();
        startingConnection.SetConnection(this.endingVertex, this.startingVertex, EdgeType.FLAT);
        startingConnection.LockConnection(true);
        this.currentComponent.connections.Add(startingConnection);

	}
	
	// Update is called once per frame.
    // Currently used to draw the shape.
	void Update () {

        // Draw grid.
        if (this.designCamera.isActiveAndEnabled && this.isGridEnabled) {
            // Find the current camera dimensions.
            Vector3 cameraDimensions = new Vector3(this.designCamera.orthographicSize * this.designCamera.aspect, this.designCamera.orthographicSize);

            // Draw the current grid.
            for (float x = 0; x < cameraDimensions.x; x += this.currentGridSize) {
                DebugLines.Instance.AddLine(new Vector2(x, cameraDimensions.y), new Vector2(x, -cameraDimensions.y), Color.grey);
                DebugLines.Instance.AddLine(new Vector2(-x, cameraDimensions.y), new Vector2(-x, -cameraDimensions.y), Color.grey);
            }
            for (float y = 0; y < cameraDimensions.y; y += this.currentGridSize) {
                DebugLines.Instance.AddLine(new Vector2(cameraDimensions.x, y), new Vector2(-cameraDimensions.x, y), Color.grey);
                DebugLines.Instance.AddLine(new Vector2(cameraDimensions.x, -y), new Vector2(-cameraDimensions.x, -y), Color.grey);
            }
        }

        // Draw vertices.
        for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
            Vertex v = this.currentComponent.vertices[i];
            Vector3 loc = new Vector3(v.Location.x, v.Location.y, 0);

            if (v.Selected) {
                DebugLines.Instance.AddLine(new Vector2(loc.x - 0.12f, loc.y), new Vector2(loc.x, loc.y + 0.12f), new Color(1, 0.45f, 0f));
                DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y + 0.12f), new Vector2(loc.x + 0.12f, loc.y), new Color(1, 0.45f, 0f));
                DebugLines.Instance.AddLine(new Vector2(loc.x + 0.12f, loc.y), new Vector2(loc.x, loc.y - 0.12f), new Color(1, 0.45f, 0f));
                DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y - 0.12f), new Vector2(loc.x - 0.12f, loc.y), new Color(1, 0.45f, 0f));
            }
            else {
                DebugLines.Instance.AddLine(new Vector2(loc.x - 0.12f, loc.y), new Vector2(loc.x, loc.y + 0.12f), Color.yellow);
                DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y + 0.12f), new Vector2(loc.x + 0.12f, loc.y), Color.yellow);
                DebugLines.Instance.AddLine(new Vector2(loc.x + 0.12f, loc.y), new Vector2(loc.x, loc.y - 0.12f), Color.yellow);
                DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y - 0.12f), new Vector2(loc.x - 0.12f, loc.y), Color.yellow);
            }

            if (v == this.selectedVertex) {
                if (v.IsVertexLocked) {
                    DebugLines.Instance.AddLine(new Vector2(loc.x - 0.2f, loc.y), new Vector2(loc.x, loc.y + 0.2f), Color.red);
                    DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y + 0.2f), new Vector2(loc.x + 0.2f, loc.y), Color.red);
                    DebugLines.Instance.AddLine(new Vector2(loc.x + 0.2f, loc.y), new Vector2(loc.x, loc.y - 0.2f), Color.red);
                    DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y - 0.2f), new Vector2(loc.x - 0.2f, loc.y), Color.red);
                }
                else {
                    DebugLines.Instance.AddLine(new Vector2(loc.x - 0.2f, loc.y), new Vector2(loc.x, loc.y + 0.2f), Color.blue);
                    DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y + 0.2f), new Vector2(loc.x + 0.2f, loc.y), Color.blue);
                    DebugLines.Instance.AddLine(new Vector2(loc.x + 0.2f, loc.y), new Vector2(loc.x, loc.y - 0.2f), Color.blue);
                    DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y - 0.2f), new Vector2(loc.x - 0.2f, loc.y), Color.blue);
                }
            }
        }

        // Draw lines.
        for (int i = 0; i < this.currentComponent.connections.Count; i++) {
            Connection c = this.currentComponent.connections[i];

            if (c.Edge == EdgeType.FLAT)
                DebugLines.Instance.AddLine(c.First.Location, c.Second.Location, new Color(0, 1, 1));
            else if (c.Edge == EdgeType.SHARP)
                DebugLines.Instance.AddLine(c.First.Location, c.Second.Location, new Color(0.8f, 0, 0.8f));

            if (c.ConnectionCanBeAttachedTo) {
                Vector2 loc = c.MidPoint;

                DebugLines.Instance.AddLine(new Vector2(loc.x - 0.2f, loc.y), new Vector2(loc.x, loc.y + 0.2f), Color.blue);
                DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y + 0.2f), new Vector2(loc.x + 0.2f, loc.y), Color.blue);
                DebugLines.Instance.AddLine(new Vector2(loc.x + 0.2f, loc.y), new Vector2(loc.x, loc.y - 0.2f), Color.blue);
                DebugLines.Instance.AddLine(new Vector2(loc.x, loc.y - 0.2f), new Vector2(loc.x - 0.2f, loc.y), Color.blue);
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="_location"></param>
    /// <returns></returns>
    public int AddClicked (Vector2 _location, EdgeType _type) {
        Vector2 pos = _location;

        // If the grid is on, we will want to adjust the location to snap to the nearest grid point.
        if (this.isGridEnabled) {
            pos = this.PointToGrid(pos);
        }

        // See if we already have a chain of points going.
        if (this.lastPlacedVertex == null) {
            // Find out if we just clicked on a point.
            float curDist = float.MaxValue;
            int curVert = -1;
            // Find the closest vertex to the mouse location (if there is any point close by).
            for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
                float dist = Vector2.Distance(this.currentComponent.vertices[i].Location, _location);

                if (dist < 0.5f && dist < curDist) {
                    curDist = dist;
                    curVert = i;
                }
            }

            // We weren't close to a point, we should just add a new one.
            if (curVert == -1) {
                // TODO: Check to see if there is already a point where we're trying to add a new one (if the grid is on).

                // Create a new point at the location and start a chain.
                Vertex newVert = new Vertex(pos);

                this.currentComponent.vertices.Add(newVert);
                this.lastPlacedVertex = newVert;
                this.lastPlacedVertex.Selected = true;

                // Set that there has been a change since the last save.
                this.changesSinceLastSave = true;

                // Return true to state that we added a new "action" onto the current list of actions.
                return 1;
            }
            else {
                // We clicked a point, start the chain following this one.

                // Make sure we don't set the lastPlacedVertex to a filled vertex.
                if (!this.currentComponent.vertices[curVert].IsVertexFilled()) {
                    this.lastPlacedVertex = this.currentComponent.vertices[curVert];
                    this.lastPlacedVertex.Selected = true;
                }
                else {
                    // Otherwise, we need to create a new point.
                    // TODO: Check to see if there is already a point where we're trying to add a new one (if the grid is on).

                    // Create a new point at the location and start a chain.
                    Vertex newVert = new Vertex(pos);

                    this.currentComponent.vertices.Add(newVert);
                    this.lastPlacedVertex.Selected = false;
                    this.lastPlacedVertex = newVert;
                    this.lastPlacedVertex.Selected = true;
                }

                // Return true to state that we added a new "action" onto the current list of actions.
                return 1;
            }

        }
        else {
            // If LastPlacedVertex isn't null, it means we're making a chain/line of points.

            // First, see if we clicked on another point.
            float curDist = float.MaxValue;
            int curVert = -1;
            // Find the closest vertex to the mouse location (if there is any point close by).
            for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
                float dist = Vector2.Distance(this.currentComponent.vertices[i].Location, _location);

                if (dist < 0.25f && dist < curDist) {
                    curDist = dist;
                    curVert = i;
                }
            }

            // We weren't close to a point, we should just add a new one.
            if (curVert == -1) {

                // See if this point is legal to place.
                // TODO: Add check for legality

                // Create a new point at the location and start a chain.
                Vertex newVert = new Vertex(pos);

                // Make sure the last placed vertex isn't full already. If it is, that means we can't connect to it.
                if (!this.lastPlacedVertex.IsVertexFilled()) {
                    // Add the new vertex to the list.
                    this.currentComponent.vertices.Add(newVert);

                    // Create a new connection.
                    Connection newConnect = new Connection();
                    newConnect.SetConnection(newVert, this.lastPlacedVertex, _type);

                    // Add the connection references.
                    this.currentComponent.connections.Add(newConnect);

                    // Set that there has been a change since the last save.
                    this.changesSinceLastSave = true;

                    // Set the new vertex as the last placed.
                    this.lastPlacedVertex.Selected = false;
                    this.lastPlacedVertex = newVert;
                    this.lastPlacedVertex.Selected = true;

                    // Return false because we didn't add any new "action". We simply moved the lastPlacedVertex to a
                    // different vertex.
                    return 0;
                }
            }
            else {
                // We were close enough to the vertex.
                Vertex temp = this.currentComponent.vertices[curVert];

                // Make sure both points have empty space to connect to.
                if (!this.lastPlacedVertex.IsVertexFilled() && !temp.IsVertexFilled()) {

                    // Create a new connection.
                    Connection newConnect = new Connection();
                    newConnect.SetConnection(temp, this.lastPlacedVertex, _type);

                    // Add the connection references.
                    this.currentComponent.connections.Add(newConnect);

                    // Set that there has been a change since the last save.
                    this.changesSinceLastSave = true;

                    // Make sure we don't set the lastPlacedVertex to a filled vertex.
                    if (!temp.IsVertexFilled()) {
                        this.lastPlacedVertex.Selected = false;
                        this.lastPlacedVertex = temp;
                        this.lastPlacedVertex.Selected = true;
                    }
                    else {
                        // Otherwise we'll just disconnect from the chain.
                        this.lastPlacedVertex.Selected = false;
                        this.lastPlacedVertex = null;

                        // We're removing from the chain since we no longer are connected to one.
                        return -1;
                    }

                    // Return false because we didn't add any new "action". We simply moved the lastPlacedVertex to a
                    // different vertex.
                    return 0;
                }
            }
        }

        // Fall through return in case something failed.
        return 0;
    }

    public bool AddRightClicked () {
        if (this.lastPlacedVertex != null) {
            this.lastPlacedVertex.Selected = false;
            this.lastPlacedVertex = null;
            // Return true that we cleared the lastPlacedVertex.
            return true;
        }
        else
            return false;
    }

    public void RemoveClicked (Vector2 _location) {

        // Find out if we just clicked on a point.
        float curVertDist = float.MaxValue;
        int curVert = -1;
        // Find the closest vertex to the mouse location (if there is any point close by).
        for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
            float dist = Vector2.Distance(this.currentComponent.vertices[i].Location, _location);

            if (dist < 0.25f && dist < curVertDist) {
                curVertDist = dist;
                curVert = i;
            }
        }

        // Find the closest line to see if we clicked one.
        float curLineDist = float.MaxValue;
        int curLine = -1;
        // see which line is closest to the mouse
        for (int i = 0; i < this.currentComponent.connections.Count; i++) {
            // get distance to line
            float dist = distanceFromLine(this.currentComponent.connections[i].First.Location, this.currentComponent.connections[i].Second.Location, _location);
            // compare to current closest
            if (dist < 0.25f && dist < curLineDist) {
                // set new closest
                curLine = i;
                curLineDist = dist;
            }
        }

        float pointBias = 2f;

        // We weren't close to a point.
        if (curVert == -1) {
            // We weren't close to a line either.
            if (curLine == -1) {
                return;
            }

            // A line was the closest thing to where we clicked. We need to remove that connection.
            Connection temp = this.currentComponent.connections[curLine];

            // Make sure we don't delete a locked connection.
            if (!temp.IsConnectionLocked) {
                temp.First.RemoveConnection(temp);
                temp.Second.RemoveConnection(temp);

                this.currentComponent.connections.RemoveAt(curLine);

                // Set that there has been a change since the last save.
                this.changesSinceLastSave = true;
            }
        }
        else {
            // See if there was no close line OR if the points were "closer" than the line.
            if (curLine == -1 || curVertDist < curLineDist * pointBias) {
                // A point was the closest thing to where we clicked. We need to remove it and all of its connections.
                Vertex temp = this.currentComponent.vertices[curVert];

                // Make sure the vertex isn't locked.
                if (!temp.IsVertexLocked) {
                    if (temp.Connections[0] != null) {
                        this.currentComponent.connections.Remove(temp.Connections[0]);
                        temp.Connections[0].DestroyConnection();
                    }

                    if (temp.Connections[1] != null) {
                        this.currentComponent.connections.Remove(temp.Connections[1]);
                        temp.Connections[1].DestroyConnection();
                    }

                    this.currentComponent.vertices.RemoveAt(curVert);

                    // Set that there has been a change since the last save.
                    this.changesSinceLastSave = true;
                }
            }
            else {
                // A line was the closest thing to where we clicked. We need to remove that connection.
                Connection temp = this.currentComponent.connections[curLine];

                // Make sure not to delete a locked connection.
                if (!temp.IsConnectionLocked) {
                    temp.First.RemoveConnection(temp);
                    temp.Second.RemoveConnection(temp);

                    this.currentComponent.connections.RemoveAt(curLine);

                    // Set that there has been a change since the last save.
                    this.changesSinceLastSave = true;
                }
            }
        }
    }

    public void EdgeModeClicked (Vector2 _location, EdgeType _type) {
        
        // Find the closest line to see if we clicked one.
        float curLineDist = float.MaxValue;
        int curLine = -1;
        // see which line is closest to the mouse
        for (int i = 0; i < this.currentComponent.connections.Count; i++) {
            // get distance to line
            float dist = distanceFromLine(this.currentComponent.connections[i].First.Location, this.currentComponent.connections[i].Second.Location, _location);
            // compare to current closest
            if (dist < 0.25f && dist < curLineDist) {
                // set new closest
                curLine = i;
                curLineDist = dist;
            }
        }

        if (curLine != -1) {
            this.currentComponent.connections[curLine].Edge = _type;

            this.currentComponent.connections[curLine].CheckIfCanBeAttachedTo();

            // Set that there has been a change since the last save.
            this.changesSinceLastSave = true;
        }
    }

    public void AddHovering (Vector2 _location) {
        Vector2 pos = _location;

        // If the grid is on, we will want to adjust the location to snap to the nearest grid point.
        if (this.isGridEnabled) {
            pos = this.PointToGrid(pos);
        }

        DebugLines.Instance.AddLine(new Vector2(pos.x - 0.24f, pos.y), new Vector2(pos.x, pos.y + 0.24f), Color.green);
        DebugLines.Instance.AddLine(new Vector2(pos.x, pos.y + 0.24f), new Vector2(pos.x + 0.24f, pos.y), Color.green);
        DebugLines.Instance.AddLine(new Vector2(pos.x + 0.24f, pos.y), new Vector2(pos.x, pos.y - 0.24f), Color.green);
        DebugLines.Instance.AddLine(new Vector2(pos.x, pos.y - 0.24f), new Vector2(pos.x - 0.24f, pos.y), Color.green);

        if (this.lastPlacedVertex != null) {
            DebugLines.Instance.AddLine(pos, this.lastPlacedVertex.Location, Color.green);
        }
    }

    public void MoveHovering (Vector2 _location) {
        // Make sure we don't try to find the nearest hover point if we're already dragging around a point.
        if (this.selectedVertex != null)
            return;

        if (this.currentComponent.vertices.Count == 0)
            return;

        float curDist = float.MaxValue;
        int curVert = -1;
        // Find the closest vertex to the mouse location (if there is any point close by).
        for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
            float dist = Vector2.Distance(this.currentComponent.vertices[i].Location, _location);

            if (dist < 0.25f && dist < curDist)
                curVert = i;
        }

        if (curVert == -1)
            return;

        // If we found a point, draw a diamond around it.
        Vertex temp = this.currentComponent.vertices[curVert];

        if (!temp.IsVertexLocked) {
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x - 0.24f, temp.Location.y), new Vector2(temp.Location.x, temp.Location.y + 0.24f), Color.cyan);
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x, temp.Location.y + 0.24f), new Vector2(temp.Location.x + 0.24f, temp.Location.y), Color.cyan);
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x + 0.24f, temp.Location.y), new Vector2(temp.Location.x, temp.Location.y - 0.24f), Color.cyan);
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x, temp.Location.y - 0.24f), new Vector2(temp.Location.x - 0.24f, temp.Location.y), Color.cyan);
        }
        else {
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x - 0.24f, temp.Location.y), new Vector2(temp.Location.x, temp.Location.y + 0.24f), new Color(1, 0.5f, 0.5f));
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x, temp.Location.y + 0.24f), new Vector2(temp.Location.x + 0.24f, temp.Location.y), new Color(1, 0.5f, 0.5f));
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x + 0.24f, temp.Location.y), new Vector2(temp.Location.x, temp.Location.y - 0.24f), new Color(1, 0.5f, 0.5f));
            DebugLines.Instance.AddLine(new Vector2(temp.Location.x, temp.Location.y - 0.24f), new Vector2(temp.Location.x - 0.24f, temp.Location.y), new Color(1, 0.5f, 0.5f));
        }
    }

    public void RemoveHovering (Vector2 _location) {

        // Find out if we just clicked on a point.
        float curVertDist = float.MaxValue;
        int curVert = -1;
        // Find the closest vertex to the mouse location (if there is any point close by).
        for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
            float dist = Vector2.Distance(this.currentComponent.vertices[i].Location, _location);

            if (dist < 0.25f && dist < curVertDist) {
                curVertDist = dist;
                curVert = i;
            }
        }

        // Find the closest line to see if we clicked one.
        float curLineDist = float.MaxValue;
        int curLine = -1;
        // see which line is closest to the mouse
        for (int i = 0; i < this.currentComponent.connections.Count; i++) {
            // get distance to line
            float dist = distanceFromLine(this.currentComponent.connections[i].First.Location, this.currentComponent.connections[i].Second.Location, _location);
            // compare to current closest
            if (dist < 0.25f && dist < curLineDist) {
                // set new closest
                curLine = i;
                curLineDist = dist;
            }
        }

        float pointBias = 2f;

        // We weren't close to a point.
        if (curVert == -1) {
            // We weren't close to a line either.
            if (curLine == -1) {
                return;
            }

            // A line was the closest thing to where we clicked. We need to remove that connection.
            Connection temp = this.currentComponent.connections[curLine];

            DebugLines.Instance.AddLine(temp.First.Location + temp.Normal * 0.1f, temp.Second.Location + temp.Normal * 0.1f, Color.red);
            DebugLines.Instance.AddLine(temp.First.Location - temp.Normal * 0.1f, temp.Second.Location - temp.Normal * 0.1f, Color.red);
        }
        else {
            // See if there was no close line OR if the points were "closer" than the line.
            if (curLine == -1 || curVertDist < curLineDist * pointBias) {
                // A point was the closest thing to where we clicked. We need to remove it and all of its connections.
                Vertex temp = this.currentComponent.vertices[curVert];

                DebugLines.Instance.AddLine(new Vector2(temp.Location.x - 0.24f, temp.Location.y), new Vector2(temp.Location.x, temp.Location.y + 0.24f), Color.red);
                DebugLines.Instance.AddLine(new Vector2(temp.Location.x, temp.Location.y + 0.24f), new Vector2(temp.Location.x + 0.24f, temp.Location.y), Color.red);
                DebugLines.Instance.AddLine(new Vector2(temp.Location.x + 0.24f, temp.Location.y), new Vector2(temp.Location.x, temp.Location.y - 0.24f), Color.red);
                DebugLines.Instance.AddLine(new Vector2(temp.Location.x, temp.Location.y - 0.24f), new Vector2(temp.Location.x - 0.24f, temp.Location.y), Color.red);

                if (temp.Connections[0] != null) {
                    Connection c = temp.Connections[0];

                    DebugLines.Instance.AddLine(c.First.Location + c.Normal * 0.1f, c.Second.Location + c.Normal * 0.1f, Color.red);
                    DebugLines.Instance.AddLine(c.First.Location - c.Normal * 0.1f, c.Second.Location - c.Normal * 0.1f, Color.red);
                }
                if (temp.Connections[1] != null) {
                    Connection c = temp.Connections[1];

                    DebugLines.Instance.AddLine(c.First.Location + c.Normal * 0.1f, c.Second.Location + c.Normal * 0.1f, Color.red);
                    DebugLines.Instance.AddLine(c.First.Location - c.Normal * 0.1f, c.Second.Location - c.Normal * 0.1f, Color.red);
                }

            }
            else {
                // A line was the closest thing to where we clicked. We need to remove that connection.
                Connection temp = this.currentComponent.connections[curLine];

                DebugLines.Instance.AddLine(temp.First.Location + temp.Normal * 0.1f, temp.Second.Location + temp.Normal * 0.1f, Color.red);
                DebugLines.Instance.AddLine(temp.First.Location - temp.Normal * 0.1f, temp.Second.Location - temp.Normal * 0.1f, Color.red);
            }
        }
    }

    public float GetGridSize () {
        return this.currentGridSize;
    }

    public int GetGridStep () {
        return this.currentGridStep;
    }

    public int SetGridSize (bool _increase) {
        if (_increase)
            this.currentGridStep = Mathf.Min(this.currentGridStep + 1, this.gridMaxSteps);
        else
            this.currentGridStep = Mathf.Max(this.currentGridStep - 1, 1);

        this.currentGridSize = this.gridStartingSize * Mathf.Pow(2f, this.currentGridStep - 1);

        return this.currentGridStep;
    }

    public bool ToggleGrid () {
        this.isGridEnabled = !this.isGridEnabled;

        return this.isGridEnabled;
    }

    public Vector3 PointToGrid (Vector3 _location) {
        Vector3 pos = _location;

        pos /= this.currentGridSize;
        pos.Set(Mathf.Round(pos.x), Mathf.Round(pos.y), Mathf.Round(pos.z));
        pos *= this.currentGridSize;

        return pos;
    }

    public void StartDragging (Vector2 _location) {
        if (this.currentComponent.vertices.Count == 0)
            return;

        float curDist = float.MaxValue;
        int curVert = -1;
        // Find the closest vertex to the mouse location (if there is any point close by).
        for (int i = 0; i < this.currentComponent.vertices.Count; i++) {
            float dist = Vector2.Distance(this.currentComponent.vertices[i].Location, _location);

            if (dist < 0.5f && dist < curDist)
                curVert = i;
        }

        if (curVert == -1)
            return;

        this.selectedVertex = this.currentComponent.vertices[curVert];
        this.MoveDraggedVertex(_location);
    }

    public void MoveDraggedVertex (Vector2 _location) {
        // Don't move a locked vertex.
        if (this.selectedVertex != null && !this.selectedVertex.IsVertexLocked) {
            Vector2 pos = _location;

            // If the grid is on, we will want to adjust the location to snap to the nearest grid point.
            if (this.isGridEnabled) {
                pos /= this.currentGridSize;
                pos.Set(Mathf.Round(pos.x), Mathf.Round(pos.y));
                pos *= this.currentGridSize;
            }

            this.selectedVertex.Location = pos;

            // Set that there has been a change since the last save.
            this.changesSinceLastSave = true;
        }
    }

    public void EndDragging (Vector2 _location) {
        if (this.selectedVertex != null) {
            this.MoveDraggedVertex(_location);
            this.selectedVertex.Selected = false;
            this.selectedVertex = null;
        }
    }

    public bool ToggleExpansion (bool _toggle) {
        // If we're not expanded already.
        if (!this.isCurrentlyExpanded && _toggle) {
            // First, sort all the vertices and get a final list for the polygon shape.
            if (this.SortVertices()) {
                // If sorting succeeded, then try to expand the shape.
                if (this.expander.CreateMesh(this.polygonVertices, this.polygonConnections, 1.0f, 30f, out this.meshIndices, out this.meshVertices, out this.currentComponent.attacmenthPoints)) {
                    // If the expansion succeeded, we can toggle the mode and switch to displaying the component.
                    this.isCurrentlyExpanded = true;

                    // Make sure each attachment point is correctly set.
                    foreach (AttachmentPoint ap in this.currentComponent.attacmenthPoints)
                        ap.SetComponent(this.currentComponent);

                    // Create a new mesh for the object.
                    Mesh m = new Mesh();
                    // If the object already exists, it will have a mesh on it to reuse.
                    if (renderedComponent != null) {
                        m = this.renderedComponent.GetComponent<MeshFilter>().mesh;
                        m.Clear();
                    }

                    // Set the properties of the mesh.
                    m.SetVertices(this.meshVertices);
                    m.SetTriangles(this.meshIndices, 0);

                    m.RecalculateNormals();
                    m.RecalculateBounds();
                    m.RecalculateTangents();

                    // Create the new object if it doesn't already exist.
                    if (this.renderedComponent == null) {
                        this.renderedComponent = GameObject.Instantiate(this.renderedPrefab, new Vector3(), Quaternion.identity);
                        this.renderedComponent.transform.SetParent(this.rootObjectForRendering.transform);
                    }
                    // Set the new mesh.
                    this.renderedComponent.GetComponent<MeshFilter>().mesh = m;

                    // Make sure the object is enabled.
                    this.renderedComponent.SetActive(true);

                    // Set the component values.
                    //this.currentComponent.SetConnections(this.connections);
                    //this.currentComponent.SetVertices(this.vertices);
                    this.currentComponent.SetMesh(m);

                    // Return true.
                    return this.isCurrentlyExpanded;
                }
                else
                    // Expansion failed.
                    return this.isCurrentlyExpanded;
            }
            else {
                // Sorting failed.
                return this.isCurrentlyExpanded;
            }
        }
        else {
            // Disable the mesh just in case.
            if (this.renderedComponent != null)
                this.renderedComponent.SetActive(false);

            return (this.isCurrentlyExpanded = false);
        }
    }

    /// <summary>
    /// See if all the points are connected to each other in a chain (or at least that there is a chain) and
    /// sort all the connections to make sure they're following the correct direction.
    /// This also creates and sorts the final list for the formed polygon.
    /// </summary>
    /// <returns></returns>
    private bool SortVertices () {
        // Initialize the starting parameters for sorting. The curVert and prevVert will always start with the
        // starting and ending points.
        Vertex curVert = this.startingVertex;
        Vertex prevVert = this.endingVertex;
        // A hardcap limit in case things go wrong.
        int tries = 0;

        // Initialize the polygon parameters.
        this.polygonVertices = new List<Vertex>();
        this.polygonConnections = new List<Connection>();

        // Make sure we loop all the way though and correct all connections if needed.
        do {
            tries++;

            // Make sure the current vertex has two connections, otherwise we can't keep sorting and we don't have
            // a completed shape.
            if (!curVert.IsVertexFilled()) {
                return false;
            }

            // Make sure the vertex has its connections in the correct order (first connection is to it and the previous
            // vertex, second connection is to it and the next vertex).
            if (!curVert.Connections[0].ContainsVertex(prevVert)) {
                // Just in case, make sure the second connection has the previous vertex. Otherwise the shape has failed.
                if (!curVert.Connections[1].ContainsVertex(prevVert))
                    return false;

                curVert.FlipConnections();
            }

            // Check to make sure the connection is in the correct order. The previous vertex should be first, followed
            // by the next (current) vertex.
            if (curVert.Connections[0].First != prevVert) {
                curVert.Connections[0].FlipConnection();
            }

            // The vertex should now be sorted properly, and the first connection should now be correct.
            // Calculate all the connection properties now that it's accurate.
            curVert.Connections[0].CalculateConnection();

            // Add the vertex and the connection to the polygon list.
            this.polygonVertices.Add(curVert);
            this.polygonConnections.Add(curVert.Connections[1]);

            // Get the new current and previous vertices.
            prevVert = curVert;

            if (prevVert.Connections[1].First == prevVert)
                curVert = prevVert.Connections[1].Second;
            else
                curVert = prevVert.Connections[1].First;

        } while (curVert != this.startingVertex && tries < 10000);

        if (tries >= 10000)
            return false;

        return true;
    }

    public string SaveCurrentComponent (string _name, bool _overwrite) {
        // If we're overwriting the save, we don't need to check with the player.
        if (_overwrite) {
            this.currentComponent.SetName(_name);
            string status = this.currentComponent.SaveComponent(_overwrite, this.componentBasePath);

            if (status == "SAVED")
                this.changesSinceLastSave = false;

            return status;
        }
        // If we're not overwriting yet, check to see if it's needed and notify the player if it is. Otherwise we can save.
        else {
            this.currentComponent.SetName(_name);
            string status = this.currentComponent.SaveComponent(_overwrite, this.componentBasePath);

            if (status == "SAVED")
                this.changesSinceLastSave = false;

            return status;
        }
    }

    /// <summary>
    /// Gets all the loadable options and creates a list of them and creates all of their gameobjects.
    /// </summary>
    /// <param name="_prefab">The load option prefab to pass in.</param>
    /// <returns>A list of loadable components in the directory. Null if there are no components to load.</returns>
    public List<GameObject> GetLoadableComponentOptions (GameObject _prefab) {
        List<GameObject> options = new List<GameObject>();

        // Find out if there are any saved components.
        // See if there's even a directory. If it doesn't, return null.
        if (!Directory.Exists(Application.persistentDataPath + this.componentBasePath))
            return null;

        string[] files = Directory.GetFiles(Application.persistentDataPath + this.componentBasePath);

        // See if there are any files to load in. Return null if there aren't.
        if (files.Length == 0)
            return null;

        // Go through each file path and create a new LoadOption for them.
        foreach (string path in files) {
            // Make sure we don't load in a file that isn't the correct data type.
            if (Path.GetExtension(path) != ".dat")
                continue;

            // Create the new object and get its script.
            GameObject temp = GameObject.Instantiate(_prefab);
            LoadComponentOption tempLoadOption = temp.GetComponent<LoadComponentOption>();

            // Set up the script.
            if (tempLoadOption.SetFilePath(path))
                // If it's successful, add the new option to the list.
                options.Add(temp);
            else
                // Otherwise, delete the option because it failed to load.
                GameObject.Destroy(temp);
        }

        return options;
    }

    public string LoadSelectedOption (string _path, bool _nosave) {
        // If we're not saving or there aren't changes to save, just load up the component.
        if (_nosave || !this.changesSinceLastSave) {
            this.currentComponent.LoadComponent(_path);
            this.changesSinceLastSave = false;

            // Set the starting and ending vertex and the starting connection.
            this.startingVertex = this.currentComponent.vertices[0];
            this.endingVertex = this.currentComponent.vertices[1];

            return "LOADED";
        }
        // If we know there's changes and we are potentially saving, return that we should check with the player.
        else {
            return "UNSAVEDCHANGES";
        }
    }

    /// <summary>
    /// Gets the distance of a point from a line described by the start point and end point.
    /// If the point is closer than the curMax, then the global projPoint will be replaced.
    /// </summary>
    /// <param name="_startPoint">Starting point of the line.</param>
    /// <param name="_endPoint">Ending point of the line.</param>
    /// <param name="_targetPoint">Point to test to the line.</param>
    /// <param name="curMax">Current maximum distance from the line. Used for comparison reasons.</param>
    /// <returns>Returns the distance from the line.</returns>
    float distanceFromLine (Vector2 _startPoint, Vector2 _endPoint, Vector2 _targetPoint) {

        // Calculate line vectors.
        Vector2 lineVec = (_endPoint - _startPoint);
        float lineLength = lineVec.magnitude;
        lineVec.Normalize();
        Vector2 lineUpVec = new Vector2(lineVec.y, -lineVec.x);

        Vector2 targetVec = _targetPoint - _startPoint;

        // Calculate distances in 2 directions from the line.
        float upDist = Vector2.Dot(lineUpVec, targetVec);
        float lngDist = Vector2.Dot(lineVec, targetVec);

        float dist = 0;

        // Behind start point
        if (lngDist < 0) {
            // check to see if the point is inside the shape but outside the segment
            if (upDist <= 0) {
                dist = targetVec.magnitude;
            }
            else {
                dist = float.MaxValue;
            }
        }
        // ahead of end point
        else if (lngDist > lineLength) {
            // check to see if the point is inside the shape but outside the segment
            if (upDist <= 0) {
                dist = (_targetPoint - _endPoint).magnitude;
            }
            else {
                dist = float.MaxValue;
            }
        }
        // point is along the line
        else {
            dist = Mathf.Abs(upDist);
        }

        return dist;
    }

}
