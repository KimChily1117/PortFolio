#nullable disable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DnfRaidDemo;
namespace ProjectDawn.Combat
{
    // Schema is compiled and finite; payloads never name CLR types. Float bits are preserved across Mono/CoreCLR.
    public static class RaidSnapshotCodec
    {
        private static readonly ConcurrentDictionary<Type,FieldInfo[]> Fields=new ConcurrentDictionary<Type,FieldInfo[]>();
        private static FieldInfo[] GetFields(Type t)=>Fields.GetOrAdd(t,k=>k.GetFields(BindingFlags.Public|BindingFlags.Instance).OrderBy(f=>f.Name,StringComparer.Ordinal).ToArray());
        public static byte[] Encode(RaidSimulation s)
        {
            using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream,Encoding.UTF8))
            { writer.Write(16);Write(writer,typeof(RaidCheckpoint),s.Checkpoint());return stream.ToArray(); }
        }
        public static RaidSimulation Decode(byte[] bytes)
        {
            if(bytes.Length>60000)throw new InvalidDataException("Raid snapshot size");
            using(var stream=new MemoryStream(bytes)) using(var reader=new BinaryReader(stream,Encoding.UTF8))
            {
                if(reader.ReadInt32()!=16)throw new InvalidDataException("Raid snapshot version");
                var state=(RaidCheckpoint)Read(reader,typeof(RaidCheckpoint));
                if(stream.Position!=stream.Length || state.Players.Length<1 || state.Players.Length>4)throw new InvalidDataException("Raid snapshot framing");
                return RaidSimulation.Restore(state);
            }
        }
        private static void Write(BinaryWriter w,Type t,object v)
        {
            if(t==typeof(int)||t.IsEnum){w.Write(Convert.ToInt32(v));return;} if(t==typeof(uint)){w.Write((uint)v);return;}
            if(t==typeof(float)){w.Write((float)v);return;}if(t==typeof(bool)){w.Write((bool)v);return;}
            if(t==typeof(string)){w.Write((string)v??"");return;}
            if(t.IsArray){var a=(Array)v;w.Write(a.Length);foreach(var x in a)Write(w,t.GetElementType(),x);return;}
            foreach(var f in GetFields(t))Write(w,f.FieldType,f.GetValue(v));
        }
        private static object Read(BinaryReader r,Type t)
        {
            if(t.IsEnum)return Enum.ToObject(t,r.ReadInt32());if(t==typeof(int))return r.ReadInt32();if(t==typeof(uint))return r.ReadUInt32();
            if(t==typeof(float))return r.ReadSingle();if(t==typeof(bool))return r.ReadBoolean();if(t==typeof(string))return r.ReadString();
            if(t.IsArray){int n=r.ReadInt32();if(n<0||n>4096)throw new InvalidDataException("Raid collection limit");var a=Array.CreateInstance(t.GetElementType(),n);
                for(int i=0;i<n;i++)a.SetValue(Read(r,t.GetElementType()),i);return a;}
            object value=Activator.CreateInstance(t);foreach(var f in GetFields(t))f.SetValue(value,Read(r,f.FieldType));return value;
        }
    }
}
