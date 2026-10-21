using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using UnityEngine;

namespace DnfRaidDemo
{
    // Opt-in local evidence capture on the AudioListener. No microphone/network/desktop capture.
    public sealed class RaidAudioRecorder : MonoBehaviour
    {
        struct Block { public float[] Samples; public int Channels; public long Utc; }
        [Serializable] sealed class Report {
            public long firstSampleUtcTicks, samples; public int sampleRate, channels, droppedBlocks;
            public float peak; public double rms; public string scope;
        }
        readonly ConcurrentQueue<Block> queue = new ConcurrentQueue<Block>();
        readonly object captureGate=new object();
        volatile bool recording;
        int pending, dropped, rate, channels;
        long firstUtc, samples;
        double squareSum;
        float peak;
        string directory;
        BinaryWriter writer;
        public void Begin(string path)
        {
            directory=path;Directory.CreateDirectory(path);rate=AudioSettings.outputSampleRate;
            writer=new BinaryWriter(File.Create(Path.Combine(path,"game-audio.wav")));
            writer.Write(new byte[44]);recording=true;
        }
        void OnAudioFilterRead(float[] data,int channelCount)
        {
            lock(captureGate){
            if(!recording)return;
            if(Interlocked.Increment(ref pending)>100){Interlocked.Decrement(ref pending);Interlocked.Increment(ref dropped);return;}
            var copy=new float[data.Length];Array.Copy(data,copy,data.Length);
            queue.Enqueue(new Block{Samples=copy,Channels=channelCount,Utc=DateTime.UtcNow.Ticks});
            }
        }
        void Update(){Drain();}
        void Drain()
        {
            if(writer==null)return;
            Block block;
            while(queue.TryDequeue(out block)){
                Interlocked.Decrement(ref pending);
                if(firstUtc==0){firstUtc=block.Utc;channels=block.Channels;}
                foreach(float value in block.Samples){writer.Write(value);squareSum+=(double)value*value;peak=Math.Max(peak,Math.Abs(value));}
                samples+=block.Samples.Length;
            }
        }
        public void Finish()
        {
            lock(captureGate){recording=false;}if(writer==null)return;Drain();
            long bytes=samples*4;writer.Seek(0,SeekOrigin.Begin);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write((int)(36+bytes));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)3);
            writer.Write((short)channels);writer.Write(rate);writer.Write(rate*channels*4);writer.Write((short)(channels*4));writer.Write((short)32);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write((int)bytes);writer.Dispose();writer=null;
            var report=new Report{firstSampleUtcTicks=firstUtc,samples=samples,sampleRate=rate,channels=channels,
                droppedBlocks=dropped,peak=peak,rms=samples>0?Math.Sqrt(squareSum/samples):0,
                scope="Local player 0 AudioListener mix; BGM and actual game effects. UTC at first DSP callback; alignment approximate within audio buffering. No desktop or microphone audio."};
            File.WriteAllText(Path.Combine(directory,"game-audio.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[GAME_AUDIO_CAPTURE] samples="+samples+" peak="+peak+" dropped="+dropped);
        }
        void OnApplicationQuit(){Finish();}
        void OnDestroy(){Finish();}
    }
}
