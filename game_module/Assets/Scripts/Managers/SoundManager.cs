using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SoundManager
{
    private Dictionary<string, AudioClip> _audioClips = new Dictionary<string, AudioClip>();
    public void Init()
    {
        Debug.Log(($"SoundManager ] Initialized"));
        
        GameObject obj = GameObject.Find("@Sound");
        if (obj == null)
        {
            obj = new GameObject { name = $"@Sound" };
        }
        UnityEngine.Object.DontDestroyOnLoad(obj);

        string[] SoundNames = Enum.GetNames(typeof(Define.SoundType));


        for (int i = 0; i < SoundNames.Length-1; i++)
        {
            GameObject go = new GameObject { name = SoundNames[i] };
            go.transform.SetParent(obj.transform);
            SetAudioComponent(go);
        }
    }




    void SetAudioComponent(GameObject targetObj)
    {
        targetObj.AddComponent<AudioSource>();
    }
}