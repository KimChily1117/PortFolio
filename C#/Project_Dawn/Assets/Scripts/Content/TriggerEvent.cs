using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriggerEvent : MonoBehaviour
{
    private Action<Collider> _enterAction;
    private Action<Collider> _stayAction;
    private Action<Collider> _exitAction;


    public void AddTriggerEnterEvent(Action<Collider> enterAction) 
    { 
        _enterAction -= enterAction;
        _enterAction += enterAction;
    
    }
    public void AddTriggerStayEvent(Action<Collider> stayAction) 
    { 
        _stayAction -= stayAction;
        _stayAction += stayAction;
    }
    public void AddTriggerExitEvent(Action<Collider> exitAction)
    {
        _exitAction -= exitAction;
        _exitAction += exitAction;
    }
    public void AddTriggerEvent(Action<Collider> enterAction = null, Action<Collider> stayAction = null, Action<Collider> exitAction = null)
    {
        _enterAction = enterAction;
        _stayAction = stayAction;
        _exitAction = exitAction;
    }

    public void RemoveTriggerEnterEvent(Action<Collider> exitAction) 
    { 
        _exitAction -= exitAction;
    }
    public void RemoveTriggerStayEvent(Action<Collider> exitAction) 
    {
        _exitAction -= exitAction; 
    }
    public void RemoveTriggerExitEvent(Action<Collider> exitAction) 
    {
        _exitAction -= exitAction;
    }
    public void ClearTriggerEvent()
    {
        _enterAction = null;
        _stayAction = null;
        _exitAction = null;
    }

    private void OnTriggerEnter(Collider other)
    {
    //    if (IsTriggerNoneAction(other))
    //        return;

        _enterAction?.Invoke(other);
    }
    private void OnTriggerStay(Collider other)
    {
        //if (IsTriggerNoneAction(other))
        //    return;

        _stayAction?.Invoke(other);
    }
    private void OnTriggerExit(Collider other)
    {       
        _exitAction?.Invoke(other);
    }

    //private bool IsTriggerNoneAction(Collider other)
    //{
    //    return other.gameObject != TargetObj()
    //        || (AgoraManager.instance != null && AgoraManager.instance.AgoraState == Com.Pineone.Metaverce.AgoraState.SCREEN_SHARE);

    //}

    /// <summary>
    /// Photon targetObject(isMine)
    /// </summary>
    /// <returns></returns>
    //private GameObject TargetObj()
    //{
        //if (Com.Pineone.Metaverce.MainUI.Instance != null)
        //{
        //    if (Com.Pineone.Metaverce.MainUI.Instance.Target != null)
        //    {
        //        return Com.Pineone.Metaverce.MainUI.Instance.Target.gameObject;
        //    }
        //    else
        //        Debug.Log("TriggerEvent] MainUI Target null");
        //}
        //else
        //    Debug.Log("TriggerEvent] MainUI null");

        //return null;
    //}
}



