import base64,json,os,pathlib,subprocess,sys
# Never print secret payloads, command output, connection strings or certificate material.
try:
 role=sys.argv[1]
 assert role in ('server','gateway')
 secret=json.loads(subprocess.check_output(['aws','secretsmanager','get-secret-value','--secret-id',os.environ['DAWN_APP_SECRET'],'--region',os.environ['AWS_REGION'],'--query','SecretString','--output','text'],stderr=subprocess.DEVNULL))
 if not secret.get('configured'): raise ValueError('App configuration pending')
 root=pathlib.Path('/opt/projectdawn/release');env=os.environ.copy()
 if role=='server':
  connection=secret['connectionString']
  # Public deployment must verify the RDS TLS certificate; never silently weaken validation.
  lowered=connection.lower().replace(' ','')
  provider=secret.get('dbProvider','MySql')
  if provider=='MySql':
   if 'sslmode=verifyfull' not in lowered:raise ValueError('MySQL VerifyFull TLS configuration required')
  elif provider=='SqlServer':
   if 'encrypt=true' not in lowered or 'trustservercertificate=false' not in lowered:raise ValueError('SQL TLS configuration required')
  else:raise ValueError('Unknown DB provider')
  env['PROJECT_DAWN_DB_PROVIDER']=provider
  env['PROJECT_DAWN_DB_CONNECTION_STRING']=connection;env['PROJECT_DAWN_CONFIG_PATH']=str(root/'flow-server.config.json');env['PROJECT_DAWN_MATCH_HISTORY_DISABLED']='false'
  command=[str(root/'server/FlowHost'),'28182']
 else:
  private=pathlib.Path('/run/dawn-gateway');private.mkdir(mode=0o700,exist_ok=True)
  os.umask(0o077)
  (private/'server.pfx').write_bytes(base64.b64decode(secret['tlsPfxBase64'],validate=True))
  (private/'invites.json').write_text(json.dumps(secret['invites']))
  env['DAWN_TLS_PFX']=str(private/'server.pfx');env['DAWN_TLS_PASSWORD']=secret.get('tlsPassword','');env['DAWN_INVITES_FILE']=str(private/'invites.json');env['DAWN_PUBLIC_PORT']='28443'
  command=[str(root/'gateway/CloudGateway')]
 os.chdir(root);os.execve(command[0],command,env)
except Exception as error:
 print('[DAWN_CONFIG_NOT_READY] '+type(error).__name__,file=sys.stderr);sys.exit(1)
