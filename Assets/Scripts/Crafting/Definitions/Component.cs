using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class Component : MonoBehaviour {

    // The empty root that this component's real object is attached to.
    //  This is so we can nest objects without worrying about matrix transforms.
    public GameObject rootObject;

    public string componentName;

    public Mesh componentMesh;


    // -----/ Design Mode Parameters /-----

    public List<Vertex> vertices;

    public List<Connection> connections;


    // -----/ Forge Mode Parameters /-----

    // The list of all the attachment points that this component has.
    public List<AttachmentPoint> attacmenthPoints;

    // The list of the physical representations of the attachment points this component has.
    public List<GameObject> attachmentPointObjects;

    // The attachment point on the parent that this component is attached to.
    public AttachmentPoint parentAttachmentPoint;

    // The attachment point on this component that is the root of its hierarchy.
    public AttachmentPoint rootAttachmentPoint;

    // The original color that this object was when it was instantiated.
    private Color originalColor;

    // The ForgerUI in the scene to get the colors from.
    private ForgerUI forgeUI;

    // Whether this object is currently locked (can't be moved, manipulated, deleted).
    public bool IsObjectLocked;

    // Whether this object is being hovered over by the mouse.
    public bool IsHovered;

    // Whether this object is currently selected by the mouse.
    public bool IsSelected;

    public bool IsComponentSnapped;

    // The rotation that the component wants to be when not snapped.
    public Vector3 currentSetRotation;

    // The current rotation that things like snapping or parents are assigning to this component.
    public Vector3 currentRealRotation;

    // The parent component that this component is attached to.
    private Component parentComponent;

    // The child component(s) that are attached to this componoent.
    private List<Component> childComponents;


    // Function to initialize variables and lists.
    private void Init () {
        if (this.vertices == null) {
            // Initialize design lists.
            this.vertices = new List<Vertex>();
            this.connections = new List<Connection>();

            // Initialize forger lists.
            this.attacmenthPoints = new List<AttachmentPoint>();
            this.childComponents = new List<Component>();
            this.componentName = "New Component";
        }
    }

    public Component () {
        this.Init();
    }

    void Awake () {
        this.Init();

        // To play safe with Unity's scripting API, these need to be called in Awake or Start, not in a constructor.
        this.componentMesh = new Mesh();
        this.FirstRender();
    }


    // -----/ Design Mode Functions /----- //
    #region DesignModeFunctions

    public void SetName (string _name) {
        this.componentName = _name;
    }

    public void SetMesh (Mesh _m) {
        this.componentMesh = _m;
    }

    public void SetVertices (List<Vertex> _verts) {
        this.vertices = _verts;
    }

    public void SetConnections (List<Connection> _conns) {
        this.connections = _conns;
    }

    public void SetAttachmentPoints (List<AttachmentPoint> _attach) {
        this.attacmenthPoints = _attach;
    }

    public void SetComponent (List<Vertex> _verts, List<Connection> _conns, Mesh _m, string _name) {
        this.vertices = _verts;
        this.connections = _conns;
        this.componentMesh = _m;
        this.componentName = _name;
    }

    public void ClearComponent () {
        this.vertices.Clear();
        this.connections.Clear();
        if (this.componentMesh != null)
            this.componentMesh.Clear();
        else
            this.componentMesh = new Mesh();

        this.componentName = "Unavailable";
    }

    #endregion

    // -----/ Forging Mode Functions /-----
    #region ForgingModeFunctions

    /// <summary>
    /// Sets up the component when it's first rendered into the forge.
    /// </summary>
    public void FirstRender () {
        this.originalColor = this.gameObject.GetComponent<Renderer>().material.color;

        this.currentSetRotation = Vector3.zero;
    }

    /// <summary>
    /// Sets the ForgerUI to be the current scene's (variable passed in).
    /// </summary>
    /// <param name="_ui">The ForgerUI in the Forge scene.</param>
    public void SetUI (ForgerUI _ui) {
        this.forgeUI = _ui;
    }

    /// <summary>
    /// Creates objects at each attachment position and sets them to be children of the component's object.
    /// </summary>
    /// <param name="_attachPointPrefab">The prefab that the attachment points should follow.</param>
    public void RenderAttachmentPoints (GameObject _attachPointPrefab) {
        // Go through each attachment point and create a new object at that location.
        foreach (AttachmentPoint ap in this.attacmenthPoints) {
            GameObject tempPoint = GameObject.Instantiate(_attachPointPrefab);

            // Add the object to the list.
            this.attachmentPointObjects.Add(tempPoint);

            // Set the name.
            tempPoint.name = this.componentName + ": Attachment Point #" + this.attachmentPointObjects.Count;

            // Set the object as a child of the component.
            tempPoint.transform.SetParent(this.gameObject.transform);

            // Set the properties of the attachment point's object.
            tempPoint.transform.position = ap.location + ap.normalDirection / 1.5f;
            tempPoint.transform.rotation = Quaternion.LookRotation(new Vector3(ap.normalDirection.y, -ap.normalDirection.x), ap.normalDirection);
        }
    }

    public void LockComponentRender (bool _locked) {
        this.IsObjectLocked = _locked;
    }

    public void SetHovered (bool _hovered) {
        this.IsHovered = _hovered;
        // If we're hovered but not selected, change the color to the hover color.
        if (this.IsHovered && !this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.forgeUI.hoverColor;

        // If we're not hovered or selected, reset the color.
        if (!this.IsHovered && !this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void SetSelected (bool _selected) {
        this.IsSelected = _selected;

        // If we're selected, change the color to the selected color.
        if (this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.forgeUI.selectedColor;

        // If we're still hovered but no longer selected, change to the hovered color.
        if (!this.IsSelected && this.IsHovered)
            this.gameObject.GetComponent<Renderer>().material.color = this.forgeUI.hoverColor;

        // If we're not hovered or selected, reset the color.
        if (!this.IsHovered && !this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void ResetComponentRender () {
        this.IsSelected = false;
        this.IsHovered = false;

        this.gameObject.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void SetComponentRotation (Vector3 _rotation) {
        // Add to the rotation.
        this.currentSetRotation += _rotation;

        // Corrent the rotation to be within -180 to 180.
        this.currentSetRotation = this.CorrectEulerAngle(this.currentSetRotation);

        // Rotate the component object if it's not snapped.
        if (!this.IsComponentSnapped) {
            this.RotateComponent(this.currentSetRotation);
        }
    }

    public void RotateComponent (Vector3 _rotation) {
        // Set the real rotation of the component.
        this.currentRealRotation = _rotation;
        this.rootObject.transform.rotation = Quaternion.Euler(this.currentRealRotation);
    }

    private void RotateComponent (Vector3 _axis, float _angle) {
        this.rootObject.transform.Rotate(_axis, _angle);
        this.currentRealRotation = this.rootObject.transform.eulerAngles;
    }

    public void SetComponentPosition (Vector3 _newPosition) {
        Vector3 moveAmount = this.rootObject.transform.position - _newPosition;
        this.MoveComponent(moveAmount);
    }

    public void MoveComponent (Vector3 _moveAmount) {
        this.rootObject.transform.position -= _moveAmount;
    }

    public int GetTotalChildCount () {
        int childCount = this.childComponents.Count;
        
        // Go through each child and add to the count.
        foreach (Component child in this.childComponents) {
            childCount += child.GetTotalChildCount();
        }

        return childCount;
    }

    public int GetLocalChildCount () {
        return this.childComponents.Count;
    }

    public List<Component> GetTotalChildren () {
        List<Component> children = this.childComponents;

        // Go through each child and add to the list.
        foreach (Component child in this.childComponents) {
            children.AddRange(child.GetTotalChildren());
        }

        return children;
    }

    public List<Component> GetChildren () {
        return this.childComponents;
    }

    public void AddChildComponent (Component _child) {
        this.childComponents.Add(_child);
    }

    public void RemoveChildComponent (Component _child) {
        if (this.childComponents.Contains(_child)) {
            this.childComponents.Remove(_child);
        }
    }

    public void SnapComponent (AttachmentPoint _ourAttach, AttachmentPoint _otherAttach) {

        // Find the normal vector to the plane that's created by the directions of the two attachment points.
        Vector3 planeNormal = Vector3.Cross(_otherAttach.GetWorldDirection(), _ourAttach.GetWorldDirection());

        // Find the angle the component needs to rotate.
        float angle = Vector3.Angle(_ourAttach.GetWorldDirection(), _otherAttach.GetWorldDirection());

        // Rotate the component to align with the attachment point.
        this.RotateComponent(planeNormal, (180f - angle));

        // Move the component by the current difference between where the attachment position SHOULD be.
        Vector3 moveAmount = (_ourAttach.GetWorldPosition() - _otherAttach.GetWorldPosition());
        this.MoveComponent(moveAmount);

        // Set the attachment points as filled.
        _ourAttach.SetAttached(true);
        _otherAttach.SetAttached(true);

        // Set our connections.
        this.parentComponent = _otherAttach.RootComponent;
        this.parentAttachmentPoint = _otherAttach;
        this.rootAttachmentPoint = _ourAttach;
        this.IsComponentSnapped = true;

        // Make sure the other component knows we're now attached to it.
        _otherAttach.RootComponent.AddChildComponent(this);

        // Finally, add our root object as a child of theirs.
        this.rootObject.transform.SetParent(_otherAttach.RootComponent.transform);
    }

    public void UnsnapComponent (GameObject _weaponRoot) {
        // First, remove our transform from being a child.
        this.rootObject.transform.SetParent(_weaponRoot.transform);

        // Reset the rotation to before it was snapped.
        this.RotateComponent(this.currentSetRotation);

        // Set the attachment points as filled.
        this.rootAttachmentPoint.SetAttached(false);
        this.parentAttachmentPoint.SetAttached(false);

        // Make sure the other component knows we've unattached to it.
        this.parentComponent.RemoveChildComponent(this);

        // Reset our connections.
        this.parentComponent = null;
        this.parentAttachmentPoint = null;
        this.rootAttachmentPoint = null;
        this.IsComponentSnapped = false;
    }

    public void DeleteComponent () {
        foreach (Component child in this.childComponents) {
            child.DeleteComponent();
        }

        Destroy(this.rootObject);
    }

    #endregion


    // -----/ Saving and Loading /-----

    public string SaveComponent (bool _overwrite, string _path) {
        string savePath = _path;

        // See if the directory(s) exist first.
        if (!Directory.Exists(Application.persistentDataPath + _path)) {
            Directory.CreateDirectory(Application.persistentDataPath + _path);
        }

        // See if the file already exists.
        bool existsAlready = File.Exists(Application.persistentDataPath + savePath + this.componentName + ".dat");

        // If it does and we're overwriting, delete the file first.
        if (existsAlready && _overwrite) {
            File.Delete(Application.persistentDataPath + savePath + this.componentName + ".dat");

            // Mark that the file doesn't exist anymore.
            existsAlready = false;
        }
        else if (existsAlready && !_overwrite) {
            return "ALREADYEXISTS";
        }

        // Create the binary stream.
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Open(Application.persistentDataPath + savePath + this.componentName + ".dat", FileMode.Create);

        // Create the component data.
        ComponentData cData = new ComponentData();
        cData.name = this.componentName;

        // Save each vertex to a vertexData object.
        List<VertexData> vData = new List<VertexData>();
        foreach (Vertex v in this.vertices) {
            VertexData tempData = new VertexData();
            tempData.Location = v.Location;
            tempData.Locked = v.IsVertexLocked;
            vData.Add(tempData);
        }

        // Save each connection to a connectionData object.
        List<ConnectionData> connData = new List<ConnectionData>();
        foreach (Connection c in this.connections) {
            ConnectionData tempData = new ConnectionData();
            tempData.First = this.vertices.IndexOf(c.First);
            tempData.Second = this.vertices.IndexOf(c.Second);
            tempData.Type = (int) c.Edge;
            connData.Add(tempData);
        }

        // Save each attachment point to an ComponentAttachmentPointData object.
        List<AttachmentPointData> attachData = new List<AttachmentPointData>();
        foreach (AttachmentPoint ap in this.attacmenthPoints) {
            AttachmentPointData tempData = new AttachmentPointData();
            tempData.Location = ap.location;
            tempData.Normal = ap.normalDirection;
            tempData.Size = ap.attachmentSize;
            attachData.Add(tempData);
        }

        // Attach the vertexData and connectionData to the componentData object.
        cData.vertData = vData;
        cData.connData = connData;
        cData.attachData = attachData;

        // If we have a mesh, we need to write where it will be located before we close this file stream.
        if (this.componentMesh != null && this.componentMesh.vertices.Length != 0) {
            cData.meshPath = Application.persistentDataPath + savePath + "/Meshes/" + this.componentName + ".dat";
        }
        else
            cData.meshPath = null;

        // Serialize the object and close the stream.
        bf.Serialize(file, cData);
        file.Close();

        // If we have a mesh, we will write its data out.
        if (this.componentMesh.vertices.Length != 0) {
            // Save the mesh.
            if (!Directory.Exists(Application.persistentDataPath + _path + "Meshes/")) {
                Directory.CreateDirectory(Application.persistentDataPath + _path + "Meshes/");
            }

            // Now to write out the mesh data.
            file = File.Open(Application.persistentDataPath + savePath + "Meshes/" + this.componentName + ".dat", FileMode.Create);

            MeshData mData = new MeshData();
            mData.meshIndices.AddRange(this.componentMesh.GetIndices(0));
            // Convert the normal vec3s to serialized vec3s.
            Vector3[] verts = this.componentMesh.vertices;
            foreach (Vector3 v in verts)
                mData.meshVertices.Add(v);
            
            // Serialize the object and close the stream.
            bf.Serialize(file, mData);
            file.Close();
        }
        else {
            // Otherwise, we will leave the meshPath as null.
            cData.meshPath = null;
        }

        return "SAVED";
    }

    public bool LoadComponent (string _path) {
        // See if the file exists. If it doesn't, we can't load it.
        if (!File.Exists(_path)) {
            return false;
        }

        // If the file exists, load it all in.

        // First, clear the component.
        this.ClearComponent();
        
        // Create the binary stream.
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file = File.Open(_path, FileMode.Open);

        // Create the componentData and load it in.
        ComponentData cData = (ComponentData) bf.Deserialize(file);

        // Close the file.
        file.Close();

        // Load in basic properties.
        this.componentName = cData.name;
        
        // Load in all the vertices.
        for (int i = 0; i < cData.vertData.Count; i++) {
            Vertex temp = new Vertex();
            VertexData tempData = cData.vertData[i];
            temp.Location = tempData.Location;
            temp.LockVertex(tempData.Locked);
            this.vertices.Add(temp);
        }

        // Load in all the connections.
        for (int i = 0; i < cData.connData.Count; i++) {
            Connection temp = new Connection();
            ConnectionData tempData = cData.connData[i];
            temp.SetConnection(this.vertices[tempData.First], this.vertices[tempData.Second], (EdgeType) tempData.Type);
            this.connections.Add(temp);
        }

        // Load in all the attachment points.
        for (int i = 0; i < cData.attachData.Count; i++) {
            AttachmentPoint temp = new AttachmentPoint();
            AttachmentPointData tempData = cData.attachData[i];
            temp.SetLocation(tempData.Location);
            temp.SetNormalDirection(tempData.Normal);
            temp.SetComponent(this);
            this.attacmenthPoints.Add(temp);
        }

        // See if the mesh data exists.
        if (cData.meshPath != null && File.Exists(cData.meshPath)) {

            // Load in the mesh data now.
            // Create the binary stream.
            bf = new BinaryFormatter();
            file = File.Open(cData.meshPath, FileMode.Open);

            // Read in the mesh data.
            MeshData mData = (MeshData) bf.Deserialize(file);

            // Close the file.
            file.Close();

            // Create a mesh and pass it in the data.
            Mesh m = new Mesh();
            // Convert the serialized vec3s to normal vec3s.
            List<Vector3> verts = new List<Vector3>();
            foreach (SerializableVector3 v in mData.meshVertices)
                verts.Add(v);

            m.SetVertices(verts);
            m.SetTriangles(mData.meshIndices, 0);

            // Calculate additional parts of the mesh.
            m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();

            // Set the component's mesh.
            this.componentMesh = m;
        }

        return true;
    }


    // -----/ Assistance Functions /-----

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


// -----/ Serialization Data Objects /-----
[Serializable]
public class ComponentData {
    public ComponentData () {
        this.vertData = new List<VertexData>();
        this.connData = new List<ConnectionData>();
        this.attachData = new List<AttachmentPointData>();
    }

    public string name;
    public string meshPath;

    public List<VertexData> vertData;
    public List<ConnectionData> connData;
    public List<AttachmentPointData> attachData;
}

[Serializable]
public class MeshData {
    public MeshData () {
        this.meshIndices = new List<int>();
        this.meshVertices = new List<SerializableVector3>();
    }

    public List<int> meshIndices;
    public List<SerializableVector3> meshVertices;
}