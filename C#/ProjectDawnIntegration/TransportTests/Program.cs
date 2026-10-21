using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using ServerCore;
class Program {
 static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);checks++;}
 sealed class Peer:PacketSession {
  public int Disconnects;public readonly List<byte[]> Packets=new List<byte[]>();
  public override void OnConnected(EndPoint p){}public override void OnSend(int n){}public override void OnDisconnected(EndPoint p){Interlocked.Increment(ref Disconnects);}
  public override void OnRecvPacket(ArraySegment<byte> b){lock(Packets)Packets.Add(b.ToArray());}
 }
 static int Main(){try {
  var q=new BoundedSendQueue(100);var a=Enumerable.Range(0,60).Select(x=>(byte)x).ToArray();
  Check(q.TryEnqueue(new[]{new ArraySegment<byte>(a,3,19),new ArraySegment<byte>(a,25,30)}),"enqueue offset segments");
  var result=new List<byte>();var random=new Random(42);
  while(q.Bytes>0){var batch=q.Batch(17,2).SelectMany(x=>x).ToArray();int count=random.Next(1,batch.Length+1);result.AddRange(batch.Take(count));q.Consume(count);}
  Check(result.SequenceEqual(a.Skip(3).Take(19).Concat(a.Skip(25).Take(30))),"short sends preserve every suffix byte in order");
  q.TryEnqueue(new[]{new ArraySegment<byte>(a)});Check(!q.TryEnqueue(new[]{new ArraySegment<byte>(a)})&&q.Bytes==60,"overflow rejects entire enqueue without partial mutation");
  var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var client=new TcpClient();client.Connect((IPEndPoint)listener.LocalEndpoint);var socket=listener.AcceptSocket();listener.Stop();socket.SendBufferSize=1024;
  var peer=new Peer();peer.Start(socket);var frame=new byte[]{7,0,1,0,9,8,7};client.GetStream().Write(frame,0,1);client.GetStream().Write(frame,1,2);client.GetStream().Write(frame,3,4);
  Check(SpinWait.SpinUntil(()=>{lock(peer.Packets)return peer.Packets.Count==1;},3000),"fragmented TCP frame reassembles once");
  var payload=Enumerable.Range(0,512*1024).Select(x=>(byte)(x%251)).ToArray();peer.Send(new ArraySegment<byte>(payload));var actual=new byte[payload.Length];int read=0;client.ReceiveTimeout=5000;while(read<actual.Length){int n=client.GetStream().Read(actual,read,Math.Min(997,actual.Length-read));if(n==0)break;read+=n;}
  Check(actual.SequenceEqual(payload),"real TCP small-buffer large transfer has no truncation or corruption");
  peer.Send(new ArraySegment<byte>(new byte[4*1024*1024+1]));Check(SpinWait.SpinUntil(()=>peer.Disconnects==1,2000),"slow-consumer bound disconnects peer once");peer.Disconnect();peer.Send(new ArraySegment<byte>(frame));
  Check(peer.Disconnects==1&&peer.PendingSendBytes==0,"send after disconnect cannot requeue data");
  Console.WriteLine("[DAWN_TRANSPORT_VALIDATED] "+checks);return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
