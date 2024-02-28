using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    Vector3 cameraPosition = new Vector3(0, 0, -1);
    public float _cameraSpeed = 2f;

    public float _height;
    public float _width;



    private void Start()
    {
        _height = Camera.main.orthographicSize;
        _width = _height * Screen.width/Screen.height;
    }


    private void FixedUpdate()
    {
        if (GameManager.ObjectManager.MyPlayer)
        {
            transform.position = Vector3.Lerp(transform.position,
                GameManager.ObjectManager.MyPlayer._Sprite.transform.position + cameraPosition,
                Time.deltaTime * _cameraSpeed);
            //transform.position = GameManager.ObjectManager.MyPlayer._Sprite.transform.position + cameraPosition;
        }
    }
}
