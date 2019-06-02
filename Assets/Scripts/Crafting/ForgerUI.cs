using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class ForgerUI : MonoBehaviour, IUIManager {

    // Every UI script requires a backend manager.
    public ForgerManager manager;


    // -----/ Internal Mode Information /----- //

    // What mode the forging UI is currently in.
    public ForgerMode currentMode;

    private bool isLoadComponentMenuOpen = false;

    private List<Component> components;

    public GameObject baseRenderedComponentPrefab;

    public GameObject baseAttachmentPointPrefab;

    // Whether the mouse is currently hovering over the design space.
    private bool isCurrentlyHovering;

    private bool isLeftMouseDown;

    private bool isRightMouseDown;


    // -----/ Hovering and Selection Information /----- //

    // The object that is currently being hovered over by the mouse (if any).
    public GameObject currentHoveredObject;

    // Whether there is currently an object selected and being manipulated.
    public bool IsAnObjectSelected;

    // How far away the object was from the camera when it was selected, OR how far to keep the object from the camera.
    public float selectedDistFromCamera;

    public Color hoverColor;

    public Color indicatorHoverColor;

    public Color selectedColor;

    public Color invalidColor;


    // -----/ Snapping information /----- //

    // The point on the selected object that is being attached currently.
    private AttachmentPoint currentSelectedAttachedPoint;

    // The point on the other object that is currently attached.
    private AttachmentPoint currentOtherAttachedPoint;


    // -----/ Camera Control Information /----- //

    public Camera mainSceneCamera;

    public Camera orthoSceneCamera;

    public GameObject cameraRootObject;

    private Vector3 cameraRotation;

    private Vector3 cameraLookPosition;

    // The last mouse position on the screen.
    private Vector3 lastMousePosition;

    // The amount the mouse moved in the last frame while the right mouse was held.
    private Vector3 mouseMoveAmount;

    // The cube in the top right corner to indicate where the camera is looking.
    public GameObject cameraDirectionIndicator;


    // -----/ Load Component UI /----- //

    public GameObject loadComponentFileMenuParent;

    public ScrollRect loadComponentFileScrollObject;

    public GameObject loadComponentFileOptionPrefab;

    private LoadComponentOption lastLoadComponentOptionClicked;


    // -----/ Weapon Loading/Saving Elements /----- //

    public GameObject weaponFileAlreadyExistsParent;

    public Text weaponFileAlreadyExistsText;

    public GameObject loadWeaponFileUnsavedChangesParent;


    // Use this for initialization
    void Start () {
        // Instantiate lists.
        this.components = new List<Component>();

        // Make sure all menus are closed.
        this.OpenCloseLoadComponentMenu(false);

        // Set up camera properties.
        this.cameraLookPosition = new Vector3(0, 0, 0);
        this.cameraRotation = mainSceneCamera.transform.rotation.eulerAngles;
        // Place the camera indicator in the correct location.
        /*
        float frustumHeight = 1.0f * Mathf.Tan(this.mainSceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3 indicatorPos = new Vector3(frustumHeight * this.mainSceneCamera.aspect, frustumHeight, 1f);
        float minScreenDimension = Mathf.Min(indicatorPos.x, indicatorPos.y);
        indicatorPos.x -= minScreenDimension * 0.15f;
        indicatorPos.y -= minScreenDimension * 0.15f;
        this.cameraDirectionIndicator.transform.localPosition = indicatorPos;
        this.cameraDirectionIndicator.transform.localScale = new Vector3(minScreenDimension * 0.075f, minScreenDimension * 0.075f, minScreenDimension * 0.075f);
        */
        this.cameraDirectionIndicator.transform.localPosition = new Vector3(this.orthoSceneCamera.aspect - 0.1f, 0.9f, 3);

        // Set up variables.
        this.lastMousePosition = new Vector3(Mathf.NegativeInfinity, 0, 0);
    }
	
	// Update is called once per frame
	void Update () {
        
        this.HandleMouseHovering(null);

        // If we currently have an object selected and we're manipulating it.
        if (this.manager.IsAComponentSelected) {
            // Check if we're deleting this object first.
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace)) {
                // If the delete or backspace key is pressed, delete the component.
                this.manager.DeleteSelectedComponent();
            }
            else {
                // Otherwise, we'll manipulate the object according to its current position and status.

                // First, check for rotations. If there are any, apply them to the current component.
                this.CheckForRotations();

                // Next move and check for snapping.
                // Get the mouse position in game space.
                Vector2 mousePos = this.mainSceneCamera.ScreenToWorldPoint(Input.mousePosition) + this.mainSceneCamera.ScreenPointToRay(Input.mousePosition).direction * this.manager.distanceToSelectedComponent;

                // Pass that to the manager.
                this.manager.MoveAndSnapComponent(mousePos);
            }
        }
    }

    private void CheckForRotations () {
        if (this.manager.currentSelectedComponent == null)
            return;

        bool shiftApplied = Input.GetKey(KeyCode.LeftShift);
        bool altApplied = Input.GetKey(KeyCode.LeftAlt);

        float rotationAmt = 90f;
        if (shiftApplied)
            rotationAmt = 15f;
        if (altApplied)
            rotationAmt = 5f;

        Vector3 rotation = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.A))
            rotation.z += rotationAmt;
        if (Input.GetKeyDown(KeyCode.D))
            rotation.z -= rotationAmt;
        if (Input.GetKeyDown(KeyCode.W))
            rotation.x += rotationAmt;
        if (Input.GetKeyDown(KeyCode.S))
            rotation.x -= rotationAmt;
        if (Input.GetKeyDown(KeyCode.Q))
            rotation.y += rotationAmt;
        if (Input.GetKeyDown(KeyCode.E))
            rotation.y -= rotationAmt;

        if (rotation != Vector3.zero)
            this.manager.RotateSelectedComponent(rotation);
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
                this.manager.HandleIdleLeftClick(this.mainSceneCamera.ScreenPointToRay(_eventData.position));

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
                if (hit != this.currentHoveredObject) {
                    // Reset the last object if this is a different object.
                    if (this.currentHoveredObject != null) {
                        // Get the component of the last object.
                        Component lastCompObj = this.currentHoveredObject.GetComponent<Component>();
                        // The only option without the component object is the direction indicator.
                        if (lastCompObj == null)
                            this.cameraDirectionIndicator.GetComponent<Renderer>().material.color = Color.white;
                        else {
                            // Otherwise, reset the component's hover status.
                            lastCompObj.SetHovered(false);
                        }
                    }

                    // Change the new hovered.
                    this.currentHoveredObject = hit;

                    // If what we hit was the indicator, give it a different color.
                    if (this.currentHoveredObject == this.cameraDirectionIndicator)
                        this.currentHoveredObject.GetComponent<Renderer>().material.color = this.indicatorHoverColor;
                    else {
                        // If we hit a component, set its color and that it's being hovered.
                        compObj.SetHovered(true);
                    }
                }
            }
        }
        else {
            // If we're no longer hovering over anything, reset the last hovered object if it exists and it's not selected.
            if (this.currentHoveredObject != null) {
                if (this.currentHoveredObject == this.cameraDirectionIndicator)
                    // If it's the camera indicator, just change the color back.
                    this.currentHoveredObject.GetComponent<Renderer>().material.color = Color.white;
                else {
                    // If it's a component, set it to not hovered and reset the color.
                    Component compObj = this.currentHoveredObject.GetComponent<Component>();

                    // The only case this would be null is if the object is currently being deleted.
                    if (compObj != null) {
                        compObj.SetHovered(false);
                    }

                    // Reset the hovered object field.
                    this.currentHoveredObject = null;
                }
            }
        }
    }

    private void HandleMouseDragging (PointerEventData _eventData) {
        if (this.lastMousePosition.x != Mathf.NegativeInfinity) {
            this.mouseMoveAmount = Input.mousePosition - this.lastMousePosition;
        }
        else {
            this.mouseMoveAmount = Vector3.zero;
        }

        this.lastMousePosition = Input.mousePosition;

        if (this.isRightMouseDown) {
            // Get the movement and rotation amounts.
            Vector3 movePos = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            Vector3 rot = new Vector3(this.mouseMoveAmount.y / 20f, this.mouseMoveAmount.x / 10f, 0);

            // Add the rotation to the current camera rotation.
            this.cameraRotation += rot;

            // Clamp the vertical rotation.
            this.cameraRotation.x = Mathf.Clamp(this.cameraRotation.x, -60, 60);

            // Fix the angle.
            this.cameraRotation = this.CorrectEulerAngle(this.cameraRotation);

            // Apply the rotation to the camera's root object.
            this.cameraRootObject.transform.rotation = Quaternion.Euler(this.cameraRotation);

            // Translate the camera.
            // NOTES: Currently disabled until there's more references for users to understand.
            //this.mainSceneCamera.transform.Translate(movePos);

            // Rotate the indicator to continue to be "level".
            this.cameraDirectionIndicator.transform.rotation = Quaternion.identity;
        }
        

        // Reset the mouse movement amount.
        this.mouseMoveAmount = Vector3.zero;
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
        this.HandleMouseDragging(_eventData);
    }

    public void OnMouseDrag (PointerEventData _eventData) {
        this.HandleMouseDragging(_eventData);
    }

    public void OnEndDrag (PointerEventData _eventData) {
        this.HandleMouseDragging(_eventData);

        // Reset the last mouse position so we know when we start dragging again.
        this.lastMousePosition.x = Mathf.NegativeInfinity;
    }

    #endregion

    private void OpenCloseLoadComponentMenu (bool _openState) {
        // Get the transform for the loadComponentMenu to open/close it.
        RectTransform loadComponentMenu = this.loadComponentFileMenuParent.transform as RectTransform;

        if (_openState) {
            // If we're opening, pop the load menu out.
            loadComponentMenu.offsetMin = new Vector2(0, 0);
            loadComponentMenu.offsetMax = new Vector2(200, 0);

            // Clear all the current options in the scroll menu.
            while (this.loadComponentFileScrollObject.content.transform.childCount > 0) {
                Transform temp = this.loadComponentFileScrollObject.content.transform.GetChild(0);
                temp.SetParent(null);
                GameObject.Destroy(temp.gameObject);
            }

            // Get all the loadable components.
            List<GameObject> loadOptionsAvailable = this.manager.GetLoadableComponentOptions(this.loadComponentFileOptionPrefab);

            // If the list isn't null, we have options to load.
            if (loadOptionsAvailable != null) {
                // Get the scroll content transform.
                Transform loadScrollContent = this.loadComponentFileScrollObject.content.transform;

                // How far down each option should be.
                int curHeight = 2;

                // Add each option to the transform.
                foreach (GameObject obj in loadOptionsAvailable) {
                    RectTransform objTransform = obj.transform as RectTransform;
                    objTransform.SetParent(loadScrollContent);
                    objTransform.offsetMin = new Vector2(2, -102 - curHeight);
                    objTransform.offsetMax = new Vector2(-2, -curHeight);
                    objTransform.localScale = new Vector3(1, 1, 1);
                    curHeight += 104;
                }
            }
        }
        else {
            // If we're closing, hide the load menu.
            loadComponentMenu.offsetMin = new Vector2(-195, 0);
            loadComponentMenu.offsetMax = new Vector2(5, 0);
        }
    }

    public void LoadComponentOptionClicked (LoadComponentOption _optionClicked) {
        this.lastLoadComponentOptionClicked = _optionClicked;

        // Create the new component object.
        GameObject loadedComponentObject = null;

        // Load in the new component.
        string status = this.manager.LoadSelectedComponent(_optionClicked.GetLoadFilePath(), this.baseRenderedComponentPrefab, out loadedComponentObject, true);

        // If we failed during loading, delete the object.
        if (status == "FAILED") {
            Destroy(loadedComponentObject);
            return;
        }
        // Otherwise, spawn it into the world.
        
        // Clear the last clicked option.
        this.lastLoadComponentOptionClicked = null;

        // Make sure the continue load menu and the load menu is closed.
        this.OpenCloseLoadComponentMenu(false);
        this.isLoadComponentMenuOpen = false;
        
        // Set the componentObject's ui to this.
        Component loadedComponent = loadedComponentObject.GetComponent<Component>();
        loadedComponent.SetUI(this);

        // Render the attachment points on the component.
        loadedComponent.RenderAttachmentPoints(this.baseAttachmentPointPrefab);

        // Add the component to the list.
        this.components.Add(loadedComponent);
    }

    public void ToggleLoadComponentMenu () {
        this.isLoadComponentMenuOpen = !this.isLoadComponentMenuOpen;
        this.OpenCloseLoadComponentMenu(this.isLoadComponentMenuOpen);
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

