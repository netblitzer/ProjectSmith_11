using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;

public class Component {

    public List<Vertex> vertices;

    public List<Connection> connections;

    public Mesh componentMesh;

    public string componentName;

    public Component () {
        this.vertices = new List<Vertex>();
        this.connections = new List<Connection>();
        this.componentMesh = new Mesh();
        this.componentName = "Unavailable";
    }

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

    public void SetComponent (List<Vertex> _verts, List<Connection> _conns, Mesh _m, string _name) {
        this.vertices = _verts;
        this.connections = _conns;
        this.componentMesh = _m;
        this.componentName = _name;
    }

    public void ClearComponent () {
        this.vertices.Clear();
        this.connections.Clear();
        this.componentMesh.Clear();
        this.componentName = "Unavailable";
    }

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
            tempData.x = v.Location.x;
            tempData.y = v.Location.y;
            tempData.locked = v.IsVertexLocked;
            vData.Add(tempData);
        }

        // Save each connection to a connectionData object.
        List<ConnectionData> connData = new List<ConnectionData>();
        foreach (Connection c in this.connections) {
            ConnectionData tempData = new ConnectionData();
            tempData.first = this.vertices.IndexOf(c.First);
            tempData.second = this.vertices.IndexOf(c.Second);
            tempData.type = (int) c.Edge;
            connData.Add(tempData);
        }

        // Attach the vertexData and connectionData to the componentData object.
        cData.vertData = vData;
        cData.connData = connData;

        // If we have a mesh, we need to write where it will be located before we close this file stream.
        if (this.componentMesh.vertices.Length != 0) {
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
            temp.Location = new Vector2(tempData.x, tempData.y);
            temp.LockVertex(tempData.locked);
            this.vertices.Add(temp);
        }

        // Load in all the connections.
        for (int i = 0; i < cData.connData.Count; i++) {
            Connection temp = new Connection();
            ConnectionData tempData = cData.connData[i];
            temp.SetConnection(this.vertices[tempData.first], this.vertices[tempData.second], (EdgeType) tempData.type);
            this.connections.Add(temp);
        }

        // See if the mesh data exists.
        if (cData.meshPath != null && File.Exists(cData.meshPath)) {

            // Load in the mesh data now.
            // Create the binary stream.
            bf = new BinaryFormatter();
            file = File.Open(cData.meshPath, FileMode.Open);

            // Read in the mesh data.
            MeshData mData = (MeshData) bf.Deserialize(file);

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
}

[Serializable]
public class ComponentData {
    public ComponentData () {
        this.vertData = new List<VertexData>();
        this.connData = new List<ConnectionData>();
    }

    public string name;
    public string meshPath;

    public List<VertexData> vertData;
    public List<ConnectionData> connData;
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