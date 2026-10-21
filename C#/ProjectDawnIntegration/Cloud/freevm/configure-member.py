#!/usr/bin/env python3
"""Fresh dedicated VM setup. Secrets remain root-only; never print SQL or env values."""
import json,os,pathlib,secrets,subprocess,sys

def run(args,**kwargs):
    result=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.PIPE,**kwargs)
    if result.returncode: raise RuntimeError('Command failed: '+args[0])
    return result.stdout

try:
    assert os.geteuid()==0
    root=pathlib.Path('/opt/projectdawn/release')
    assert (root/'server/FlowHost').is_file()
    private=pathlib.Path('/etc/projectdawn/private');private.mkdir(mode=0o700,exist_ok=True)
    os.umask(0o077)
    creds=private/'database.json'
    if not creds.exists():creds.write_text(json.dumps({'game':secrets.token_hex(32),'auth':secrets.token_hex(32)}))
    passwords=json.loads(creds.read_text())
    for password in passwords.values():assert len(password)==64 and all(c in '0123456789abcdef' for c in password)
    tls=pathlib.Path('/etc/mysql/dawn-tls');tls.mkdir(mode=0o750,exist_ok=True)
    if not (tls/'server-cert.pem').exists():
        run(['openssl','req','-x509','-newkey','rsa:2048','-nodes','-days','3650','-subj','/CN=ProjectDawn Local DB CA','-keyout',str(private/'mysql-ca-key.pem'),'-out','/etc/projectdawn/mysql-ca.pem'])
        run(['openssl','req','-newkey','rsa:2048','-nodes','-subj','/CN=localhost','-keyout',str(tls/'server-key.pem'),'-out',str(tls/'server.csr')])
        ext=tls/'extensions.cnf';ext.write_text('subjectAltName=DNS:localhost,IP:127.0.0.1\nextendedKeyUsage=serverAuth\n')
        run(['openssl','x509','-req','-in',str(tls/'server.csr'),'-CA','/etc/projectdawn/mysql-ca.pem','-CAkey',str(private/'mysql-ca-key.pem'),'-CAcreateserial','-days','365','-sha256','-extfile',str(ext),'-out',str(tls/'server-cert.pem')])
    os.chmod('/etc/projectdawn/mysql-ca.pem',0o644)
    (tls/'ca.pem').write_bytes(pathlib.Path('/etc/projectdawn/mysql-ca.pem').read_bytes())
    run(['chown','-R','root:mysql',str(tls)])
    os.chmod(tls,0o750)
    for path in tls.iterdir():os.chmod(path,0o640)
    pathlib.Path('/etc/mysql/mysql.conf.d/projectdawn-tls.cnf').write_text('[mysqld]\nssl-ca=/etc/mysql/dawn-tls/ca.pem\nssl-cert=/etc/mysql/dawn-tls/server-cert.pem\nssl-key=/etc/mysql/dawn-tls/server-key.pem\n')
    os.chmod('/etc/mysql/mysql.conf.d/projectdawn-tls.cnf',0o644)
    run(['systemctl','restart','mysql'])
    for role,service in (('game','dawn-server'),('auth','dawn-gateway')):
        client=pathlib.Path('/etc/projectdawn/db-client-'+role);client.mkdir(mode=0o750,exist_ok=True)
        if not (client/'client-cert.pem').exists():
            run(['openssl','req','-newkey','rsa:2048','-nodes','-subj','/CN=dawn_'+role,'-keyout',str(client/'client-key.pem'),'-out',str(client/'client.csr')])
            extension=client/'extensions.cnf';extension.write_text('extendedKeyUsage=clientAuth\n')
            run(['openssl','x509','-req','-in',str(client/'client.csr'),'-CA','/etc/projectdawn/mysql-ca.pem','-CAkey',str(private/'mysql-ca-key.pem'),'-CAcreateserial','-days','365','-sha256','-extfile',str(extension),'-out',str(client/'client-cert.pem')])
        # Oracle Connector/NET PEM parser expects traditional PKCS#1 RSA keys.
        run(['openssl','rsa','-in',str(client/'client-key.pem'),'-traditional','-out',str(client/'client-key.next')])
        (client/'client-key.next').replace(client/'client-key.pem')
        run(['chown','-R','root:'+service,str(client)]);os.chmod(client,0o750)
        for path in client.iterdir():os.chmod(path,0o640)
    sql='CREATE DATABASE IF NOT EXISTS projectdawn CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;\nUSE projectdawn;\n'
    sql+=(root/'ops/001_initial.sql').read_text(encoding='utf-8-sig')+'\n'+(root/'ops/002_member_auth.sql').read_text(encoding='utf-8-sig')
    for role in ('game','auth'):
        sql+=f"\nCREATE USER IF NOT EXISTS 'dawn_{role}'@'localhost' IDENTIFIED BY '{passwords[role]}' REQUIRE SSL;\nALTER USER 'dawn_{role}'@'localhost' REQUIRE SSL;\n"
    for table in ('player','item','matchhistory','matchhistorymember','raidrewardencounter','raidrewardgrant'):
        sql+=f'GRANT SELECT,INSERT,UPDATE,DELETE ON projectdawn.{table} TO dawn_game@localhost;\n'
    sql+='GRANT SELECT ON projectdawn.account TO dawn_game@localhost;\n'
    sql+='GRANT SELECT,INSERT ON projectdawn.account TO dawn_auth@localhost;\n'
    sql+='GRANT SELECT,INSERT,UPDATE ON projectdawn.accountcredential TO dawn_auth@localhost;\n'
    sql+='GRANT SELECT,UPDATE ON projectdawn.authregistrationgate TO dawn_auth@localhost;\n'
    run(['mysql','--protocol=socket','-u','root'],input=sql.encode())
    def connection(role,pool):return f'Server=localhost;Port=3306;Database=projectdawn;User ID=dawn_{role};Password={passwords[role]};SslMode=VerifyFull;SslCa=/etc/projectdawn/mysql-ca.pem;SslCert=/etc/projectdawn/db-client-{role}/client-cert.pem;SslKey=/etc/projectdawn/db-client-{role}/client-key.pem;Pooling=true;MaximumPoolSize={pool};Connection Timeout=5;Default Command Timeout=10;'
    pathlib.Path('/etc/projectdawn/server.env').write_text('PROJECT_DAWN_DB_CONNECTION_STRING="'+connection('game',16)+'"\n')
    pathlib.Path('/etc/projectdawn/gateway.env').write_text('DAWN_AUTH_DB_CONNECTION_STRING="'+connection('auth',8)+'"\nDAWN_TLS_PFX=/etc/projectdawn/tls/server.pfx\nDAWN_TLS_PASSWORD=\n')
    for role in ('server','gateway'):
        source=root/f'ops/freevm/dawn-member-{role}.service'
        run(['install','-m','0644',str(source),f'/etc/systemd/system/dawn-member-{role}.service'])
    run(['systemctl','daemon-reload'])
    run(['systemctl','enable','--now','dawn-member-server'])
    print('[DAWN_MEMBER_DB_READY] Separate DML roles / verified DB TLS / additive schema / game server started')
except Exception as error:
    print('[DAWN_MEMBER_SETUP_FAILED] '+type(error).__name__,file=sys.stderr);sys.exit(1)
