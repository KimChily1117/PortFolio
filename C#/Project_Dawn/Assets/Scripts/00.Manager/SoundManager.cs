using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static Define;


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

        for (int i = 0; i < SoundNames.Length - 1; i++)
        {
            GameObject go = new GameObject { name = SoundNames[i] };
            go.transform.SetParent(obj.transform);
            audioSources[i] = go.GetOrAddComponent<AudioSource>();
        }
        audioSources[(int)Define.SoundType.BGM].loop = true;
    }

    public void Play(string path, Define.SoundType soundType = Define.SoundType.Effect, float pitch = 1.0f)
    {
        AudioClip audioClip = GetorAddAuidioClip(path, soundType);

        switch (soundType)
        {
            case Define.SoundType.BGM:
                {
                    if (!audioClip)
                    {
                        Debug.Log(($"Bgm File is null!!"));
                    }
                    audioSources[(int)Define.SoundType.BGM].clip = audioClip;


                    audioSources[(int)Define.SoundType.BGM].loop = true;
                    audioSources[(int)Define.SoundType.BGM].pitch = pitch;
                    audioSources[(int)Define.SoundType.BGM].Play();

                }
                break;
            case Define.SoundType.Effect:
                {
                    if (!audioClip)
                    {
                        Debug.Log($"Effect Sound File is null!");
                    }

                    audioSources[(int)Define.SoundType.Effect].pitch = pitch;
                    audioSources[(int)Define.SoundType.Effect].PlayOneShot((audioClip));
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(soundType), soundType, null);
        }
    }


    public void PlayEffectOneShot(string path)
    {
        AudioClip audioClip = GetorAddAuidioClip(path, Define.SoundType.Effect);


        if (!audioClip)
        {
            Debug.Log($"Effect Sound File is null!");
        }
        if (audioSources[(int)Define.SoundType.Effect].isPlaying == false)
        {
            audioSources[(int)Define.SoundType.Effect].pitch = 1f;
            audioSources[(int)Define.SoundType.Effect].PlayOneShot((audioClip));
        }
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
            path = $"Sounds/{path}";
        }
        AudioClip audioClip = null;

        switch (type)
        {
            case Define.SoundType.BGM:
                audioClip = GameManager.Resources.Load<AudioClip>(path);
                break;
            case Define.SoundType.Effect:
                if (_audioClips.TryGetValue(path, out audioClip) == false)
                {
                    audioClip = GameManager.Resources.Load<AudioClip>(path);
                    _audioClips.Add(path, audioClip);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
        return audioClip;
    }

    public void BGMStop()
    {
        audioSources[(int)Define.SoundType.BGM].Stop();
    }



    public void SetAudioVolume(float volume , Define.SoundType soundType = SoundType.BGM)
    {
        switch (soundType)
        {
            case SoundType.BGM:
                audioSources[(int)Define.SoundType.BGM].volume = volume;

                break;
            case SoundType.Effect:
                audioSources[(int)Define.SoundType.BGM].volume = volume;

                break;
            case SoundType.MaxCount:
                break;
        }


    }


}