using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ServerCore;


namespace DummyClient
{
    abstract class Packet
    {
        public ushort packetId;
        public abstract ArraySegment<byte> Write();
        public abstract void Read(ArraySegment<byte> s);
    }

    class PlayerInfoReq : Packet
    {
        public long playerId;
        public string playerName;


        public struct Skillinfo
        {
            public int id;
            public short level;
            public float duration;




            public bool Write(Span<byte> s, ref ushort count)
            {
                bool success = true;
                success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.id);
                count += sizeof(int);
                success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.level);
                count += sizeof(short);
                success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.duration);
                count += sizeof(float);

                return success;
            }

            public void Read(ReadOnlySpan<byte> s, ref ushort count)
            {
                this.id = BitConverter.ToInt32(s.Slice(count, s.Length - count));
                count += sizeof(int);
                this.level = BitConverter.ToInt16(s.Slice(count, s.Length - count));
                count += sizeof(short);
                this.duration = BitConverter.ToSingle(s.Slice(count, s.Length - count));
                count += sizeof(float);
            }
        }


        public List<Skillinfo> skills = new List<Skillinfo>();


        public PlayerInfoReq()
        {
            this.packetId = (ushort)PacketID.PlayerInfoReq;
        }

        public override void Read(ArraySegment<byte> segment)
        {
            ushort count = 0;

            ReadOnlySpan<byte> s = new ReadOnlySpan<byte>(segment.Array, segment.Offset, segment.Count);


            //ushort size = BitConverter.ToUInt16(s.Array, s.Offset);
            //count += 2;
            //ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + pos);
            //count += 2;

            this.playerId = BitConverter.ToInt64(s.Slice(count, s.Length - count));
            count += 8;


            //string

            ushort playerlen = BitConverter.ToUInt16(s.Slice(count, s.Length - count));

            count += sizeof(ushort);
            this.playerName = Encoding.Unicode.GetString(s.Slice(count, playerlen));
            count += playerlen;


            // ?? s.Length에서 카운트를 뺴는 이유는 무엇인가?
            // 커서를 이동시켜서 쓰기와 읽기를 한다고 생각하면될거같다.


            // skill list 

            // 00length를 ushort타입으로 추출하여서 길이를 알아내는 이유. 
            // span타입으로 저장되어있는 패킷 내의 정보를 추출하여서 read할때 해당길이에 어느 범위 (index)부터 추출하기 위함


            ushort skilllen = BitConverter.ToUInt16(s.Slice(count, s.Length - count)); // 읽을 index(범위)를 설정하여 넣어주고

            count += sizeof(ushort); //읽을 데이터형만큼의 count를 미리 증가시킨다.

            skills.Clear(); 
            for (int i = 0; i < skilllen; i++)
            {
                Skillinfo skillinfo = new Skillinfo();
                skillinfo.Read(s, ref count);

                this.skills.Add(skillinfo);

            }



        }

        public override ArraySegment<byte> Write()
        {
            ushort count = 0;
            bool success = true;


            ArraySegment<byte> segment = SendBufferHelper.Open(4096);
            Span<byte> s = new Span<byte>(segment.Array, segment.Offset, segment.Count);

            count += sizeof(ushort);
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.playerId);
            count += sizeof(ushort);
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), this.packetId);

            count += sizeof(long);


            // string
            ushort nameLen = (ushort)Encoding.Unicode.GetBytes(this.playerName, 0, this.playerName.Length,
                segment.Array, segment.Offset + count + sizeof(ushort));
            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), nameLen);


            count += sizeof(ushort);
            count += nameLen;



            // skill List

            success &= BitConverter.TryWriteBytes(s.Slice(count, s.Length - count), (ushort)this.skills.Count);
            count += sizeof(ushort);


            foreach (Skillinfo skill in skills)
            {
                success &= skill.Write(s, ref count);
            }


            success &= BitConverter.TryWriteBytes(s, count);

            if (success == false)
                return null;

            return SendBufferHelper.Close(count);

        }
    }

    class PlayerInfoOk : Packet
    {
        public int hp;
        public int attack;

        public override void Read(ArraySegment<byte> s)
        {

        }

        public override ArraySegment<byte> Write()
        {
            return null;
        }
    }

    public enum PacketID
    {
        PlayerInfoReq = 1,
    }

    class ServerSession : Session
    {
        static unsafe void ToBytes(byte[] array, int offset, ulong value)
        {
            fixed (byte* ptr = &array[offset])
                *(ulong*)ptr = value;
        }

        static unsafe void ToBytes<T>(byte[] array, int offset, T value) where T : unmanaged
        {
            fixed (byte* ptr = &array[offset])
                *(T*)ptr = value;
        }

        public override void OnConnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnConnected : {endPoint}");

            PlayerInfoReq packet = new PlayerInfoReq() { playerId = 1001, playerName = "ABCD" };

            packet.skills.Add(new PlayerInfoReq.Skillinfo() { id = 101, level = 1 , duration = 3.0f});
            packet.skills.Add(new PlayerInfoReq.Skillinfo() { id = 102, level = 2 , duration = 6.0f});
            packet.skills.Add(new PlayerInfoReq.Skillinfo() { id = 103, level = 3 , duration = 9.0f});

            ArraySegment<byte> s = packet.Write();
            if (s != null)
                Send(s);
        }



        public override void OnDisconnected(EndPoint endPoint)
        {
            Console.WriteLine($"OnDisconnected : {endPoint}");
        }

        public override int OnRecv(ArraySegment<byte> buffer)
        {
            string recvData = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
            Console.WriteLine($"[From Server] {recvData}");
            return buffer.Count;
        }

        public override void OnSend(int numOfBytes)
        {
            Console.WriteLine($"Transferred bytes: {numOfBytes}");
        }
    }

}
