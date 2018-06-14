using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DesignSpace : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler {

    public DesignerUI mainUI;

    public void OnPointerClick (PointerEventData _eventData) {
        if (_eventData.button == PointerEventData.InputButton.Left)
            mainUI.HandleLeftClick(_eventData);
        else if (_eventData.button == PointerEventData.InputButton.Right)
            mainUI.HandleRightClick(_eventData);
    }
    
    public void OnPointerUp (PointerEventData _eventData) {
        if (_eventData.button == PointerEventData.InputButton.Left)
            mainUI.EnableLeftMouseDown(false);
    }

    public void OnPointerDown (PointerEventData _eventData) {
        if (_eventData.button == PointerEventData.InputButton.Left)
            mainUI.EnableLeftMouseDown(true);
    }

    public void OnPointerEnter (PointerEventData _eventData) {
        mainUI.EnableHovering(true);
    }

    public void OnPointerExit (PointerEventData _eventData) {
        mainUI.EnableHovering(false);
    }

    public void OnBeginDrag (PointerEventData _eventData) {
        mainUI.OnBeginDrag(_eventData);
    }

    public void OnDrag (PointerEventData _eventData) {
        mainUI.OnMouseDrag(_eventData);
    }

    public void OnEndDrag (PointerEventData _eventData) {
        mainUI.OnEndDrag(_eventData);
    }
}
