using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{

    Vector3 cameraPosition = new Vector3(0, 0, -1);
    public float _cameraSpeed = 3.5f;

    public float _height;
    public float _width;

    public Vector2 mapSize;
    public Vector2 center;


    private void Start()
    {
        _height = Camera.main.orthographicSize;
        _width = _height * Screen.width/Screen.height;
    }


    private void FixedUpdate()
    {
        if (GameManager.ObjectManager.MyPlayer)
        {
            LimitCameraArea();
            //    transform.position = Vector3.Lerp(transform.position,
            //        GameManager.ObjectManager.MyPlayer._Sprite.transform.position + cameraPosition,
            //        Time.deltaTime * _cameraSpeed);
            //    //transform.position = GameManager.ObjectManager.MyPlayer._Sprite.transform.position + cameraPosition;
            //
        }
    }

    void LimitCameraArea()
    {
        transform.position = Vector3.Lerp(transform.position,
                                          GameManager.ObjectManager.MyPlayer._Sprite.transform.position + cameraPosition,
                                          Time.deltaTime * _cameraSpeed);
        float lx = mapSize.x - _width;
        float clampX = Mathf.Clamp(transform.position.x, -lx + center.x , lx + center.x);

        float ly = mapSize.y - _height;
        float clampY = Mathf.Clamp(transform.position.y, -ly + center.y, ly + center.y);

        transform.position = new Vector3(clampX, clampY, -10f);
    }

    public void SetCameraLimit(TownMapState mapState)
    {
        switch (mapState) 
        {
            case TownMapState.SERIAROOM:
                mapSize.Set(4.98f, 3.0f);
                center.Set(0f, 0f);
                break;

            case TownMapState.DUNGEONENTRANCE:
                mapSize.Set(8.1f, 3f);
                center.Set(25.79f, 0f);
                break;        
        }
    }
}
