using System;
using System.Linq;
using Google.Protobuf.Protocol;
using Server.DB;
namespace Server.Game.Object {
 public static class IntegrationEquipment {
  public static void Equip(Player player,C_EquipItem request) {
   if(player==null || request==null || player.Session.IsTransferring || player.Session.RaidMatchRequested || player.Room?.RoomType!=RoomType.Town)return;
   var item=player.Inven.Get(request.ItemDbId);
   if(item==null || item.ItemType==ItemType.Consumable)return;
   try {
    using(var db=new AppDbContext()) using(var tx=db.Database.BeginTransaction()) {
     var owned=db.Items.Where(x=>x.OwnerDbId==player.PlayerDbId).ToList();
     var row=owned.SingleOrDefault(x=>x.ItemDbId==request.ItemDbId);if(row==null)return;
     var changed=new System.Collections.Generic.List<Server.Game.Item>();
     if(request.Equipped) foreach(var other in player.Inven.Items.Values.Where(x=>x.Equipped && x.ItemDbId!=item.ItemDbId && x.ItemType==item.ItemType)) {
      if(item is Armor armor && other is Armor otherArmor && armor.ArmorType!=otherArmor.ArmorType)continue;
      var otherRow=owned.SingleOrDefault(x=>x.ItemDbId==other.ItemDbId);if(otherRow==null)return;
      otherRow.Equipped=false;changed.Add(other);
     }
     row.Equipped=request.Equipped;db.SaveChanges();tx.Commit();
     foreach(var other in changed){other.Equipped=false;player.Session.Send(new S_EquipItem{ItemDbId=other.ItemDbId,Equipped=false});}
     item.Equipped=request.Equipped;player.RefreshAdditionalStat();
     player.Session.Send(new S_EquipItem{ItemDbId=item.ItemDbId,Equipped=item.Equipped});
     player.Session.Send(player.EquipmentStats());
    }
   } catch(Exception e) {Console.WriteLine("[EQUIPMENT][SAVE_FAILED] "+e.GetType().Name);var list=new S_ItemList();list.Items.AddRange(player.Inven.Items.Values.Select(x=>x.Info.Clone()));player.Session.Send(list);}
  }
 }
}
