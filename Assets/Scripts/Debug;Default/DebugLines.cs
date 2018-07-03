using UnityEngine;
using System.Collections;

public class DebugLines : MonoBehaviour {

    private Queue lineRenderQueue;
    
    private Material lineMaterial;

    /// <summary>
    /// Create a static instance (singleton) of this script that's publicly available.
    /// </summary>
    private static DebugLines debugLines;
    public static DebugLines Instance
    {
        get
        {
            if (!debugLines) {
                debugLines = FindObjectOfType<DebugLines>();

                if (!debugLines) {
                    Debug.LogError("A DebugLines is required in this scene.");
                }
                else {
                    debugLines.Init();
                }
            }

            return debugLines;
        }
    }

    void Init () {
        this.lineRenderQueue = new Queue();
        this.CreateLineMaterial();
    }

    // Use this for initialization
    void Start () {
        if (debugLines != null && debugLines != this) {
            Destroy(this.gameObject);
        }
        debugLines = this;

        // Make sure this object persists between scene changes
        /// TODO: Make this make sure the root parent doesn't get destroyed.
        //DontDestroyOnLoad(this.transform.parent.gameObject);
        GameObject child = this.gameObject;
        while(child.transform.parent != null) {
            child = child.transform.parent.gameObject;
        }
        DontDestroyOnLoad(child);

        this.Init();
    }

    void CreateLineMaterial () {
        // Unity has a built-in shader that is useful for drawing
        // simple colored things.
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        this.lineMaterial = new Material(shader);
        this.lineMaterial.hideFlags = HideFlags.HideAndDontSave;
        // Turn on alpha blending
        this.lineMaterial.SetInt("_SrcBlend", (int) UnityEngine.Rendering.BlendMode.SrcAlpha);
        this.lineMaterial.SetInt("_DstBlend", (int) UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        // Turn backface culling off
        this.lineMaterial.SetInt("_Cull", (int) UnityEngine.Rendering.CullMode.Off);
        // Turn off depth writes
        this.lineMaterial.SetInt("_ZWrite", 0);
    }

    public void ClearQueue () {
        this.lineRenderQueue.Clear();
    }

    // Function to add new lines to render
    public void AddLine (Vector3 _startingPoint, Vector3 _endPoint) {
        this.AddLine(_startingPoint, _endPoint, Color.black);
    }

    public void AddLine (Vector3 _startingPoint, Vector3 _endPoint, Color _color) {
        Line l = new Line(_startingPoint, _endPoint, _color);
        this.lineRenderQueue.Enqueue(l);
    }

    void OnRenderObject () {
        Queue q = new Queue();
        while (this.lineRenderQueue.Count > 0) {
            Line temp = (Line) this.lineRenderQueue.Dequeue();
            //q.Enqueue(temp);

            this.lineMaterial.SetPass(0);

            GL.Begin(GL.LINES);
            GL.Color(temp.color);
            GL.Vertex(temp.start);
            GL.Vertex(temp.end);
            GL.End();
        }
        //this.lineRenderQueue = q;
    }

    private class Line {
        public Vector3 start;
        public Vector3 end;
        public Color color;

        public Line (Vector3 _start, Vector3 _end, Color _color) {
            start = _start;
            end = _end;
            color = _color;
        }
    }
}