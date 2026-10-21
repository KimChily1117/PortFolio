using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Server.DB;
class Program
{
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
 static string Read(int id){using(var db=new AppDbContext())return db.Players.AsNoTracking().Single(p=>p.PlayerDbId==id).Appearance;}
 static int Main(string[] args)
 {
  PlayerDb player=null;string original=null;
  try {
   if(args.Length!=1 || !args[0].StartsWith("PD_Flow_",StringComparison.Ordinal))throw new Exception("Explicit test character required");
   using(var db=new AppDbContext()){player=db.Players.AsNoTracking().Single(p=>p.PlayerName==args[0]);original=player.Appearance;}
   if(string.IsNullOrEmpty(original))throw new Exception("Run FlowTests first");
   bool denied=false;
   try{using(var db=new AppDbContext())AppearanceStore.Save(db,-1,player.PlayerDbId,"skin:0001",false);}catch(InvalidOperationException){denied=true;}
   Check(denied && Read(player.PlayerDbId)==original,"foreign account cannot modify character appearance");
   bool failed=false;
   try{using(var db=new AppDbContext())AppearanceStore.Save(db,player.AccountDbId,player.PlayerDbId,new string('x',1200),false);}catch(Microsoft.Data.SqlClient.SqlException){failed=true;}
   Check(failed && Read(player.PlayerDbId)==original,"SQL write failure rolls back and preserves previous look");
   using(var db=new AppDbContext())db.Database.ExecuteSqlInterpolated($"UPDATE dbo.Player SET Appearance=NULL WHERE PlayerDbId={player.PlayerDbId}");
   using(var db=new AppDbContext())Check(AppearanceStore.Save(db,player.AccountDbId,player.PlayerDbId,original,true)==original,"first local import initializes a null SQL appearance");
   using(var db=new AppDbContext())Check(AppearanceStore.Save(db,player.AccountDbId,player.PlayerDbId,"skin:0001",true)==original,"late first-login import cannot replace a saved appearance");
   Check(Read(player.PlayerDbId)==original,"new DB context reads the committed look");
   Console.WriteLine("[DAWN_APPEARANCE_STORE_VALIDATED] "+checks+" checks");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}
  finally{if(player!=null && original!=null)using(var db=new AppDbContext())AppearanceStore.Save(db,player.AccountDbId,player.PlayerDbId,original,false);}
 }
}
