using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
namespace ProjectDawn.Cloud;

public interface IMemberAccounts {
 Task<bool> Register(string account,string password,CancellationToken token);
 Task<bool> Verify(string account,string password,CancellationToken token);
}
public static class MemberPasswords {
 public const int Iterations=600000;
 public static bool ValidId(string? value)=>Regex.IsMatch(value??"","\\A[a-zA-Z0-9_-]{3,32}\\z");
 public static bool ValidPassword(string? value)=>value!=null&&value.Length>=8&&value.Length<=128&&!string.IsNullOrWhiteSpace(value)&&!value.Any(char.IsControl);
 public static byte[] Hash(string password,byte[] salt,int iterations=Iterations)=>Rfc2898DeriveBytes.Pbkdf2(password,salt,iterations,HashAlgorithmName.SHA256,32);
}
public sealed class MySqlMemberAccounts : IMemberAccounts {
 readonly string connection;readonly int maxAccounts;
 static readonly byte[] dummySalt=RandomNumberGenerator.GetBytes(32),dummyHash=RandomNumberGenerator.GetBytes(32);
 public MySqlMemberAccounts(string connection,int maxAccounts=64){if(maxAccounts<1||maxAccounts>1000)throw new ArgumentOutOfRangeException(nameof(maxAccounts));this.connection=connection;this.maxAccounts=maxAccounts;}
 static MySqlCommand Command(MySqlConnection c,MySqlTransaction? tx,string sql,params (string,object)[] args){var cmd=c.CreateCommand();cmd.Transaction=tx;cmd.CommandText=sql;cmd.CommandTimeout=5;foreach(var (name,value) in args)cmd.Parameters.AddWithValue(name,value);return cmd;}
 public async Task<bool> Register(string account,string password,CancellationToken token) {
  if(!MemberPasswords.ValidId(account)||!MemberPasswords.ValidPassword(password))return false;
  account=account.ToLowerInvariant();var salt=RandomNumberGenerator.GetBytes(32);var hash=MemberPasswords.Hash(password,salt);
  using var c=new MySqlConnection(connection);await c.OpenAsync(token);using var tx=await c.BeginTransactionAsync(token);
  try {
   using(var gate=Command(c,tx,"SELECT Id FROM authregistrationgate WHERE Id=1 FOR UPDATE")){if(await gate.ExecuteScalarAsync(token)==null)return false;}
   using(var count=Command(c,tx,"SELECT COUNT(*) FROM accountcredential")){if(Convert.ToInt64(await count.ExecuteScalarAsync(token))>=maxAccounts)return false;}
   // A legacy account can never be claimed by registering its name.
   using(var insert=Command(c,tx,"INSERT INTO account(AccountName) VALUES(@id)",("@id",account))){await insert.ExecuteNonQueryAsync(token);}
   long id;using(var last=Command(c,tx,"SELECT LAST_INSERT_ID()")){id=Convert.ToInt64(await last.ExecuteScalarAsync(token));}
   using(var insert=Command(c,tx,"INSERT INTO accountcredential(AccountDbId,LoginId,PasswordSalt,PasswordHash,Iterations,HashAlgorithm,CreatedUtc) VALUES(@pk,@id,@salt,@hash,@iterations,'PBKDF2-SHA256',UTC_TIMESTAMP(6))",("@pk",id),("@id",account),("@salt",salt),("@hash",hash),("@iterations",MemberPasswords.Iterations))){await insert.ExecuteNonQueryAsync(token);}
   await tx.CommitAsync(token);return true;
  }catch(MySqlException e)when(e.Number==1062){await tx.RollbackAsync(CancellationToken.None);return false;}
  finally{CryptographicOperations.ZeroMemory(hash);}
 }
 public async Task<bool> Verify(string account,string password,CancellationToken token) {
  if(!MemberPasswords.ValidId(account)||!MemberPasswords.ValidPassword(password))return false;
  using var c=new MySqlConnection(connection);await c.OpenAsync(token);
  byte[] salt=dummySalt,expected=dummyHash;int iterations=MemberPasswords.Iterations;bool found=false,enabled=false;long id=0;
  using(var select=Command(c,null,"SELECT AccountDbId,PasswordSalt,PasswordHash,Iterations,HashAlgorithm,Disabled FROM accountcredential WHERE LoginId=@id",("@id",account.ToLowerInvariant())))
  using(var reader=await select.ExecuteReaderAsync(token)) {
   if(await reader.ReadAsync(token)){id=reader.GetInt32(0);salt=(byte[])reader.GetValue(1);expected=(byte[])reader.GetValue(2);iterations=reader.GetInt32(3);found=reader.GetString(4)=="PBKDF2-SHA256";enabled=!reader.GetBoolean(5);}
  }
  if(salt.Length!=32||expected.Length!=32||iterations<MemberPasswords.Iterations||iterations>2000000)return false;
  var supplied=MemberPasswords.Hash(password,salt,iterations);bool valid=CryptographicOperations.FixedTimeEquals(supplied,expected)&&found&&enabled;CryptographicOperations.ZeroMemory(supplied);
  if(valid){using var update=Command(c,null,"UPDATE accountcredential SET LastLoginUtc=UTC_TIMESTAMP(6) WHERE AccountDbId=@id",("@id",id));await update.ExecuteNonQueryAsync(token);}
  return valid;
 }
}
// Bounded in-memory limits complement the database's persistent account quota.
public sealed class AuthRateLimits {
 readonly Dictionary<string,(long Since,int Count)> windows=new();readonly object gate=new();
 public bool Allow(string key,int limit,long durationMs,long now) {
  lock(gate){
   if(windows.TryGetValue(key,out var state)&&now-state.Since<durationMs){if(state.Count>=limit)return false;windows[key]=(state.Since,state.Count+1);return true;}
   if(windows.Count>=4096){foreach(var old in windows.Where(p=>now-p.Value.Since>=3600000).Select(p=>p.Key).ToArray())windows.Remove(old);if(windows.Count>=4096&&!windows.ContainsKey(key))return false;}
   windows[key]=(now,1);return true;
  }
 }
}
