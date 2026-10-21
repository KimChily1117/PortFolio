using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using ProjectDawn.Combat;
using ProjectDawn.Network;

namespace DnfRaidDemo
{
    // Existing Unity renderer consumes this adapter's confirmed state plus personal visual prediction.
    public sealed class DawnRaidTransport : IDisposable
    {
        public readonly RaidNetworkClient Client=new RaidNetworkClient();
        private TcpClient tcp;
        private Stream stream;
        private readonly object networkGate=new object();
        private readonly ConcurrentQueue<Action> dispatch=new ConcurrentQueue<Action>();
        private Thread worker;
        private volatile bool stopped;
        private bool started;
        public string EndReason {get;private set;}="";
        private readonly List<byte> bytes=new List<byte>();
        private RaidPresentationTimeline timeline;
        private long tickUs, selfId, lastFrameServerUs,lastFrameLocalUs;
        private readonly List<string> presentationAudit=new List<string>();
        public int TimelineSamples,TimelineMaxLeadTicks;
        private readonly Stopwatch clock=Stopwatch.StartNew();
        private RaidSimulation confirmed;
        private double lastSend;
        private int pendingSkills,pendingButtons;
        public int LocalSlot {get;private set;}
        public int ConfirmedTick=>confirmed?.TickIndex??0;
        public bool Connected=>started && !stopped;
        public string Status {get;private set;}="서버 연결 중";
        public int Frames {get;private set;}
        public int SentInputs {get;private set;}
        public int Rejections {get;private set;}
        public long LastEvent {get;private set;}
        public readonly HashSet<int> Skills=new HashSet<int>();
        public readonly HashSet<int> Patterns=new HashSet<int>();
        public readonly HashSet<string> FinalHashes=new HashSet<string>();
        public event Action<CombatEvent> PersonalFeedback;
        public event Action<RaidEvent> FinalEvent;
        public int ClockMappedInputs,ClockFallbackInputs;
        public long ClockRttUs,ClockVersion;
        public bool ClockTrusted;
        public int MeteorAvoids {get;private set;}
        public int GroundAvoids {get;private set;}
        public float MaximumJump {get;private set;}
        private readonly List<string> audit=new List<string>();
        public event Action<IMessage> FlowMessage;
        readonly bool automatic;
        public void SendFlow(IMessage message)=>Send(message);
        public DawnRaidTransport(string host,int port,bool automatic=true,string account="",string invite="",string password=null)
        {
            this.automatic=automatic;
            stream=password==null?CloudTransport.Connect(host,port,account,invite,out tcp):CloudTransport.ConnectMember(host,port,account,password,out tcp);
            Client.PatternPlanner=new LuaPatternPlanner(UnityEngine.Resources.Load<UnityEngine.TextAsset>("DawnLua/bakal_patterns").text);
            Client.Send=Send;
            Client.Started+=s=>Enqueue(()=>{
                EndReason="";pendingSkills=pendingButtons=0;lastSend=0;
                Frames=SentInputs=Rejections=ClockMappedInputs=ClockFallbackInputs=TimelineSamples=TimelineMaxLeadTicks=MeteorAvoids=GroundAvoids=0;
                LastEvent=s.EventSequence;MaximumJump=0;ClockRttUs=ClockVersion=0;ClockTrusted=false;
                Skills.Clear();Patterns.Clear();FinalHashes.Clear();audit.Clear();presentationAudit.Clear();
                started=true;lastFrameServerUs=s.ServerTimeUs;lastFrameLocalUs=Client.NowUs;
                LocalSlot=s.PlayerSlots.IndexOf(s.Self);confirmed=RaidSnapshotCodec.Decode(s.Content.ToByteArray());tickUs=s.TickUs;selfId=s.Self;
                timeline=new RaidPresentationTimeline(LocalSlot);timeline.Feedback+=e=>PersonalFeedback?.Invoke(e);
                timeline.Activated+=(z,activation,observed,p)=>{
                    if(z.Tag=="DawnMeteorImpact" || z.Tag=="DawnMeteorShockwave")presentationAudit.Add("zone "+z.Id+" "+z.Tag+" "+activation+" "+observed+" "+p.JumpHeight.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                    if(z.Tag=="DawnMeteorShockwave" && z.RoomId==p.RoomId && p.Hp>0) {
                        if(p.Invulnerable>0)presentationAudit.Add("invulnerable "+z.Id+" "+activation);
                        else if(System.Math.Max(p.JumpHeight,RaidSimulation.SkillElevation(p))>=RaidSimulation.JumpDodgeHeight)presentationAudit.Add("safe "+z.Id+" "+activation);
                    }
                };
                Status=confirmed.Players.Count+"인 협동 · "+(Client.IsHost?"방장":"참가자")+" · 서버 검증";
                UnityEngine.Debug.Log("[DAWN_UNITY_STARTED] slot="+LocalSlot+" host="+Client.IsHost+" encounter="+s.Encounter);
            });
            Client.Frame+=f=>{long received=Client.NowUs;Enqueue(()=>{
                lastFrameServerUs=f.ServerTimeUs;lastFrameLocalUs=received;
                if(f.Content.Length==0)return;
                confirmed=RaidSnapshotCodec.Decode(f.Content.ToByteArray());Frames++;
                // Audit confirmed bytes, never predicted HP. Shared tick/hash comparisons work across four processes.
                if(f.FinalizedTick%30==0) {using(var sha=System.Security.Cryptography.SHA256.Create()) {
                    string hash=BitConverter.ToString(sha.ComputeHash(f.Content.ToByteArray())).Replace("-","");
                    audit.Add(f.FinalizedTick+" "+hash);FinalHashes.Add(f.FinalizedTick+" "+hash);}}
                foreach(var p in confirmed.Players)MaximumJump=Math.Max(MaximumJump,p.JumpHeight);
            });};
            Client.Committed+=e=>Enqueue(()=>{LastEvent=e.Sequence;if(e.Kind=="ContentSkill")Skills.Add(e.Damage);
                if(e.Kind=="ContentPattern")Patterns.Add(e.Damage); if(e.Kind=="ContentDodge")Skills.Add(3);
                if(e.Kind=="ContentJumpAvoid")GroundAvoids++;
                if(e.Kind=="ContentMeteorJumpAvoid"){GroundAvoids++;MeteorAvoids++;}
                if(e.Origin!=null && e.Origin.Player==selfId && (e.Kind=="ActionRejected" || e.Kind=="InputRejected"))timeline?.Reject(e.Origin.Sequence);
                if(e.Kind=="ContentMeteorJumpAvoid" && e.Target==selfId)presentationAudit.Add("avoid "+e.Damage+" "+e.EvaluatedTick);
                if(e.Kind=="ContentMeteorShockwave")presentationAudit.Add("shock "+e.Damage+" "+e.EvaluatedTick);
                if(e.Kind=="ContentMeteorImpact")presentationAudit.Add("impact "+e.Damage+" "+e.EvaluatedTick);
                FinalEvent?.Invoke(e);
                if(e.Kind.StartsWith("Content")) audit.Add("event "+e.Sequence+" "+e.EvaluatedTick+" "+e.Kind+" "+e.Target+" "+e.Damage);
            });
            Client.Ended+=e=>Enqueue(()=>{started=false;EndReason=e.Reason;Status="전투 종료: "+e.Reason;UnityEngine.Debug.Log("[DAWN_UNITY_ENDED] "+e.Reason);});
            Client.ClockChanged+=c=>Enqueue(()=>{ClockRttUs=c.RoundTripUs;ClockVersion=c.Version;ClockTrusted=c.Trusted;});
            Client.InputReceived+=r=>{if(r.Input.Key.Player==Client.Start.Self)
                Enqueue(()=>{
                    bool accepted=r.Result==(int)Admission.Admitted || r.Result==(int)Admission.Duplicate && r.OriginalResult==(int)Admission.Admitted;
                    timeline?.Acknowledge(r.Input.Key.Sequence,r.EffectiveTick,accepted);
                    if(r.Result==(int)Admission.Admitted){if(r.UsedReceiveTime)ClockFallbackInputs++;else ClockMappedInputs++;}
                });};
            Client.Negotiate(3);
            worker=new Thread(NetworkLoop){IsBackground=true,Name="Dawn raid transport and host proposals"};worker.Start();
        }
        private void Enqueue(Action action)
        {
            if(dispatch.Count>=4096)throw new IOException("Presentation queue exceeded bound");
            dispatch.Enqueue(action);
        }
        private void NetworkLoop()
        {
            try {while(!stopped){ReceivePackets();}}
            catch(Exception e){if(!stopped)dispatch.Enqueue(()=>{EndReason="TransportFailure";Status=e.Message;started=false;UnityEngine.Debug.LogWarning("[DAWN_DISCONNECTED] "+e.GetType().Name);});}finally{tcp?.Close();}
        }
        private void ReceivePackets()
        {
            if(tcp==null)return;
            if(bytes.Count<4 || bytes.Count<(bytes[0]|bytes[1]<<8)) {
                var incoming=new byte[65536];int n=stream.Read(incoming,0,incoming.Length);
                if(n==0)throw new IOException("서버 연결이 종료되었습니다.");bytes.AddRange(incoming.Take(n));
            }
            int processed=0;
            while(bytes.Count>=4 && processed++<256)
            {
                int size=bytes[0]|(bytes[1]<<8);if(size<4)throw new InvalidDataException("Invalid Dawn frame");if(bytes.Count<size)break;
                var id=(MsgId)(bytes[2]|(bytes[3]<<8));var body=bytes.GetRange(4,size-4).ToArray();bytes.RemoveRange(0,size);
                IMessage message=Parse(id,body);if(message==null)continue;
                if(automatic && message is S_RaidHello hello && hello.Version==3) {Enqueue(()=>Status="4인 매칭 대기");Send(new C_CreateRoom());}
                if(automatic && message is S_SceneMove move) {Enqueue(()=>Status=move.TargetRoomType==RoomType.Town?"마을 복귀 완료":"네 명 입장 준비 중");
                    Send(new C_SceneReady {TargetRoomId=move.TargetRoomId,TransferId=move.TransferId,TargetRoomType=move.TargetRoomType,SceneType=move.SceneType});}
                if(message is S_RaidCinematic)Enqueue(()=>{started=false;Status="레이드 클리어 · 시네마틱";});
                if(message is S_RaidRewards rewards && rewards.Status!="History")Enqueue(()=>{started=false;Status=rewards.Status=="Saved"?"레이드 클리어 · 보상 저장 완료":"레이드 클리어 · 보상 저장 중";});
                if(message is S_RaidInput receipt && receipt.Result!=(int)Admission.Admitted && receipt.Result!=(int)Admission.Duplicate)Enqueue(()=>Rejections++);
                if(message is S_RaidProposalResult proposal && proposal.Result!=(int)Admission.Admitted && proposal.Result!=(int)Admission.Duplicate)Enqueue(()=>Rejections++);
                if(!automatic)Enqueue(()=>FlowMessage?.Invoke(message));
                lock(networkGate)Client.Handle(message);
            }
        }
        public void Poll(RaidSimulation display)
        {
            int count=0;
            while(count++<4096 && dispatch.TryDequeue(out var action))action();
            long now;
            lock(networkGate) {
                ClockTrusted=Client.ClockTrusted;
                now=ClockTrusted?Client.NowUs+Client.ClockState.OffsetUs:lastFrameServerUs+Math.Max(0,Client.NowUs-lastFrameLocalUs);
            }
            if(confirmed==null)return;
            var projected=timeline.Project(confirmed,started?now/tickUs:confirmed.TickIndex);
            if(ClockTrusted && started) {TimelineSamples++;TimelineMaxLeadTicks=Math.Max(TimelineMaxLeadTicks,projected.TickIndex-confirmed.TickIndex);}
            display.Players=projected.Players;display.Boss=projected.Boss;display.Zones=projected.Zones;display.Events=projected.Events;
            display.NextRoomMarkTick=projected.NextRoomMarkTick;display.PendingRoomMarkTick=projected.PendingRoomMarkTick;display.MarkTargets=projected.MarkTargets;display.PreviousMarkMask=projected.PreviousMarkMask;display.RoomsEnabled=projected.RoomsEnabled;display.MarkTarget=projected.MarkTarget;display.MarkDeadline=projected.MarkDeadline;display.RoomFires=projected.RoomFires;display.BossMoveUntil=projected.BossMoveUntil;
            display.Mode=confirmed.Mode;display.Elapsed=projected.Elapsed;display.TickIndex=projected.TickIndex;display.TimeLimit=confirmed.TimeLimit;
            display.IntroEnabled=confirmed.IntroEnabled;display.PhaseTransitionStartTick=confirmed.PhaseTransitionStartTick;
            display.ReviveCharges=confirmed.ReviveCharges;display.WipeDeadlineTick=confirmed.WipeDeadlineTick;
        }
        public void Input(PlayerCommand command)
        {
            if(!Connected)return;
            command.PlayerId=LocalSlot;
            pendingSkills|=command.Skills;pendingButtons|=(command.Jump?4:0)|(command.Dash?8:0)|(command.Revive?16:0)|(command.Potion?32:0);
            if(clock.Elapsed.TotalSeconds-lastSend<.045 && pendingSkills==0 && pendingButtons==0)return;
            lastSend=clock.Elapsed.TotalSeconds;
            var send=new PlayerCommand {PlayerId=LocalSlot,Move=command.Move,Basic=command.Basic,Run=command.Run,
                Skills=pendingSkills,Jump=(pendingButtons&4)!=0,Dash=(pendingButtons&8)!=0,Revive=(pendingButtons&16)!=0,Potion=(pendingButtons&32)!=0};
            pendingSkills=pendingButtons=0;lock(networkGate){if(Client.Start!=null){var packet=Client.Controls(send);
                long stamp=Client.ClockTrusted?packet.ClientTimeUs+Client.ClockState.OffsetUs:lastFrameServerUs+Math.Max(0,Client.NowUs-lastFrameLocalUs);
                timeline.Track(packet.Key.Sequence,Math.Max(1,stamp/tickUs+1),send);SentInputs++;}}
        }
        public void WriteAudit(string folder)
        {
            Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,"presentation-audit.txt"),presentationAudit);File.WriteAllLines(Path.Combine(folder,"confirmed-audit.txt"),audit);
            File.WriteAllText(Path.Combine(folder,"network-result.json"),UnityEngine.JsonUtility.ToJson(new Result {slot=LocalSlot,
                frames=Frames,inputs=SentInputs,rejections=Rejections,events=LastEvent,skills=Skills.OrderBy(i=>i).ToArray(),patterns=Patterns.OrderBy(i=>i).ToArray(),
                timelineSamples=TimelineSamples,timelineMaxLeadTicks=TimelineMaxLeadTicks,clockMappedInputs=ClockMappedInputs,clockFallbackInputs=ClockFallbackInputs,clockRttUs=ClockRttUs,clockVersion=ClockVersion,
                players=confirmed?.Players.Count??0,bossHp=confirmed?.Boss.Hp??0,jump=MaximumJump,groundAvoids=GroundAvoids,meteorAvoids=MeteorAvoids,luaRequests=Client.PatternPlanner.Requests,luaConfirmed=Client.PatternPlanner.Confirmations,status=Status,endReason=EndReason},true));
        }
        [Serializable] private sealed class Result {public int timelineSamples,timelineMaxLeadTicks,clockMappedInputs,clockFallbackInputs;public long clockRttUs,clockVersion;public int slot,frames,inputs,rejections,players,groundAvoids,meteorAvoids,luaRequests,luaConfirmed;public long events;public int[] skills,patterns;public float bossHp,jump;public string status,endReason;}
        private void Send(IMessage message)
        {
            if(tcp==null)return;byte[] body=message.ToByteArray();if(body.Length>65531)throw new InvalidDataException("Outbound frame too large");
            var packet=new byte[body.Length+4];ushort id=(ushort)(MsgId)Enum.Parse(typeof(MsgId),message.Descriptor.Name.Replace("_",""));
            Array.Copy(BitConverter.GetBytes((ushort)packet.Length),packet,2);Array.Copy(BitConverter.GetBytes(id),0,packet,2,2);Array.Copy(body,0,packet,4,body.Length);
            lock(networkGate){if(!stopped)stream.Write(packet,0,packet.Length);}
        }
        private static IMessage Parse(MsgId id,byte[] b)
        {
            switch(id){
                case MsgId.SChat:return S_Chat.Parser.ParseFrom(b);
                case MsgId.SChatContext:return S_ChatContext.Parser.ParseFrom(b);
                case MsgId.SEquipmentStats:return S_EquipmentStats.Parser.ParseFrom(b);
                case MsgId.SRaidResume:return S_RaidResume.Parser.ParseFrom(b);
                case MsgId.SRaidRewards:return S_RaidRewards.Parser.ParseFrom(b);
                case MsgId.SRaidCinematic:return S_RaidCinematic.Parser.ParseFrom(b);
                case MsgId.SChannelList:return S_ChannelList.Parser.ParseFrom(b);
                case MsgId.SAppearance:return S_Appearance.Parser.ParseFrom(b);
                case MsgId.SChannelMove:return S_ChannelMove.Parser.ParseFrom(b);
                case MsgId.SConnected:return S_Connected.Parser.ParseFrom(b);
                case MsgId.SLogin:return S_Login.Parser.ParseFrom(b);
                case MsgId.SCreatePlayer:return S_CreatePlayer.Parser.ParseFrom(b);
                case MsgId.SEnterGame:return S_EnterGame.Parser.ParseFrom(b);
                case MsgId.SSpawn:return S_Spawn.Parser.ParseFrom(b);
                case MsgId.SDespawn:return S_Despawn.Parser.ParseFrom(b);
                case MsgId.SMove:return S_Move.Parser.ParseFrom(b);
                case MsgId.SItemList:return S_ItemList.Parser.ParseFrom(b);
                case MsgId.SEquipItem:return S_EquipItem.Parser.ParseFrom(b);
                case MsgId.SCreateRoom:return S_CreateRoom.Parser.ParseFrom(b);
                case MsgId.SEnterParty:return S_EnterParty.Parser.ParseFrom(b);
                case MsgId.SRaidClockChallenge:return S_RaidClockChallenge.Parser.ParseFrom(b);case MsgId.SRaidClockState:return S_RaidClockState.Parser.ParseFrom(b);
                case MsgId.SRaidHello:return S_RaidHello.Parser.ParseFrom(b);case MsgId.SRaidStart:return S_RaidStart.Parser.ParseFrom(b);
                case MsgId.SRaidInput:return S_RaidInput.Parser.ParseFrom(b);case MsgId.SRaidFrame:return S_RaidFrame.Parser.ParseFrom(b);
                case MsgId.SRaidProposalResult:return S_RaidProposalResult.Parser.ParseFrom(b);case MsgId.SRaidEnded:return S_RaidEnded.Parser.ParseFrom(b);
                case MsgId.SSceneMove:return S_SceneMove.Parser.ParseFrom(b);default:return null;}
        }
        public void Dispose(){stopped=true;tcp?.Close();worker?.Join(500);stream?.Dispose();tcp=null;}
    }
}
