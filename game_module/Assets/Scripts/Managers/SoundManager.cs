using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SoundManager
{
    public AudioSource[] audioSources = new AudioSource[(int)Define.SoundType.MaxCount];
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
            audioSources[i] = go.GetOrAddComponent<AudioSource>();
        }

        audioSources[(int)Define.SoundType.BGM].loop = true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    AudioClip GetorAddAuidioClip(string path, Define.SoundType type = Define.SoundType.Effect)
    {
        if (path.Contains("Sounds") == false)
        {
            return null;
        }

        AudioClip audioClip = null;
        
        return audioClip;
    }
}