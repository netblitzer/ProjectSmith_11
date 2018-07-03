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

    private List<ComponentObject> components;

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
        this.components = new List<ComponentObject>();

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
                ComponentObject comp = this.currentActiveObject.GetComponent<ComponentObject>();
                this.components.Remove(comp);
                Destroy(this.currentActiveObject);
                this.currentActiveObject = null;
                this.IsAnObjectSelected = false;
            }
            else {
                this.currentActiveObject.transform.position = this.mainSceneCamera.transform.position + this.mainSceneCamera.ScreenPointToRay(Input.mousePosition).direction * this.selectedDistFromCamera;
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
            if (hit.GetComponent<ComponentObject>() != null || hit == this.cameraDirectionIndicator) {
                // Get the object ComponentObject (if any).
                ComponentObject compObj = hit.GetComponent<ComponentObject>();
                // Make sure we're not hovering something that is already active.
                if (hit != this.currentActiveObject) {
                    // Reset the last object if this is a different object.
                    if (this.currentActiveObject != null) {
                        // Get the componentObject of the last object.
                        ComponentObject lastCompObj = this.currentActiveObject.GetComponent<ComponentObject>();
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
                    ComponentObject compObj = this.currentActiveObject.GetComponent<ComponentObject>();
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

            // Get the object's ComponentObject.
            ComponentObject compObj = hit.GetComponent<ComponentObject>();

            // See if the object is a component.
            if (hit.GetComponent<ComponentObject>() != null) {
                // Reset the last object if there is one that was being hovered.
                if (this.currentActiveObject != null)
                    compObj.ResetComponent();

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

            // Get the object's ComponentObject.
            ComponentObject compObj = this.currentActiveObject.GetComponent<ComponentObject>();

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

        // Load in the new component.
        GameObject newComp;
        string status = this.manager.LoadSelectedComponent(_optionClicked.GetLoadFilePath(), out newComp);

        // Clear the last clicked option.
        this.lastLoadOptionClicked = null;

        // Make sure the continue load menu and the load menu is closed.
        this.ToggleLoadMenu(false);
        
        // Set the componentObject's ui to this.
        ComponentObject comp = newComp.GetComponent<ComponentObject>();
        comp.SetUI(this);

        // Add the component to the list.
        this.components.Add(comp);
    }
}

