using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Define.CameraView _cameraType = Define.CameraView.QuaterView;

    public Vector3 _delta; // ??

    [SerializeField] private GameObject Player;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (_cameraType == Define.CameraView.QuaterView)
        {
            RaycastHit hit;
            if (Physics.Raycast(Player.transform.position, _delta, out hit,
                _delta.magnitude,
                LayerMask.GetMask("Wall")))
            {
                float dist = (hit.point - Player.transform.position).magnitude * 0.8f;
                transform.position = Player.transform.position + (_delta.normalized * dist);
            }

            else
            {

                transform.position = Player.transform.position + _delta;
                transform.LookAt(Player.transform);
            }
        }
    }



    private void InitializedCameraType(Define.CameraView _cameraView)
    {
        switch (_cameraView)
        {
            case Define.CameraView.QuaterView:
                break;
            default:
                break;
        }
    }


}
