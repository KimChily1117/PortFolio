using UnityEngine;
namespace DnfRaidDemo
{
    public sealed partial class RaidDemoApp
    {
        int ViewedRoom => sim==null || sim.Players.Count==0 ? 4:sim.Players[Mathf.Clamp(net.LocalPlayerId,0,sim.Players.Count-1)].RoomId;
        Vector2 MarkScreenPoint(PlayerState player){Vector2 shown;return ScreenPoint(displayPositions.TryGetValue(player.Id,out shown)?shown:player.Position);}
        void DrawRoomFloor()
        {
            Texture2D backdrop=sim.PhaseTwoBattlefieldActive?phaseTwoBattlefield:sim.RoomsEnabled&&ViewedRoom!=4&&originalCorridor!=null?originalCorridor:arena;
            if(sim.PhaseTwoBattlefieldActive&&backdrop==null){LoadBakalPhaseTwo();backdrop=phaseTwoBattlefield;}
            if(backdrop!=null){var kick=DawnShake();float cover=Mathf.Max(Mathf.Abs(kick.x),Mathf.Abs(kick.y));GUI.DrawTexture(new Rect(kick.x-cover,kick.y-cover,1280+cover*2,720+cover*2),backdrop);}
            if(!sim.RoomsEnabled)return;
            int activeFires=0,lastFireEnd=0;
            foreach(var fire in sim.RoomFires) {
                if(fire.RoomId!=ViewedRoom||sim.TickIndex<fire.StartTick||sim.TickIndex>=fire.EndTick)continue;
                activeFires++;lastFireEnd=Mathf.Max(lastFireEnd,fire.EndTick);
                float age=(sim.TickIndex-fire.StartTick)/60f;
                float alpha=Mathf.Clamp01((fire.EndTick-sim.TickIndex)/30f);
                for(int y=-2;y<=2;y+=2)for(int x=-8;x<=8;x+=2)
                {
                    var at=ScreenPoint(new Vector2(x,y));float phase=age+(x+8)*.13f+(y+2)*.19f;
                    OriginalWorld("world_ground_magic",at,.44f,phase,new Color(1,1,1,.65f*alpha));
                    OriginalWorld("world_ground_flame",at,.38f,phase,new Color(1,1,1,.8f*alpha));
                }
            }
            if(activeFires>0)Label(new Rect(355,228,570,26),"불바다 "+activeFires+"곳 · 모두 소멸까지 "+Mathf.CeilToInt((lastFireEnd-sim.TickIndex)/60f)+"초 · 다른 방으로 이동",17,new Color(1,.6f,.3f),true,TextAnchor.MiddleCenter);
            DrawRoomDoor(-1,0,new Vector2(-8.8f,0),"◀");DrawRoomDoor(1,0,new Vector2(8.8f,0),"▶");
            DrawRoomDoor(0,-1,new Vector2(0,2.85f),"▲");DrawRoomDoor(0,1,new Vector2(0,-2.85f),"▼");
            DrawRoomMarkGauges();
        }
        Texture2D meteorMarkOutline;
        void DrawRoomMarkGauges()
        {
            // Same projected server tick used by the falling meteor. No local wall-clock timer.
            float progress=Mathf.Clamp01(1-(sim.MarkDeadline-sim.TickIndex)/(float)RaidSimulation.MarkTicks);
            foreach(int id in sim.MarkTargets){
                var player=sim.Players[id];if(player.RoomId!=ViewedRoom)continue;
                var at=MarkScreenPoint(player);
                // User-confirmed reference: a large ground ellipse, filled radially
                // from its centre, not an angular countdown or a white sprite wipe.
                // This is a timing indicator; its size does not change the hit area.
                const float width=320, height=134;
                if(meteorMarkOutline==null)meteorMarkOutline=Radial(256,.975f,false);
                if(progress>0)TintTexture(disc,new Rect(at.x-width*progress*.5f,at.y-height*progress*.5f,width*progress,height*progress),new Color(1,.025f,.015f,.36f));
                TintTexture(meteorMarkOutline,new Rect(at.x-width*.5f,at.y-height*.5f,width,height),new Color(1,.06f,.035f,.95f));
            }
        }
        void DrawRoomDoor(int dx,int dy,Vector2 world,string arrow)
        {
            int next=RaidSimulation.NeighborRoom(ViewedRoom,dx,dy);if(next<0)return;
            Vector2 at=ScreenPoint(world);var rect=new Rect(at.x-28,at.y-23,56,46);
            OriginalWorld("world_room_portal",at+new Vector2(0,18),.38f,sim.TickIndex/60f,Color.white);
            Label(rect,arrow,23,Color.white,true,TextAnchor.MiddleCenter);
        }
        void DrawRoomOverlay()
        {
            if(!sim.RoomsEnabled)return;
            Fill(new Rect(1050,135,205,195),new Color(.025f,.025f,.035f,.94f));
            Outline(new Rect(1050,135,205,195),gold,1);
            Label(new Rect(1058,139,189,21),"방 "+(ViewedRoom+1)+" / 9",14,gold,true,TextAnchor.MiddleCenter);
            float nextMark=Mathf.Max(0,(sim.NextRoomMarkTick-sim.TickIndex)/60f);
            Label(new Rect(1058,160,189,17),nextMark>0?"다음 표식 "+nextMark.ToString("0.0")+"초":"다음 표식 준비 중",12,Color.white,false,TextAnchor.MiddleCenter);
            for(int room=0;room<9;room++) {
                var rect=new Rect(1058+room%3*64,180+room/3*45,60,41);
                int fireCount=0,expires=0;bool incoming=false,markedRoom=false;
                foreach(var fire in sim.RoomFires){
                    if(fire.RoomId!=room||fire.EndTick<=sim.TickIndex)continue;
                    if(fire.StartTick>sim.TickIndex){incoming=true;continue;}
                    fireCount++;expires=Mathf.Max(expires,fire.EndTick);
                }
                int occupants=0;foreach(var p in sim.Players)if(!p.Departed && p.RoomId==room){occupants++;if(System.Array.IndexOf(sim.MarkTargets,p.Id)>=0)markedRoom=true;}
                Fill(rect,fireCount>0?new Color(.43f,.065f,.015f):room==ViewedRoom?new Color(.045f,.25f,.3f):new Color(.12f,.12f,.15f));
                Outline(rect,room==ViewedRoom?cyan:room==4?gold:Color.gray,room==ViewedRoom?2:1);
                if(markedRoom)Outline(new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6),new Color(1,.55f,.1f),1);
                int shown=0;
                for(int id=0;id<4;id++){
                    var actor=sim.Players.Find(p=>p.Id==id&&!p.Departed&&p.RoomId==room);if(actor==null)continue;
                    bool marked=System.Array.IndexOf(sim.MarkTargets,id)>=0;
                    Label(new Rect(rect.center.x-occupants*6+shown++*12,rect.y+3,12,17),(id+1).ToString(),13,marked?new Color(1,.6f,.15f):Color.white,true,TextAnchor.MiddleCenter);
                }
                if(occupants==0)Label(new Rect(rect.x,rect.y+3,rect.width,17),room==4?"B":"·",12,Color.white,true,TextAnchor.MiddleCenter);
                string status=fireCount>0?Mathf.CeilToInt((expires-sim.TickIndex)/60f)+"초"+(fireCount>1?" ×"+fireCount:""):incoming?"낙하":"안전";
                Label(new Rect(rect.x,rect.y+23,rect.width,15),status,11,fireCount>0?new Color(1,.65f,.4f):incoming?gold:new Color(.55f,.7f,.65f),false,TextAnchor.MiddleCenter);
            }
            Label(new Rect(1058,314,189,14),"주황 숫자: 표식 · 초: 불바다",10,new Color(.9f,.75f,.6f),false,TextAnchor.MiddleCenter);
            if(sim.MarkTargets.Length>0) {
                float remaining=Mathf.Max(0,(sim.MarkDeadline-sim.TickIndex)/60f);
                string slots=string.Join(" · ",System.Array.ConvertAll(sim.MarkTargets,id=>(id+1)+"P"));
                Label(new Rect(280,188,720,32),slots+" 메테오 표식 · "+remaining.ToString("0.0")+"초 · 서로 다른 빈 방으로 유도",18,new Color(1,.6f,.2f),true,TextAnchor.MiddleCenter);
                foreach(int id in sim.MarkTargets) {
                    var target=sim.Players[id];if(target.RoomId!=ViewedRoom)continue;
                    var at=MarkScreenPoint(target);
                    OriginalWorld("world_target_mark",at-new Vector2(0,172),1f,sim.TickIndex/60f,Color.white);
                    Label(new Rect(at.x-55,at.y-225,110,28),remaining.ToString("0.0"),22,new Color(1,.4f,.1f),true,TextAnchor.MiddleCenter);
                }
            }
            var me=sim.Players[Mathf.Clamp(net.LocalPlayerId,0,sim.Players.Count-1)];
            float fade=1-Mathf.Clamp01((sim.TickIndex-me.RoomChangedTick)/18f);
            if(fade>0)Fill(new Rect(0,130,1280,590),new Color(0,0,0,fade*.85f));
            if(showHelp)Label(new Rect(360,635,560,22),"문의 화살표 방향으로 이동 · 메테오/불바다는 해당 방에서만 적용",12,Color.white,false,TextAnchor.MiddleCenter);
        }
        int autoMarkDeadline,autoDepositRoom=-1;
        int AutoNextRoom(int from,int destination)
        {
            var me=sim.Players[net.LocalPlayerId];int best=-1;
            float bestCost=float.MaxValue;
            int distance=Mathf.Abs(from%3-destination%3)+Mathf.Abs(from/3-destination/3);
            foreach(int delta in new[]{-3,3,-1,1}) {
                int next=RaidSimulation.NeighborRoom(from,delta==-1?-1:delta==1?1:0,delta==-3?-1:delta==3?1:0);
                if(next<0||Mathf.Abs(next%3-destination%3)+Mathf.Abs(next/3-destination/3)>=distance)continue;
                int dx=next%3-from%3,dy=next/3-from/3;
                float cost=dx!=0?Mathf.Abs(me.Position.y)+8.7f-dx*me.Position.x:Mathf.Abs(me.Position.x)+2.8f+dy*me.Position.y;
                foreach(var fire in sim.RoomFires)if(fire.RoomId==next&&fire.EndTick>sim.TickIndex)cost+=100;
                if(cost<bestCost){bestCost=cost;best=next;}
            }
            return best;
        }
        PlayerCommand RoomAutoCommand(PlayerCommand command)
        {
            if(!sim.RoomsEnabled)return command;
            var p=sim.Players[net.LocalPlayerId];int index=System.Array.IndexOf(sim.MarkTargets,p.Id);bool marked=index>=0;
            if(marked&&autoMarkDeadline!=sim.MarkDeadline) {
                autoMarkDeadline=sim.MarkDeadline;
                int wave=Mathf.Max(0,(sim.MarkDeadline-RaidSimulation.MarkTicks-300)/RaidSimulation.RoomMarkInterval);
                autoDepositRoom=wave%2==0?(index==0?0:8):(index==0?2:6);
            }
            if(!marked&&p.RoomId==4)return command;
            command.Skills=0;command.Basic=command.Jump=command.Dash=false;command.Run=p.DashTime<=0&&p.StunTime<=0&&p.AttackTime<=0;
            if(marked&&p.AttackTime>0&&p.Cooldowns[(int)SkillId.Dash]<=0)command.Dash=true;
            if(marked&&p.RoomId==autoDepositRoom) {
                command.Run=false;float x=p.RoomId%3==0?6f:-6f;
                command.Move=Mathf.Abs(p.Position.x-x)>.12f?new Vector2(Mathf.Sign(x-p.Position.x),0):Mathf.Abs(p.Position.y)>.12f?new Vector2(0,-Mathf.Sign(p.Position.y)):Vector2.zero;
                return command;
            }
            // First wave shows the actual fire briefly after dodging the impact vertically.
            // All commands still pass through the ordinary authoritative input path.
            var fire=sim.RoomFires.FindLast(f=>f.RoomId==p.RoomId);
            if(!marked&&autoMarkDeadline==720&&p.RoomId==autoDepositRoom&&fire!=null&&sim.TickIndex<fire.StartTick+8) {
                command.Move=p.Position.y<2.05f?Vector2.up:Vector2.zero;return command;
            }
            int next=AutoNextRoom(p.RoomId,marked?autoDepositRoom:4);
            if(next<0){command.Move=Vector2.zero;return command;}
            int dx=next%3-p.RoomId%3,dy=next/3-p.RoomId/3;
            if(marked&&next==autoDepositRoom&&sim.RoomFires.Exists(f=>f.RoomId==next&&f.EndTick>sim.TickIndex)) {
                // The previous fire expires before this mark locks. Wait outside its door.
                Vector2 waitAt=dx!=0?new Vector2(dx*7.7f,0):new Vector2(0,-dy*2f);
                Vector2 offset=waitAt-p.Position;command.Move=offset.magnitude>.12f?offset.normalized:Vector2.zero;
                command.Dash=false;return command;
            }
            command.Move=dx!=0?(Mathf.Abs(p.Position.y)>.12f?new Vector2(0,-Mathf.Sign(p.Position.y)):new Vector2(dx,0)):
                (Mathf.Abs(p.Position.x)>.12f?new Vector2(-Mathf.Sign(p.Position.x),0):new Vector2(0,-dy));
            return command;
        }
    }
}
