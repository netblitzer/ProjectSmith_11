using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ForgerManager : MonoBehaviour {

    // The root for viewing the completed weapon in scene.
    public GameObject weaponRoot;

    // The root component that all other components must attach to.
    private Component rootComponent;

    // The currently selected component.
    public Component currentSelectedComponent;
    // Whether we have a component selected or not.
    public bool IsAComponentSelected { get; private set; }

    // The distance the selected component is from the screen.
    public float distanceToSelectedComponent { get; private set; }

    private Weapon createdWeapon;

    private List<Component> activeComponents;

    public float snapDistance = 1f;

    public float unsnapDistance = 2f;

    public float maxSnapAngle = 30f;

    // -----/ Saving Information /----- //

    private string componentBasePath = "/SaveData/Components/";

    private bool changesSinceLastSave;

    // Use this for initialization
    void Start () {
        // Initialize lists.
        this.activeComponents = new List<Component>();

        this.rootComponent = null;
        this.createdWeapon = new Weapon();
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

    public string LoadSelectedComponent (string _path, GameObject _basePrefab, out GameObject _component, bool _nosave) {
        // Create the new gameobject for the component.
        GameObject loadedComponentObjectRoot = GameObject.Instantiate(_basePrefab, Vector3.zero, Quaternion.identity);

        // If we're not saving or there aren't changes to save, just load up the component.
        if (_nosave || !this.changesSinceLastSave) {
            // The component is on the child of the prefab object.
            _component = loadedComponentObjectRoot.transform.GetChild(0).gameObject;
            Component loadedComponent = _component.GetComponent<Component>();

            // Try to load in the component specified.
            if (loadedComponent.LoadComponent(_path)) {
                this.changesSinceLastSave = true;

                // Tell the component about its root object.
                loadedComponent.rootObject = loadedComponentObjectRoot;

                // If this is the first component loaded in, make it the root component.
                if (this.activeComponents.Count == 0) {
                    this.rootComponent = loadedComponent;
                    this.createdWeapon.SetRootComponent(loadedComponent);
                }

                // Add the component to the current list of components.
                this.activeComponents.Add(loadedComponent);

                // Create the mesh representation of the component.
                MeshFilter mFilter = _component.GetComponent<MeshFilter>();
                mFilter.mesh = loadedComponent.componentMesh;

                _component.transform.position = Vector3.zero;
                _component.name = loadedComponent.componentName;

                // Get the collider and set it.
                MeshCollider mCol = _component.GetComponent<MeshCollider>();
                mCol.sharedMesh = mFilter.mesh;

                // Make it a child of the root object.
                loadedComponentObjectRoot.transform.SetParent(this.weaponRoot.transform);

                return "LOADED";
            }
        }

        _component = null;
        return "FAILED";
    }


    public void HandleIdleLeftClick (Ray _mouseRay) {
        // If we have an object selected already, unselect it.
        if (IsAComponentSelected) {
            this.currentSelectedComponent.SetSelected(false);
            this.IsAComponentSelected = false;
            this.currentSelectedComponent = null;
            return;
        }
        // Otherwise find an object to select.
        this.SelectObject(_mouseRay);
    }

    private bool SelectObject (Ray _mouseRay) {

        RaycastHit mouseRayHit;

        if (Physics.Raycast(_mouseRay, out mouseRayHit, 1000f)) {
            // Get the object hit.
            GameObject hit = mouseRayHit.collider.gameObject;

            // Get the object's component.
            Component compObj = hit.GetComponent<Component>();

            // See if the object is a component.
            if (compObj != null) {
                // Reset the last object if there is one that was being hovered.
                if (this.currentSelectedComponent != null)
                    this.currentSelectedComponent.ResetComponentRender();

                // Change the active object.
                //this.currentActiveObject = hit;
                this.currentSelectedComponent = compObj;

                // Set that we have something selected.
                this.IsAComponentSelected = true;

                // Find out how far away from the camera this object was.
                this.distanceToSelectedComponent = Vector3.Distance(_mouseRay.origin, hit.transform.position);

                // If we hit a component, set its color and that it's being selected.
                this.currentSelectedComponent.SetSelected(true);

                return true;
            }
        }

        return false;
    }

    public void DeleteSelectedComponent () {
        // If we have a component selected, delete it and remove it from all references.
        if (this.IsAComponentSelected && this.currentSelectedComponent != null) {
            // If it's the root component, make sure to remove it from our property.
            if (this.rootComponent == this.currentSelectedComponent) {
                this.rootComponent = null;
            }

            this.activeComponents.Remove(this.currentSelectedComponent);

            this.currentSelectedComponent.DeleteComponent();
            this.currentSelectedComponent = null;
            this.IsAComponentSelected = false;
        }
        else {
            // Make sure that the flag for having a component selected is off just in case.
            this.IsAComponentSelected = false;
        }
    }

    public void RotateSelectedComponent (Vector3 _rotation) {
        if (!this.IsAComponentSelected)
            return;

        this.currentSelectedComponent.SetComponentRotation(_rotation);
    }

    public void MoveAndSnapComponent (Vector3 _mousePosition) {
        if (this.IsAComponentSelected && this.currentSelectedComponent != null) {
            // Check if there are other components to attach to.
            if (this.activeComponents.Count > 1) {

                // First, check if the object has any attachment points close to any other component's attachment points.
                // If it does, that means they should snap if their angles are relatively colinear and the distance isn't that great.
                // WARNING: THIS IS VERY UNOPTIMIZED. FOR LARGE AMOUNTS OF ATTACHMENT POINTS, THIS WILL CAUSE SEVERE LAG.

                // Placeholders for the closest point we can attach to.
                AttachmentPoint closestAttachmentPoint = null;
                AttachmentPoint ourClosestAttachmentPoint = null;
                float closestDiancest = float.MaxValue;
                float distance;

                // If the component isn't currently snapped, see if it can snap to any other component.
                if (!this.currentSelectedComponent.IsComponentSnapped) {
                    for (int i = 0; i < this.activeComponents.Count; i++) {

                        // Get each component.
                        Component testComponent = this.activeComponents[i];

                        // Check to make sure we're not comparing to the current selected component. If we are, continue to the next component.
                        if (this.currentSelectedComponent == testComponent)
                            continue;

                        // At this point, we can start comparing attachment points.
                        for (int j = 0; j < this.currentSelectedComponent.attacmenthPoints.Count; j++) {
                            AttachmentPoint currentAttachmentPoint = this.currentSelectedComponent.attacmenthPoints[j];

                            // If the attachment point is filled, skip it and go to the next one.
                            if (currentAttachmentPoint.IsAttached)
                                continue;

                            for (int k = 0; k < testComponent.attacmenthPoints.Count; k++) {
                                AttachmentPoint testAPoint = testComponent.attacmenthPoints[k];

                                // Check to see if the test point is already filled and skip it if it is.
                                if (testAPoint.IsAttached)
                                    continue;

                                // Get the angle between the two attachment points.
                                float ang = Vector3.Angle(currentAttachmentPoint.GetWorldDirection(), testAPoint.GetWorldDirection());

                                // See if the points are within snapping alignment.
                                if (ang > 180f - this.maxSnapAngle) {
                                    // Compare the distances to see if they're within snapping distance.
                                    if ((distance = Vector3.Distance(currentAttachmentPoint.GetWorldPosition(), testAPoint.GetWorldPosition())) < this.snapDistance) {
                                        // If we've made it here, we have points that we can snap together.
                                        if (distance < closestDiancest) {
                                            closestDiancest = distance;
                                            closestAttachmentPoint = testAPoint;
                                            ourClosestAttachmentPoint = currentAttachmentPoint;
                                        }
                                    }
                                }
                            }
                        }
                    } // End check for points.

                    // If we found a point to attach to, snap the components together.
                    if (closestAttachmentPoint != null) {
                        this.currentSelectedComponent.SnapComponent(ourClosestAttachmentPoint, closestAttachmentPoint);
                    }
                    else {
                        // Otherwise, move the component to the mouse.
                        this.currentSelectedComponent.SetComponentPosition(_mousePosition);
                    }
                } // End if component is unsnapped.
                else {
                    // If we're already attached to something, see if moving to the mouse would cause us to attach to something else.
                    Vector3 originalComponentPosition = this.currentSelectedComponent.transform.position;
                    this.currentSelectedComponent.SetComponentPosition(_mousePosition);

                    for (int i = 0; i < this.activeComponents.Count; i++) {

                        // Get each component.
                        Component testComponent = this.activeComponents[i];

                        // Check to make sure we're not comparing to the current selected component. If we are, continue to the next component.
                        if (this.currentSelectedComponent == testComponent)
                            continue;

                        // At this point, we can start comparing attachment points.
                        for (int j = 0; j < this.currentSelectedComponent.attacmenthPoints.Count; j++) {
                            AttachmentPoint currentAttachmentPoint = this.currentSelectedComponent.attacmenthPoints[j];

                            // If the attachment point is filled, skip it and go to the next one.
                            if (currentAttachmentPoint.IsAttached)
                                continue;

                            for (int k = 0; k < testComponent.attacmenthPoints.Count; k++) {
                                AttachmentPoint testAPoint = testComponent.attacmenthPoints[k];

                                // Check to see if the test point is already filled and skip it if it is.
                                if (testAPoint.IsAttached)
                                    continue;

                                // Get the angle between the two attachment points.
                                float ang = Vector3.Angle(currentAttachmentPoint.GetWorldDirection(), testAPoint.GetWorldDirection());

                                // See if the points are within snapping alignment.
                                if (ang > 180f - this.maxSnapAngle) {
                                    // Compare the distances to see if they're within snapping distance.
                                    if ((distance = Vector3.Distance(currentAttachmentPoint.GetWorldPosition(), testAPoint.GetWorldPosition())) < this.snapDistance) {
                                        // If we've made it here, we have points that we can snap together.
                                        if (distance < closestDiancest) {
                                            closestDiancest = distance;
                                            closestAttachmentPoint = testAPoint;
                                            ourClosestAttachmentPoint = currentAttachmentPoint;
                                        }
                                    }
                                }
                            }
                        }
                    } // End check for points.

                    // If we found a point to attach to and we're not already attached to it, snap the components together.
                    if (closestAttachmentPoint != null && (closestAttachmentPoint != this.currentSelectedComponent.parentAttachmentPoint && ourClosestAttachmentPoint != this.currentSelectedComponent.rootAttachmentPoint)) {
                        // First unsnap the component to ensure all connections are broken to the originally snapped component.
                        this.currentSelectedComponent.UnsnapComponent(this.weaponRoot);

                        // Snap to the new component.
                        this.currentSelectedComponent.SnapComponent(ourClosestAttachmentPoint, closestAttachmentPoint);
                    }
                    else {
                        // See if we should unsnap the component instead.

                        // Get the distance between the mouse and the current selected component.
                        distance = Vector3.Distance(_mousePosition, originalComponentPosition);
                        if (distance > this.unsnapDistance) {
                            // Unsnap the component.
                            this.currentSelectedComponent.UnsnapComponent(this.weaponRoot);
                            // The component has already moved to the mouse in this scenario so we don't need to move it.
                        }
                        else {
                            // If we didn't find anything new to snap to and we're not far enough away to unsnap, move back
                            //  to the original location.
                            this.currentSelectedComponent.SetComponentPosition(originalComponentPosition);
                        }
                    }
                } // End if the component was already snapped.
            } // End if there are more than 1 active components.
            else {
                // If there's only one component, move it to the mouse.
                this.currentSelectedComponent.SetComponentPosition(_mousePosition);
            }
        }

    } // End MoveAndSnapComponent function.


} // End file.
