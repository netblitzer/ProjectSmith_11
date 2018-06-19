using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class ForgerUI : MonoBehaviour, IUIManager {

    public ForgerManager manager;

    public Camera mainSceneCamera;


    // -----/ Internal Mode Information /-----

    // What mode the forging UI is currently in.
    public ForgerMode currentMode;

    // Whether the mouse is currently hovering over the design space.
    private bool isCurrentlyHovering;

    private bool isLeftMouseDown;

    private bool isRightMouseDown;

    private Vector3 cameraRotation;

    private Vector3 cameraLookPosition;

    private Vector3 lastMousePosition;

    private Vector3 mouseMoveAmount;


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
        // Make sure all menus are closed.
        this.ToggleLoadMenu(false);

        this.cameraLookPosition = new Vector3(0, 0, 0);
        this.cameraRotation = mainSceneCamera.transform.rotation.eulerAngles;
    }
	
	// Update is called once per frame
	void Update () {

        if (this.isCurrentlyHovering) {
            this.HandleMouseHovering(null);
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

        string status = this.manager.LoadSelectedComponent(_optionClicked.GetLoadFilePath());

        // Clear the last clicked option.
        this.lastLoadOptionClicked = null;

        // Make sure the continue load menu and the load menu is closed.
        this.ToggleLoadMenu(false);
    }
}
