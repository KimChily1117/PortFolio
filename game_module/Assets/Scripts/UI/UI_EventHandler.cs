using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_EventHandler : MonoBehaviour, IPointerClickHandler ,IBeginDragHandler , IEndDragHandler 
{
    public Action<PointerEventData> ClickEventAction = null;
    public Action<PointerEventData> DragEventAction = null;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (DragEventAction != null)
        {
            DragEventAction.Invoke(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        throw new System.NotImplementedException();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ClickEventAction.Invoke(eventData);
    }
}
