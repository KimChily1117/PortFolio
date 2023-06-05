using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using agora_gaming_rtc;
using Photon.Pun;
using Photon.Realtime;
using System;

public class AgoraManager : MonoBehaviourPunCallbacks
{
    public GameObject videoCanvas;

#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
    //Permission List
    private ArrayList permissionList;
#endif
    //Agora] Agora engine
    private IRtcEngine mRtcEngine;
    //Agora] AppId
    private string appId = "5a8c4960745341d7a446691d83e91ee5";

    //Agora] ChannelName
    private string _channelName = "kim";

    //Agora] MyId
    private uint myId;

    public Define.AgoraState AgoraState => _agoraState;
    private Define.AgoraState _agoraState;

    //Agora] Agora CallBack
    private Action onOtherCallback;
    private Action<string, uint, int> onJoinChannelCB;
    private Action<uint, int> onUserJoinCB;
    private Action<uint, USER_OFFLINE_REASON> onUserOfflineCB;
    private Action<RtcStats> onLeaveChannelCB;

    //private Action<LOCAL_AUDIO_STREAM_STATE, LOCAL_AUDIO_STREAM_ERROR> onLocalAudioStateChangedCB;
    private Action<uint, REMOTE_AUDIO_STATE, REMOTE_AUDIO_STATE_REASON, int> onRemoteAudioStateChangedCB;

    private Action<LOCAL_VIDEO_STREAM_STATE, LOCAL_VIDEO_STREAM_ERROR> onLocalVideoStateChangedCB;
    private Action<uint, REMOTE_VIDEO_STATE, REMOTE_VIDEO_STATE_REASON, int> onRemoteVideoStateChangedCB;
    private GameObject _VideoSurface;

    private uint remoteUid;
    private bool isStreaming = true;
    private const float Offset = 100;
    private bool isRemoteUser = false;

    private void RemoveCallback()
    {
        onJoinChannelCB = null;
        onUserJoinCB = null;
        onUserOfflineCB = null;
        onLeaveChannelCB = null;

        onOtherCallback = null;
        //onLocalAudioStateChangedCB = null;
        onRemoteAudioStateChangedCB = null;

        onLocalVideoStateChangedCB = null;
        onRemoteVideoStateChangedCB = null;
    }




    /// <summary>
    /// 호스트 콜백 등록 및 비디오 캔버스 엑티브
    /// </summary>
    public void StartHostStreaming()
    {

        Debug.Log($"Host");

        if (videoCanvas.activeSelf == false)
        {
            videoCanvas.SetActive(true);
            RemoveCallback();
            SetOnOtherCallback(() => SetOtherCallbackForHost());
            SetOnJoinChannelSuccessCallback(OnJoinChannelSusccess);
            EnterAgoraChannel(Define.AgoraState.LIVESTREAMING_HOST);
            //AgoraManager.instance.SetOnUserOfflineCallback(SetLeaveOtherUser);
            SetOnLeaveChannelCallback(UnloadEngine);
            //SetLeaveButtonCallBack(LeaveStreamingChannel, true);
            // StreamingObjectManager.instance.SetClientObjectActive(true);
        }
    }

    private void UnloadEngine(RtcStats rtcStats)
    {
        if (mRtcEngine == null)
        {
            //LogPrint("UnLoadEngine] mRtcEngine Null");
            return;
        }

        //LogPrint("UnLoadEngine] calling unloadEngine mRtcEngine");
        IRtcEngine.Destroy();
        mRtcEngine = null;
    }

    private void EnterAgoraChannel(Define.AgoraState type)
    {       
       _agoraState = type;
       CreateRtcEngine();            
    }
    #region Agora Callback
    /// <summary>
    /// Agora] Set Other callback For Streaming 
    /// </summary>
    /// <param name="action"></param>
    public void SetOnOtherCallback(Action action) { onOtherCallback = action; }

    /// <summary>
    /// Agora] Set channel join callback
    /// </summary>
    /// <param name="action"></param>
    public void SetOnJoinChannelSuccessCallback(Action<string, uint, int> action) { onJoinChannelCB = action; }

    /// <summary>
    /// Agora] Set other users access the channel callback
    /// </summary>
    /// <param name="action"></param>
    public void SetOnUserJoinedCallback(Action<uint, int> action) { onUserJoinCB = action; }

    /// <summary>
    /// Agora] Set Another user disconnects from the channel callback
    /// </summary>
    /// <param name="action"></param>
    public void SetOnUserOfflineCallback(Action<uint, USER_OFFLINE_REASON> action) { onUserOfflineCB = action; }

    /// <summary>
    /// Agora] Set channel disconnection callback
    /// </summary>
    /// <param name="action"></param>
    public void SetOnLeaveChannelCallback(Action<RtcStats> action) { onLeaveChannelCB = action; }

    ///// <summary>
    ///// Agora] Set LocalAudioStateChanged callback
    ///// </summary>
    ///// <param name="action"></param>
    //public void SetOnLocalAudioStateChangedCallback(Action<LOCAL_AUDIO_STREAM_STATE, LOCAL_AUDIO_STREAM_ERROR> action) { onLocalAudioStateChangedCB = action; }


    /// <summary>
    /// Agora] Set RemoteAudioStateChanged callback
    /// </summary>
    /// <param name="action"></param>
    public void SetOnRemoteAudioStateChangedCallback(Action<uint, REMOTE_AUDIO_STATE, REMOTE_AUDIO_STATE_REASON, int> action) { onRemoteAudioStateChangedCB = action; }

    /// Agora] Set RemoteVideoStateChanged callback
    /// </summary>
    /// <param name="action"></param>    /// <summary>
    public void SetOnLocalVideoStateChangedCBCallback(Action<LOCAL_VIDEO_STREAM_STATE, LOCAL_VIDEO_STREAM_ERROR> action) { onLocalVideoStateChangedCB = action; }

    /// Agora] Set RemoteVideoStateChanged callback
    /// </summary>
    /// <param name="action"></param>    /// <summary>
    public void SetOnRemoteVideoStateChangedCallback(Action<uint, REMOTE_VIDEO_STATE, REMOTE_VIDEO_STATE_REASON, int> action) { onRemoteVideoStateChangedCB = action; }


    /// <summary>
    /// Agora] Remove channel join callback
    /// </summary>
    public void RemoveOnJoinChannelSuccessCallback() { onJoinChannelCB = null; }

    /// <summary>
    /// Agora] Remove other users access the channel callback
    /// </summary>
    public void RemoveOnUserJoinedCallback() { onUserJoinCB = null; }

    /// <summary>
    /// Agora] Remove Another user disconnects from the channel callback
    /// </summary>
    public void RemoveOnUserOfflineCallback() { onUserOfflineCB = null; }

    /// <summary>
    ///  Agora] Remove channel disconnection callback
    /// </summary>
    public void RemoveOnLeaveChannelCallback() { onLeaveChannelCB = null; }

    ///// <summary>
    /////  Agora] Remove LocalAudioStateChanged callback
    ///// </summary>
    //public void RemoveOnLocalAudioStateChangedCallback() { onLocalAudioStateChangedCB = null; }

    /// <summary>
    ///  Agora] Remove RemoteAudioStateChanged callback
    /// </summary>
    public void RemovenRemoteAudioStateChangedCallback() { onRemoteAudioStateChangedCB = null; }

    /// <summary>
    ///  Agora] Remove LocalVideoStateChanged callback
    /// </summary>
    public void RemoveOnLocalVideoStateChangedCBCallback() { onLocalVideoStateChangedCB = null; }

    /// <summary>
    ///  Agora] Remove RemoteVideoStateChanged callback
    /// </summary>
    public void RemoveOnRemoteVideoStateChangedCallback() { onRemoteVideoStateChangedCB = null; }
    #endregion Agora Callback



    public void OnJoinChannelSusccess(string Channelname, uint uid, int elapsed)
    {
        makeVideoView(0);
        StartTranscoding(GetRtcEngine(), false);

    }


    private void makeVideoView(uint uid)
    {
        GameObject go = GameObject.Find(uid.ToString());
        remoteUid = uid;
        if (!ReferenceEquals(go, null))
        {
            return; // reuse
        }

        // create a GameObject and assign to this new user
        VideoSurface videoSurface = makeImageSurface(uid);
        _VideoSurface = videoSurface.gameObject;
        if (!ReferenceEquals(videoSurface, null))
        {
            // configure videoSurface
            videoSurface.SetForUser(uid);
            videoSurface.SetEnable(true);
            videoSurface.SetVideoSurfaceType(AgoraVideoSurfaceType.RawImage);
            videoSurface.SetGameFps(30);
        }

    }

    private VideoSurface makeImageSurface(uint uid)
    {
        throw new NotImplementedException();
    }

    void StartTranscoding(IRtcEngine mRtcEngine, bool ifRemoteUser = false)
    {
        if (!ifRemoteUser) return;
        if (ifRemoteUser)
        {
            //  mRtcEngine.RemovePublishStreamUrl(RTMP_URL);
        }

        var lt = new LiveTranscoding(); //트렌스 코딩 : 재생장치나 플랫폼에서 영상이 원활ㄹ하게 재생될 수 있도록 원본 동영상을 인코딩 해주는 작
        lt.videoBitrate = 400;
        lt.videoCodecProfile = VIDEO_CODEC_PROFILE_TYPE.VIDEO_CODEC_PROFILE_HIGH;
        lt.videoGop = 30;
        lt.videoFramerate = 24;
        lt.lowLatency = false;
        lt.audioSampleRate = AUDIO_SAMPLE_RATE_TYPE.AUDIO_SAMPLE_RATE_44100;
        lt.audioBitrate = 48;
        lt.audioChannels = 1;
        lt.audioCodecProfile = AUDIO_CODEC_PROFILE_TYPE.AUDIO_CODEC_PROFILE_LC_AAC;
        lt.liveStreamAdvancedFeatures = new LiveStreamAdvancedFeature[0];

        var localUesr = new TranscodingUser()
        {
            uid = 0,
            x = 0,
            y = 0,
            width = 360,
            height = 640,
            audioChannel = 0,
            alpha = 1.0,
        };

        if (ifRemoteUser)
        {
            var remoteUser = new TranscodingUser()
            {
                uid = remoteUid,
                x = 360,
                y = 0,
                width = 360,
                height = 640,
                audioChannel = 0,
                alpha = 1.0,
            };
            lt.userCount = 2;
            lt.width = 720;
            lt.height = 640;
            lt.transcodingUsers = new[] { localUesr, remoteUser };
        }
        else
        {
            lt.userCount = 1;
            lt.width = 360;
            lt.height = 640;
            lt.transcodingUsers = new[] { localUesr };
        }

        mRtcEngine.SetLiveTranscoding(lt);

        // var rc = mRtcEngine.AddPublishStreamUrl(RTMP_URL, true); // RTMP_URL 사용
    }


    public void SetOtherCallbackForHost()
    {
        if (GetRtcEngine() != null)
        {
            IRtcEngine rtc = GetRtcEngine();
            rtc.SetChannelProfile(CHANNEL_PROFILE.CHANNEL_PROFILE_LIVE_BROADCASTING);

            rtc.SetClientRole(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);

            rtc.SetVideoEncoderConfiguration(new VideoEncoderConfiguration
            {
                dimensions = new VideoDimensions { width = 480, height = 270 },
                frameRate = FRAME_RATE.FRAME_RATE_FPS_24
            });
        }
    }
    public void UnLoadEngine()
    {
        if (mRtcEngine == null)
        {
            Debug.Log("UnLoadEngine] mRtcEngine Null");
            return;
        }

        Debug.Log("UnLoadEngine] calling unloadEngine mRtcEngine");
        IRtcEngine.Destroy();
        mRtcEngine = null;
    }

    public void CreateRtcEngine()
    {
        UnLoadEngine();

        if (!string.IsNullOrEmpty(appId))
        {
            mRtcEngine = IRtcEngine.GetEngine(appId);
        }
    }
    public IRtcEngine GetRtcEngine()
    {
        return mRtcEngine;
    }

    /// <summary>
    /// 클라이언트 콜백 등록 및 비디오 캔버스 엑티브
    /// </summary>
    public void StartClientStreaming()
    {

        videoCanvas.SetActive(true);
        RemoveCallback();
        SetOnOtherCallback(() => SetOtherCallbackForHost());
        SetOnJoinChannelSuccessCallback(OnJoinChannelSusccess);
        EnterAgoraChannel(Define.AgoraState.LIVESTREAMING_HOST);
        //AgoraManager.instance.SetOnUserOfflineCallback(SetLeaveOtherUser);
        SetOnLeaveChannelCallback(UnloadEngine);
        //SetLeaveButtonCallBack(LeaveStreamingChannel, true);
        // StreamingObjectManager.instance.SetClientObjectActive(true);
       
    }




}
