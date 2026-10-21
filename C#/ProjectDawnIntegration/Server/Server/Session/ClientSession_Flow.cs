using System;
using System.Linq;
using Google.Protobuf.Protocol;
using Server.DB;
using Server.Game.Room;
namespace Server {
    public partial class ClientSession {
        public void HandleCinematicSkip(C_RaidCinematicSkip packet){
            var room=MyPlayer?.Room;
            if(packet==null||room==null||!room.UsesRaidCore||IsTransferring)return;
            room.Push(()=>room.RequestCinematicSkip(this,packet.Encounter));
        }
        private void GrantIntegrationStarterItems(AppDbContext db, PlayerDb player) {
            if(RaidProtocolVersion!=3) return;
            int slot=0;
            foreach(int id in new[]{1,100,91001,91002,91003,91004,91005}) {
                if(!Data.DataManager.ItemDict.ContainsKey(id))continue;
                db.Items.Add(new ItemDb{OwnerDbId=player.PlayerDbId,TemplateId=id,Count=1,Slot=slot++,Equipped=id==1||id==100});
            }
            db.SaveChanges();
        }
        public void HandleTownGate(C_TownGate packet) {
            var p=MyPlayer;var room=p?.Room;
            if(packet==null || room==null || room.RoomType!=RoomType.Town || IsTransferring || RaidMatchRequested) return;
            room.Push(()=>{
                if(p.Room!=room || IsTransferring || RaidMatchRequested) return;
                if(packet.PublicArea)TownSpawnService.ApplyLobbySpawn(p,room,"IntegrationGate");
                else TownSpawnService.ApplyMyRoomSpawn(p,room,"IntegrationGate");
                // Use room re-entry to refresh AOI after the authoritative portal teleport.
                room.LeaveRoom(p.Id,false);room.EnterRoom(p);
            });
        }
        public void HandleAppearance(C_Appearance packet) {
            var player=MyPlayer;var room=player?.Room;
            if(packet==null || player==null)return;
            Action<string> reject=error=>Send(new S_Appearance{PlayerId=player.Id,Look=player.Info.Appearance,Error=error});
            if(room==null || room.RoomType!=RoomType.Town || IsTransferring || RaidMatchRequested){reject("TownOnly");return;}
            if(!Data.AppearanceCatalog.Valid(packet.Look)){reject("InvalidLook");return;}
            room.Push(()=>{
                if(player.Room!=room || MyPlayer!=player || IsTransferring || RaidMatchRequested){reject("TownOnly");return;}
                try {
                    string saved;
                    using(var db=new AppDbContext())saved=AppearanceStore.Save(db,AccountDbId,player.PlayerDbId,packet.Look,packet.InitializeOnly);
                    player.Info.Appearance=saved;
                    room.Broadcast(new S_Appearance{PlayerId=player.Id,Look=saved});
                } catch(Exception e) {
                    Console.WriteLine("[APPEARANCE][SAVE_FAILED] "+e.GetType().Name);
                    reject("SaveFailed");
                }
            });
        }
        public void HandleRaidLeave(C_RaidLeave packet) {
            var room=MyPlayer?.Room;
            if(packet==null || room==null || !room.UsesRaidCore || IsTransferring)return;
            room.Push(()=>room.RequestRaidReturn(this,packet.Encounter));
        }
    }
}
