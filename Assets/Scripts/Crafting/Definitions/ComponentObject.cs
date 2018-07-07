using System;
using System.Collections.Generic;
using UnityEngine;

public class ComponentObject : MonoBehaviour {
    // The component that this object originates from.
    private Component mainComponent;

    // The original color that this object was when it was instantiated.
    private Color originalColor;

    // The ForgerUI in the scene to get the colors from.
    private ForgerUI forgeUI;

    // Whether this object is currently locked (can't be moved, manipulated, deleted).
    public bool IsObjectLocked;

    // Whether this object is being hovered over by the mouse.
    public bool IsHovered;

    // Whether this object is currently selected by the mouse.
    public bool IsSelected;

    private void Start () {
        this.originalColor = this.gameObject.GetComponent<Renderer>().material.color;
    }

    public void SetUI (ForgerUI _ui) {
        this.forgeUI = _ui;
    }

    public void SetComponent (Component _comp) {
        this.mainComponent = _comp;

        // Create a new indicator for each attachment point.
    }

    public void LockComponentObject (bool _locked) {
        this.IsObjectLocked = _locked;
    }

    public void SetHovered (bool _hovered) {
        this.IsHovered = _hovered;
        // If we're hovered but not selected, change the color to the hover color.
        if (this.IsHovered && !this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.forgeUI.hoverColor;

        // If we're not hovered or selected, reset the color.
        if (!this.IsHovered && !this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void SetSelected (bool _selected) {
        this.IsSelected = _selected;

        // If we're selected, change the color to the selected color.
        if (this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.forgeUI.selectedColor;

        // If we're not hovered or selected, reset the color.
        if (!this.IsHovered && !this.IsSelected)
            this.gameObject.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void ResetComponent () {
        this.IsSelected = false;
        this.IsHovered = false;

        this.gameObject.GetComponent<Renderer>().material.color = this.originalColor;
    }
}
