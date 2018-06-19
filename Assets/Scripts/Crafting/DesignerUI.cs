using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class DesignerUI : MonoBehaviour, IUIManager {

    // -----/ Major Scene Objects /-----

    // The DesignerManager in the scene.
    public DesignerManager manager;

    // The 2D camera that drives the designing scene.
    public Camera designerCamera;

    // The 3D camera that drives the component viewer.
    public Camera viewCamera;


    // -----/ Internal Mode Information /-----

    // Which mode the designer is currently in. (IDLE, ADD, MOVE, REMOVE).
    private DesignMode currentMode;

    private EdgeType currentEdgeType;

    // How many actions are currently being handled. Things like starting a new line or drawing the control
    // points for curved lines are each an action that can be cancelled and reverted.
    private int currentActionsBeingHandled;

    // Whether the mouse is currently hovering over the design space.
    private bool isCurrentlyHovering;

    // Whether the mouse is currently pressed down.
    private bool isLeftMouseDown;


    // -----/ Right Click Menu Information /----

    // The parent for the right click menu.
    public GameObject rightClickMenuHandle;

    // Whether the right click menu is currently open or not.
    private bool isRightClickMenuOpen;


    // -----/ UI Display Text/Buttons /-----

    // The text that indicates what the grid size is currently at.
    public Text gridSizeText;

    // The text that tells the player what mode they're currently in.
    public Text designModeText;

    public GameObject designUIParent;

    public GameObject viewUIParent;

    public InputField componentNameField;


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
        // Initialize the manager.
        this.ToggleExpand(false);

        // Make sure all menus are closed.
        this.CloseRightClickMenu();
        this.ToggleOverwriteMenu(false);
        this.ToggleLoadMenu(false);
        this.ToggleContinueLoadMenu(false);

        // Set the current held actions at 0.
        this.currentActionsBeingHandled = 0;

        // Initialize the grid.
        this.gridSizeText.text = this.manager.GetGridStep().ToString();
        this.gridSizeText.transform.parent.GetComponent<Image>().color = Color.grey;

        // Set the standard mode into idle.
        this.currentMode = DesignMode.IDLE;
        this.currentEdgeType = EdgeType.FLAT;
        this.designModeText.text = "Mode: " + this.currentMode;

        this.isCurrentlyHovering = false;
    }
	
	// Update is called once per frame
	void Update () {
        this.CheckIfRightClickMenuShouldClose();

        if (this.isCurrentlyHovering) {
            this.HandleMouseHovering(null);
        }
	}

    /// <summary>
    /// A function that handles what happens when the left mouse button is CLICKED on the design space.
    /// </summary>
    public void HandleLeftClick (PointerEventData _eventData) {
        Vector2 worldPos = this.designerCamera.ScreenToWorldPoint(_eventData.position);

        switch (this.currentMode) {
            case DesignMode.ADD:
                this.currentActionsBeingHandled += this.manager.AddClicked(worldPos, this.currentEdgeType);
                break;
            case DesignMode.MOVE:

                break;
            case DesignMode.REMOVE:
                this.manager.RemoveClicked(worldPos);
                break;
            case DesignMode.EDGEMODE:
                this.manager.EdgeModeClicked(worldPos, this.currentEdgeType);
                break;
            case DesignMode.IDLE:
            default:

                break;
        }
    }
    
    /// <summary>
    /// A function that handles what happens when the right mouse button is CLICKED on the design space.
    /// </summary>
    public void HandleRightClick (PointerEventData _eventData) {

        // If the player is currently doing something, we should cancel it.
        if (this.currentActionsBeingHandled > 0) {

            switch (this.currentMode) {
                case DesignMode.ADD:
                    if (this.manager.AddRightClicked())
                        this.currentActionsBeingHandled--;
                    break;
                case DesignMode.MOVE:

                    break;
                case DesignMode.REMOVE:

                    break;
                case DesignMode.EDGEMODE:

                    break;
                case DesignMode.IDLE:
                default:

                    break;
            }

            return;
        }
        else if (this.currentMode == DesignMode.IDLE) {
            // See if the menu is open. If it is, close it. If it's not, open it.
            if (this.isRightClickMenuOpen) {
                this.isRightClickMenuOpen = false;
                this.rightClickMenuHandle.SetActive(false);
            }
            else {
                this.isRightClickMenuOpen = true;
                this.rightClickMenuHandle.SetActive(true);
                // Set the menu to the position of the mouse currently.
                this.rightClickMenuHandle.transform.position = Input.mousePosition;
            }
        }
        else {
            this.currentMode = DesignMode.IDLE;
            this.designModeText.text = "Mode: " + this.currentMode;
        }
    }

    public void OnPointerDown (PointerEventData _eventData) {
        throw new NotImplementedException();
    }

    public void OnPointerUp (PointerEventData _eventData) {
        throw new NotImplementedException();
    }

    public void EnableHovering (bool _enabled) {
        this.isCurrentlyHovering = _enabled;
    }

    public void HandleMouseHovering (PointerEventData _eventData) {
        Vector2 worldPos;
        if (_eventData != null)
            worldPos = this.designerCamera.ScreenToWorldPoint(_eventData.position);
        else
            worldPos = this.designerCamera.ScreenToWorldPoint(Input.mousePosition);


        switch (this.currentMode) {
            case DesignMode.ADD:
                this.manager.AddHovering(worldPos);
                break;
            case DesignMode.MOVE:
                this.manager.MoveHovering(worldPos);
                break;
            case DesignMode.REMOVE:
                this.manager.RemoveHovering(worldPos);
                break;
            case DesignMode.EDGEMODE:

                break;
            case DesignMode.IDLE:
            default:

                break;
        }
    }
    
    public void OnMouseDrag (PointerEventData _eventData) {
        // Make sure the mouse is over the design space currently.
        if (_eventData.pointerEnter.name == "DesignArea") {
            // Make sure we're in the move mode AND the left mouse is what's being pressed.
            if (this.currentMode == DesignMode.MOVE && _eventData.button == PointerEventData.InputButton.Left)
                this.manager.MoveDraggedVertex(this.designerCamera.ScreenToWorldPoint(_eventData.position));
        }
    }

    public void OnBeginDrag (PointerEventData _eventData) {
        // Make sure the mouse is over the design space currently.
        if (_eventData.pointerEnter.name == "DesignArea") {
            // Make sure we're in the move mode AND the left mouse is what's being pressed.
            if (this.currentMode == DesignMode.MOVE && _eventData.button == PointerEventData.InputButton.Left)
                this.manager.StartDragging(this.designerCamera.ScreenToWorldPoint(_eventData.position));
        }
    }

    public void OnEndDrag (PointerEventData _eventData) {
        // Make sure the mouse is over the design space currently.
        if (_eventData.pointerEnter.name == "DesignArea") {
            // Make sure we're in the move mode AND the left mouse is what's being pressed.
            if (this.currentMode == DesignMode.MOVE && _eventData.button == PointerEventData.InputButton.Left)
                this.manager.EndDragging(this.designerCamera.ScreenToWorldPoint(_eventData.position));
        }
    }

    void CheckIfRightClickMenuShouldClose () {
        if (this.isRightClickMenuOpen) {
            // See if the moue is too far away.
            float dist = Vector2.Distance(this.rightClickMenuHandle.transform.position, Input.mousePosition);

            if (dist > 200f) {
                this.CloseRightClickMenu();
            }
        }
    }

    public void CloseRightClickMenu () {
        this.isRightClickMenuOpen = false;
        this.rightClickMenuHandle.SetActive(false);

        this.rightClickMenuHandle.transform.position = new Vector2(-1000, 0);
    }

    public void ToggleGrid () {
        if (this.manager.ToggleGrid())
            this.gridSizeText.transform.parent.GetComponent<Image>().color = new Color(0.5f, 1.0f, 0.5f);
        else
            this.gridSizeText.transform.parent.GetComponent<Image>().color = Color.grey;
    }

    public void ChangeGridSize (bool _increase) {
        this.gridSizeText.text = this.manager.SetGridSize(_increase).ToString();
    }

    public void ChangeDesignMode (int _mode) {
        // If the mode passed in is the same as what we already have, we should go into idle and cancel any actions.
        if (_mode == (int) this.currentMode) {
            this.currentMode = DesignMode.IDLE;
            this.designModeText.text = "Mode: " + this.currentMode;
            this.currentActionsBeingHandled = 0;
        }
        else {
            this.currentMode = (DesignMode) _mode;
            this.designModeText.text = "Mode: " + this.currentMode;
        }

        if (this.isRightClickMenuOpen)
            this.CloseRightClickMenu();
    }

    public void ToggleExpand (bool _toggle) {
        if (this.manager.ToggleExpansion(_toggle)) {
            this.designerCamera.gameObject.SetActive(false);
            this.designUIParent.SetActive(false);

            this.viewCamera.gameObject.SetActive(true);
            this.viewUIParent.SetActive(true);
        }
        else {
            this.designerCamera.gameObject.SetActive(true);
            this.designUIParent.SetActive(true);

            this.viewCamera.gameObject.SetActive(false);
            this.viewUIParent.SetActive(false);
        }
    }

    public void ChangeEdgeMode (int _type) {
        if (this.currentMode != DesignMode.ADD) {
            this.ChangeDesignMode(4);
        }

        this.currentEdgeType = (EdgeType) _type;
    }



    // -----/ Save/Load Methods /----- //

    public void SaveClicked (bool _overwrite) {
        string name = this.componentNameField.text;

        // Find out what sort of status came back.
        string status = this.manager.SaveCurrentComponent(name, _overwrite);

        if (status == "SAVED") {
            // Make sure to close the overwrite window if it needs to be closed.
            if (this.fileAlreadyExistsParent.activeSelf)
                this.ToggleOverwriteMenu(false);
        }
        else if (status == "ALREADYEXISTS") {
            this.ToggleOverwriteMenu(true);
        }
        else {

        }
    }

    public void ToggleOverwriteMenu (bool _toggle) {
        if (_toggle) {
            this.fileAlreadyExistsText.text = this.componentNameField.text;
        }

        this.fileAlreadyExistsParent.SetActive(_toggle);
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

        string status = this.manager.LoadSelectedOption(_optionClicked.GetLoadFilePath(), false);

        if (status == "UNSAVEDCHANGES") {
            this.ToggleContinueLoadMenu(true);
        }
        else if (status == "LOADED") {
            // Clear the last clicked option.
            this.lastLoadOptionClicked = null;

            // Make sure the continue load menu and the load menu is closed.
            this.ToggleContinueLoadMenu(false);
            this.ToggleLoadMenu(false);
        }
    }

    public void ContinueLoadClicked (bool _continue) {
        // If we want to continue with loading, load the option without saving.
        if (_continue) {
            // Try to load the last option without saving.
            string status = this.manager.LoadSelectedOption(this.lastLoadOptionClicked.GetLoadFilePath(), true);

            if (status == "UNSAVEDCHANGES") {

            }
            else if (status == "LOADED") {
                // Clear the last clicked option.
                this.lastLoadOptionClicked = null;

                // Make sure the continue load menu and the load menu is closed.
                this.ToggleContinueLoadMenu(false);
                this.ToggleLoadMenu(false);
            }
        }
        else {
            // Clear the last clicked option.
            this.lastLoadOptionClicked = null;

            // If we don't want to continue, close the menus so the player can save.
            this.ToggleContinueLoadMenu(false);
            this.ToggleLoadMenu(false);
        }
    }

    
    public void ToggleContinueLoadMenu (bool _toggle) {
        this.loadFileUnsavedChangesParent.SetActive(_toggle);
    }
}
