using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ForgerManager : MonoBehaviour {

    public GameObject weaponRoot;

    public GameObject currentSelectedComponent;

    private List<Component> activeComponents;

    private List<GameObject> activeRenderedComponentObjects;

    private string componentBasePath = "/SaveData/Components/";

    private bool changesSinceLastSave;

    // Use this for initialization
    void Start () {
        // Initialize lists.
        this.activeComponents = new List<Component>();
        this.activeRenderedComponentObjects = new List<GameObject>();
	}
	
	// Update is called once per frame
	void Update () {
		
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
            LoadOption tempLoadOption = temp.GetComponent<LoadOption>();

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

    public string LoadSelectedComponent (string _path, GameObject _component) {
        // If we're not saving or there aren't changes to save, just load up the component.
        Component c = _component.GetComponent<Component>();
        if (c.LoadComponent(_path)) {
            this.changesSinceLastSave = false;

            // Add the component to the current list of components.
            this.activeComponents.Add(c);

            // Create the mesh representation of the component.
            MeshFilter mFilter = _component.GetComponent<MeshFilter>();
            mFilter.mesh = c.componentMesh;

            _component.transform.position = Vector3.zero;
            _component.name = c.componentName;

            // Get the collider and set it.
            MeshCollider mCol = _component.GetComponent<MeshCollider>();
            mCol.sharedMesh = mFilter.mesh;

            // Make it a child of the root object.
            _component.transform.SetParent(this.weaponRoot.transform);

            // Add the mesh to the list.
            this.activeRenderedComponentObjects.Add(_component);

            return "LOADED";
        }

        _component = null;
        return "FAILED";
    }
}
