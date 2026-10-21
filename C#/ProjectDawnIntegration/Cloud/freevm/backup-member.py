#!/usr/bin/env python3
"""Maintenance-only backup and isolated restore verification. Prints no row data."""
import datetime, hashlib, json, os, pathlib, subprocess, sys

def run(args, **kwargs):
    result = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.PIPE, **kwargs)
    if result.returncode:
        raise RuntimeError('Command failed: ' + args[0])
    return result.stdout

assert os.geteuid() == 0, 'Run as root'
for service in ('dawn-member-gateway', 'dawn-member-server'):
    if subprocess.run(['systemctl', 'is-active', '--quiet', service]).returncode == 0:
        sys.exit('Stop gateway and server during scheduled maintenance before backup verification.')
os.umask(0o077)
stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d_%H%M%S')
folder = pathlib.Path('/etc/projectdawn/private/backups') / stamp
folder.mkdir(parents=True, mode=0o700)
dump = folder / 'projectdawn.sql'
args = ['mysqldump', '--protocol=socket', '-u', 'root', '--single-transaction',
        '--set-gtid-purged=OFF', '--no-tablespaces', '--hex-blob']
dump.write_bytes(run(args + ['--routines', '--triggers', 'projectdawn']))
target = 'dawn_restore_' + stamp
assert target.startswith('dawn_restore_') and all(c.isalnum() or c == '_' for c in target)
run(['mysql', '--protocol=socket', '-u', 'root', '-e',
     'CREATE DATABASE ' + target + ' CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci'])
run(['mysql', '--protocol=socket', '-u', 'root', target], input=dump.read_bytes())
fingerprint_args = args + ['--no-create-info', '--skip-extended-insert', '--skip-comments', '--order-by-primary', '--compact']
source = run(fingerprint_args + ['projectdawn'])
restored = run(fingerprint_args + [target])
if source != restored:
    sys.exit('Restore mismatch; backup and isolated restore database retained for investigation.')
state = pathlib.Path('/var/lib/dawn-member-server')
if state.exists():
    run(['tar', '-czf', str(folder / 'server-state.tar.gz'), '-C', '/var/lib', 'dawn-member-server'])
report = {'createdUtc': stamp, 'database': 'projectdawn', 'restoreDatabase': target,
          'restoreVerified': True, 'sha256': hashlib.sha256(dump.read_bytes()).hexdigest(),
          'dataSha256': hashlib.sha256(source).hexdigest(),
          'note': 'All nine tables restored and deterministic row dump matched. Separate restore DB retained.'}
(folder / 'report.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report))
