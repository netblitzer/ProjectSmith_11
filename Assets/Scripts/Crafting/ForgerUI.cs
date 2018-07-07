using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class ForgerUI : MonoBehaviour, IUIManager {

    public ForgerManager manager;


    // -----/ Internal Mode Information /-----

    // What mode the forging UI is currently in.
    public ForgerMode currentMode;

    private List<Component> components;

    public GameObject baseRenderedComponentPrefab;

    public GameObject baseAttachmentPointPrefab;

    // Whether the mouse is currently hovering over the design space.
    private bool isCurrentlyHovering;

    private bool isLeftMouseDown;

    private bool isRightMouseDown;


    // -----/ Hovering and Selection Information /-----

    // The object that is currently being hovered over by the mouse (if any) or selected/dragged.
    public GameObject currentActiveObject;

    // Whether there is currently an object selected and being manipulated.
    public bool IsAnObjectSelected;

    // How far away the object was from the camera when it was selected, OR how far to keep the object from the camera.
    public float selectedDistFromCamera;

    public Color hoverColor;

    public Color indicatorHoverColor;

    public Color selectedColor;

    public Color invalidColor;


    // -----/ Camera Control Information /-----

    public Camera mainSceneCamera;

    private Vector3 cameraRotation;

    private Vector3 cameraLookPosition;

    // The last mouse position on the screen.
    private Vector3 lastMousePosition;

    // The amount the mouse moved in the last frame while the right mouse was held.
    private Vector3 mouseMoveAmount;

    // The cube in the top right corner to indicate where the camera is looking.
    public GameObject cameraDirectionIndicator;


    // -----/ UI Saving Elements /-----

    public GameObject fileAlreadyExistsParent;

    public Text fileAlreadyExistsText;

    public GameObject loadFileMenuParent;

    public ScrollRect loadFileScrollObject;

    public GameObject loadFileOptionPrefab;

    public GameObject loadFileUnsavedChangesParent;

    private LoadOption lastLoadOptionClicked;


    // Use this for initialization
    void Start () {
        // Instantiate lists.
        this.components = new List<Component>();

        // Make sure all menus are closed.
        this.ToggleLoadMenu(false);

        // Set up camera properties.
        this.cameraLookPosition = new Vector3(0, 0, 0);
        this.cameraRotation = mainSceneCamera.transform.rotation.eulerAngles;
        // Place the camera indicator in the correct location.
        float frustumHeight = 1.0f * Mathf.Tan(this.mainSceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 indicatorPos = new Vector3(frustumHeight * this.mainSceneCamera.aspect, frustumHeight, 1f);
        float minScreenDimension = Mathf.Min(indicatorPos.x, indicatorPos.y);
        indicatorPos.x -= minScreenDimension * 0.15f;
        indicatorPos.y -= minScreenDimension * 0.15f;
        this.cameraDirectionIndicator.transform.localPosition = indicatorPos;
        this.cameraDirectionIndicator.transform.localScale = new Vector3(minScreenDimension * 0.075f, minScreenDimension * 0.075f, minScreenDimension * 0.075f);
    }
	
	// Update is called once per frame
	void Update () {

        if (this.isCurrentlyHovering && !this.IsAnObjectSelected) {
            this.HandleMouseHovering(null);
        }

        // If we currently have an object selected and we're manipulating it.
        if (this.IsAnObjectSelected) {
            // Check if we're deleting this object first.
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) {
                // If the delete or backspace key is pressed, destroy the object and remove it from any instances.
                Component comp = this.currentActiveObject.GetComponent<Component>();
                this.components.Remove(comp);
                Destroy(this.currentActiveObject);
                this.currentActiveObject = null;
                this.IsAnObjectSelected = false;
            }
            else {
                // Otherwise, we'll manipulate the object according to its current position and status.

                // We don't have to worry about snapping if there's not more than 1 component currently out.
                if (this.components.Count > 1) {
                    // First, check if the object has any attachment points close to any other component's attachment points.
                    // If it does, that means they should snap if their angles are relatively colinear and the distance isn't that great.
                    // WARNING: THIS IS VERY UNOPTIMIZED. FOR LARGE AMOUNTS OF ATTACHMENT POINTS, THIS WILL CAUSE SEVERE LAG.

                    // Get the component attached to our current object.
                    Component currentComp = this.currentActiveObject.GetComponent<Component>();

                    // Make sure the currentComp exists and then go through each component in our list.
                    if (currentComp != null) {
                        // Placeholders for the closest point we can attach to.
                        AttachmentPoint closestAPoint = null;
                        AttachmentPoint ourClosestAPoint = null;
                        float closestDist = float.MaxValue;

                        for (int i = 0; i < this.components.Count; i++) {

                            // Get each component.
                            Component testComp = this.components[i];

                            // Check to make sure we're not comparing to the current selected component. If we are, break out of the loop.
                            if (currentComp == testComp)
                                break;

                            // At this point, we can start comparing attachment points.
                            for (int j = 0; j < currentComp.attachPoints.Count; j++) {
                                AttachmentPoint currAPoint = currentComp.attachPoints[j];

                                for (int k = 0; k < testComp.attachPoints.Count; k++) {
                                    AttachmentPoint testAPoint = testComp.attachPoints[k];

                                    float ang = Vector3.Angle(currAPoint.GetWorldDirection(), testAPoint.GetWorldDirection());
                                    // Compare the two objects angles to see if they're relatively aligned.
                                    if (ang > 150f) {
                                        float dist;
                                        // Compare the distances to see if they're relatively close.
                                        if ((dist = Vector3.Distance(currAPoint.GetWorldPosition(), testAPoint.GetWorldPosition())) < 1f) {
                                            // If we've made it here, we have points that we can snap together potentially.
                                            if (dist < closestDist) {
                                                closestDist = dist;
                                                closestAPoint = testAPoint;
                                                ourClosestAPoint = currAPoint;
                                            }
                                        }
                                    }
                                }
                            }
                        } // End check for points.

                        // If we found a point, we should now snap the current selected object to that point.
                        if (closestAPoint != null) {
                            // Move the object by the current difference between where the attachment position SHOULD be.
                            this.currentActiveObject.transform.position -= (ourClosestAPoint.GetWorldPosition() - closestAPoint.GetWorldPosition());
                        }
                        else {
                            // Otherwise, just place the object where the mouse is.
                            this.currentActiveObject.transform.position = this.mainSceneCamera.transform.position 
                                + this.mainSceneCamera.ScreenPointToRay(Input.mousePosition).direction * this.selectedDistFromCamera;
                        }
                    }
                }
                // If we don't have more than 1 component, just move it to where the mouse is.
                else { 
                    this.currentActiveObject.transform.position = this.mainSceneCamera.transform.position + this.mainSceneCamera.ScreenPointToRay(Input.mousePosition).direction * this.selectedDistFromCamera;
                }
            }
        }

        // Handle camera movement.
        if (this.isRightMouseDown) {
            // Get the movement and rotation amounts.
            Vector3 movePos = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            Vector3 rot = new Vector3(this.mouseMoveAmount.x / 10f, -this.mouseMoveAmount.y / 10f, 0);

            // Rotate the camera around its current look position.
            this.mainSceneCamera.transform.RotateAround(this.cameraLookPosition, this.mainSceneCamera.transform.up, rot.x);
            this.mainSceneCamera.transform.RotateAround(this.cameraLookPosition, this.mainSceneCamera.transform.right, rot.y);

            // Translate the camera.
            // NOTES: Currently disabled until there's more references for users to understand.
            //this.mainSceneCamera.transform.Translate(movePos);

            // Clamp the vertical camera rotation.
            this.cameraRotation.x = Mathf.Clamp(this.cameraRotation.x, -85f, 85f);

            // Rotate the indicator to continue to be "level".
            this.cameraDirectionIndicator.transform.rotation = Quaternion.identity;

            // Reset the mouse movement amount.
            this.mouseMoveAmount = Vector3.zero;
        }
    }

    // -----/ Mouse Events /-----
    #region MouseEvents

    public void HandleLeftClick (PointerEventData _eventData) {

        Vector2 worldPos = this.mainSceneCamera.ScreenToWorldPoint(_eventData.position);

        switch (this.currentMode) {
            case ForgerMode.MOVE:

                break;
            case ForgerMode.SCALE:

                break;
            case ForgerMode.ROTATE:

                break;
            case ForgerMode.IDLE:
            default:
                // If we're in idle and click without anything selected, check if we're selecting a component.
                if (!this.IsAnObjectSelected) {
                    this.SelectObject(this.mainSceneCamera.ScreenPointToRay(_eventData.position));
                }
                else {
                    // If we're clicking with something already selected, we should unselect it.
                    this.UnselectObject();
                }

                break;
        }
    }

    public void HandleRightClick (PointerEventData _eventData) {
        //throw new NotImplementedException();
    }

    public void HandleMouseHovering (PointerEventData _eventData) {
        Vector2 screenPos;
        if (_eventData != null)
            screenPos = _eventData.position;
        else
            screenPos = Input.mousePosition;

        // Find out where our mouse is currently aiming.
        Ray mouseRay = this.mainSceneCamera.ScreenPointToRay(screenPos);
        RaycastHit mouseRayHit;

        if (Physics.Raycast(mouseRay, out mouseRayHit, 1000f)) {
            // Get the object hit.
            GameObject hit = mouseRayHit.collider.gameObject;

            // See if the object is either a component or the direction indicator.
            if (hit.GetComponent<Component>() != null || hit == this.cameraDirectionIndicator) {
                // Get the object component (if any).
                Component compObj = hit.GetComponent<Component>();
                // Make sure we're not hovering something that is already active.
                if (hit != this.currentActiveObject) {
                    // Reset the last object if this is a different object.
                    if (this.currentActiveObject != null) {
                        // Get the component of the last object.
                        Component lastCompObj = this.currentActiveObject.GetComponent<Component>();
                        // The only option without the component object is the direction indicator.
                        if (lastCompObj == null)
                            this.cameraDirectionIndicator.GetComponent<Renderer>().material.color = Color.white;
                        else {
                            // Otherwise, reset the component's hover status.
                            lastCompObj.SetHovered(false);
                        }
                    }

                    // Change the new hovered.
                    this.currentActiveObject = hit;

                    // If what we hit was the indicator, give it a different color.
                    if (this.currentActiveObject == this.cameraDirectionIndicator)
                        this.currentActiveObject.GetComponent<Renderer>().material.color = this.indicatorHoverColor;
                    else {
                        // If we hit a component, set its color and that it's being hovered.
                        compObj.SetHovered(true);
                    }
                }
            }
        }
        else {
            // If we're no longer hovering over anything, reset the last hovered object if it exists and it's not selected.
            if (this.currentActiveObject != null) {
                if (this.currentActiveObject == this.cameraDirectionIndicator)
                    // If it's the camera indicator, just change the color back.
                    this.currentActiveObject.GetComponent<Renderer>().material.color = Color.white;
                else {
                    // If it's a component, set it to not hovered and reset the color.
                    Component compObj = this.currentActiveObject.GetComponent<Component>();
                    compObj.SetHovered(false);
                }

                if (!IsAnObjectSelected)
                    this.currentActiveObject = null;
            }
        }
    }

    public void OnPointerDown (PointerEventData _eventData) {
        if (_eventData.button == PointerEventData.InputButton.Left)
            this.isLeftMouseDown = true;

        if (_eventData.button == PointerEventData.InputButton.Right)
            this.isRightMouseDown = true;
    }

    public void OnPointerUp (PointerEventData _eventData) {
        if (_eventData.button == PointerEventData.InputButton.Left)
            this.isLeftMouseDown = false;

        if (_eventData.button == PointerEventData.InputButton.Right)
            this.isRightMouseDown = false;
    }

    public void EnableHovering (bool _enabled) {
        this.isCurrentlyHovering = _enabled;
    }

    public void OnBeginDrag (PointerEventData _eventData) {
        if (this.isRightMouseDown) {
            this.mouseMoveAmount = Input.mousePosition - this.lastMousePosition;

            this.lastMousePosition = Input.mousePosition;
        }
    }

    public void OnMouseDrag (PointerEventData _eventData) {
        if (this.isRightMouseDown) {
            this.mouseMoveAmount = Input.mousePosition - this.lastMousePosition;

            this.lastMousePosition = Input.mousePosition;
        }
    }

    public void OnEndDrag (PointerEventData _eventData) {
        if (this.isRightMouseDown) {
            this.mouseMoveAmount = Input.mousePosition - this.lastMousePosition;

            this.lastMousePosition = Input.mousePosition;
        }
    }

    #endregion

    private bool SelectObject (Ray _mouseRay) {

        RaycastHit mouseRayHit;

        if (Physics.Raycast(_mouseRay, out mouseRayHit, 1000f)) {
            // Get the object hit.
            GameObject hit = mouseRayHit.collider.gameObject;

            // Get the object's component.
            Component compObj = hit.GetComponent<Component>();

            // See if the object is a component.
            if (hit.GetComponent<Component>() != null) {
                // Reset the last object if there is one that was being hovered.
                if (this.currentActiveObject != null)
                    compObj.ResetComponentRender();

                // Change the active object.
                this.currentActiveObject = hit;

                // Set that we have something selected.
                this.IsAnObjectSelected = true;

                // Find out how far away from the camera this object was.
                this.selectedDistFromCamera = Vector3.Distance(this.mainSceneCamera.transform.position, hit.transform.position);
                
                // If we hit a component, set its color and that it's being selected.
                compObj.SetSelected(true);

                return true;
            }
        }

        return false;
    }

    private void UnselectObject () {
        if (this.IsAnObjectSelected && this.currentActiveObject != null) {

            // Get the object's component.
            Component compObj = this.currentActiveObject.GetComponent<Component>();

            // Unselect it.
            compObj.SetSelected(false);
            this.currentActiveObject = null;
            this.IsAnObjectSelected = false;
        }
    }

    public void ToggleLoadMenu (bool _toggle) {
        if (_toggle) {
            // Clear all the current options in the scroll menu.
            while (this.loadFileScrollObject.content.transform.childCount > 0) {
                Transform temp = this.loadFileScrollObject.content.transform.GetChild(0);
                temp.SetParent(null);
                GameObject.Destroy(temp.gameObject);
            }

            // Get all the loadable components.
            List<GameObject> loadOptionsAvailable = this.manager.GetLoadableComponentOptions(this.loadFileOptionPrefab);

            // If the list isn't null, we have options to load.
            if (loadOptionsAvailable != null) {
                // Get the scroll content transform.
                Transform loadScrollContent = this.loadFileScrollObject.content.transform;

                // How far down each option should be.
                int curHeight = 5;

                // Add each option to the transform.
                foreach (GameObject obj in loadOptionsAvailable) {
                    RectTransform objTransform = obj.transform as RectTransform;
                    objTransform.SetParent(loadScrollContent);
                    //objTransform.offsetMin = new Vector2(5, -65 - curHeight);
                    //objTransform.offsetMax = new Vector2(-5, -curHeight);
                    objTransform.localScale = new Vector3(1, 1, 1);
                    curHeight += 70;
                }
            }
        }

        this.loadFileMenuParent.SetActive(_toggle);
    }

    public void LoadOptionClicked (LoadOption _optionClicked) {
        this.lastLoadOptionClicked = _optionClicked;

        // Create the new component object.
        GameObject newComp = GameObject.Instantiate(this.baseRenderedComponentPrefab, Vector3.zero, Quaternion.identity);

        // Load in the new component.
        string status = this.manager.LoadSelectedComponent(_optionClicked.GetLoadFilePath(), newComp);

        // If we failed during loading, delete the object.
        if (status == "FAILED") {
            GameObject.Destroy(newComp);
            return;
        }
        // Otherwise, spawn it into the world.
        
        // Clear the last clicked option.
        this.lastLoadOptionClicked = null;

        // Make sure the continue load menu and the load menu is closed.
        this.ToggleLoadMenu(false);
        
        // Set the componentObject's ui to this.
        Component comp = newComp.GetComponent<Component>();
        comp.SetUI(this);

        // Render the attachment points on the component.
        comp.RenderAttachmentPoints(this.baseAttachmentPointPrefab);

        // Add the component to the list.
        this.components.Add(comp);
    }
}

