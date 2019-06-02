using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.EventSystems;

public interface IUIManager {

    void HandleLeftClick (PointerEventData _eventData);

    void HandleRightClick (PointerEventData _eventData);

    void OnPointerDown (PointerEventData _eventData);

    void OnPointerUp (PointerEventData _eventData);

    void HandleMouseHovering (PointerEventData _eventData);

    void EnableHovering (bool _enabled);

    void OnBeginDrag (PointerEventData _eventData);

    void OnMouseDrag (PointerEventData _eventData);

    void OnEndDrag (PointerEventData _eventData);
}
