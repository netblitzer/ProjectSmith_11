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

        /*
        // Save the component.
        StreamWriter writer;
        // First, the vertices and connections.
        using (writer = File.CreateText(savePath + this.componentName + ".txt")) {
            // Save the name.
            writer.WriteLine(this.componentName);

            // Write how many vertices there are and how many connections there are.
            writer.WriteLine(this.vertices.Count);
            writer.WriteLine(this.connections.Count);
            writer.WriteLine();

            // Save the vertices.
            foreach (Vertex v in this.vertices) {
                v.Save(writer);
            }

            // Save the connections.
            foreach (Connection c in this.connections) {
                writer.WriteLine(this.vertices.IndexOf(c.First));
                writer.WriteLine(this.vertices.IndexOf(c.Second));
                writer.WriteLine((int) c.Edge);
                writer.WriteLine();
            }
        }
        */

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

        // Serialize the object and close the stream.
        bf.Serialize(file, cData);
        file.Close();

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

        /*
        StreamReader reader;
        using (reader = File.OpenText(_path)) {
            // Read in the name.
            this.componentName = reader.ReadLine();
            // Read in vertex and connection counts.
            int vertCount = reader.Read();
            int connectionCount = reader.Read();

            // Load in all the vertices.
            for (int i = 0; i < vertCount; i++) {
                Vertex temp = new Vertex();
                temp.Load(reader);
                this.vertices.Add(temp);
            }

            // Load in all the connections.
            for (int i = 0; i < connectionCount; i++) {
                Connection temp = new Connection();
                temp.SetConnection(this.vertices[reader.Read()], this.vertices[reader.Read()], (EdgeType) reader.Read());
                this.connections.Add(temp);
            }
        }
        */

        return true;
    }
}

[Serializable]
public class ComponentData {
    public string name;

    public List<VertexData> vertData;
    public List<ConnectionData> connData;
}

[Serializable]
public class VertexData {
    public float x;
    public float y;
    public bool locked;
}

[Serializable]
public class ConnectionData {
    public int first;
    public int second;
    public int type;
}
