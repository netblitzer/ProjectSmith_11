using System;
using System.Collections.Generic;
using UnityEngine;

public class ComponentObject : MonoBehaviour {
    public GameObject componentObj;
    private Color originalColor;
    private ForgerUI forgeUI;

    public bool IsObjectLocked;
    public bool IsHovered;
    public bool IsSelected;

    public void SetComponent (ForgerUI _ui, GameObject _obj) {
        this.SetComponent(_ui, _obj, false);
    }

    public void SetComponent (ForgerUI _ui, GameObject _obj, bool _locked) {
        this.forgeUI = _ui;

        this.componentObj = _obj;
        this.originalColor = _obj.GetComponent<Renderer>().material.color;
        this.IsObjectLocked = _locked;
    }

    public void SetHovered (bool _hovered) {
        this.IsHovered = _hovered;
        // If we're hovered but not selected, change the color to the hover color.
        if (this.IsHovered && !this.IsSelected)
            this.componentObj.GetComponent<Renderer>().material.color = this.forgeUI.hoverColor;

        // If we're not hovered or selected, reset the color.
        if (!this.IsHovered && !this.IsSelected)
            this.componentObj.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void SetSelected (bool _selected) {
        this.IsSelected = _selected;

        // If we're selected, change the color to the selected color.
        if (this.IsSelected)
            this.componentObj.GetComponent<Renderer>().material.color = this.forgeUI.selectedColor;

        // If we're not hovered or selected, reset the color.
        if (!this.IsHovered && !this.IsSelected)
            this.componentObj.GetComponent<Renderer>().material.color = this.originalColor;
    }

    public void ResetComponent () {
        this.IsSelected = false;
        this.IsHovered = false;

        this.componentObj.GetComponent<Renderer>().material.color = this.originalColor;
    }
}
