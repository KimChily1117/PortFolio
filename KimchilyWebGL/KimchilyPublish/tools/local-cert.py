"""Create a development CA and LAN server certificate; never install system trust."""
from __future__ import annotations

import argparse
from datetime import datetime, timedelta, timezone
import hashlib
import ipaddress
import json
import os
from pathlib import Path
import ssl
import subprocess
import sys
import urllib.request

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / '.deps'))


def write_private(path: Path, value: bytes):
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    with os.fdopen(descriptor, 'wb') as stream:
        stream.write(value)


def protect_directory(directory: Path):
    """Restrict existing and future key files without changing their contents."""
    for path in (directory, *directory.parents):
        if path.is_symlink() or getattr(path, 'is_junction', lambda: False)():
            raise ValueError('TLS paths cannot contain symbolic links or junctions.')
    directory.mkdir(parents=True, exist_ok=True)
    for path in directory.iterdir():
        if not path.is_file() or path.is_symlink() or getattr(path, 'is_junction', lambda: False)():
            raise ValueError('The TLS directory must contain only regular certificate and metadata files.')
    if os.name == 'nt':
        powershell = Path(os.environ['SystemRoot'])/'System32/WindowsPowerShell/v1.0/powershell.exe'
        # Python may inherit PowerShell 7's module path. Windows PowerShell 5
        # must load its own security cmdlets when applying Windows ACLs.
        environment = dict(os.environ, PSModulePath=str(powershell.parent/'Modules'))
        result = subprocess.run([str(powershell), '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
                                 '-File', str(Path(__file__).with_name('protect-tls.ps1')), '-Directory', str(directory)],
                                capture_output=True, text=True, check=False, env=environment)
        if result.returncode != 0 or 'TLS_PRIVATE_ACL_READY' not in result.stdout:
            raise ValueError('Could not restrict the TLS certificate directory to this Windows account, SYSTEM and Administrators.')
    else:
        directory.chmod(0o700)
        for path in directory.iterdir():
            path.chmod(0o600)


def prepare(directory: Path, address: str, port: int):
    from cryptography import x509
    from cryptography.exceptions import InvalidSignature
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID
    ip = ipaddress.ip_address(address)
    if ip.version != 4 or not ip.is_private or ip.is_loopback or ip.is_unspecified:
        raise ValueError('Choose an active private LAN IPv4 address.')
    if not 1 <= port <= 65535:
        raise ValueError('Invalid HTTPS port.')
    protect_directory(directory)
    now = datetime.now(timezone.utc)
    ca_path, ca_key_path = directory/'ca.pem', directory/'ca-key.pem'
    if ca_path.exists() != ca_key_path.exists():
        raise ValueError('The existing development CA is incomplete. Restore its matching key/certificate; it was not replaced.')
    if ca_path.exists():
        ca = x509.load_pem_x509_certificate(ca_path.read_bytes())
        key = serialization.load_pem_private_key(ca_key_path.read_bytes(), password=None)
        if ca.public_key().public_numbers() != key.public_key().public_numbers():
            raise ValueError('The development CA key does not match its certificate.')
        try:
            constraints = ca.extensions.get_extension_for_class(x509.BasicConstraints).value
            usage = ca.extensions.get_extension_for_class(x509.KeyUsage).value
            ca.verify_directly_issued_by(ca)
            if not constraints.ca or not usage.key_cert_sign or ca.not_valid_before_utc > now:
                raise ValueError('Invalid CA role or validity period.')
        except (ValueError, InvalidSignature, x509.ExtensionNotFound) as error:
            raise ValueError('The existing development CA is not a currently valid self-signed certificate authority. It was not replaced.') from error
        if ca.not_valid_after_utc < now + timedelta(days=7):
            raise ValueError('The development CA is expiring. Plan certificate renewal and phone trust installation.')
    else:
        key = rsa.generate_private_key(public_exponent=65537, key_size=3072)
        name = x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'Kimchily LAN Development CA')])
        ca = (x509.CertificateBuilder().subject_name(name).issuer_name(name).public_key(key.public_key())
              .serial_number(x509.random_serial_number()).not_valid_before(now-timedelta(minutes=5))
              .not_valid_after(now+timedelta(days=3650))
              .add_extension(x509.BasicConstraints(ca=True, path_length=0), critical=True)
              .add_extension(x509.KeyUsage(False, False, False, False, False, True, True, False, False), critical=True)
              .add_extension(x509.SubjectKeyIdentifier.from_public_key(key.public_key()), critical=False)
              .sign(key, hashes.SHA256()))
        write_private(ca_key_path, key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
        ca_path.write_bytes(ca.public_bytes(serialization.Encoding.PEM))
    cert_path, key_path = directory/'server.pem', directory/'server-key.pem'
    cert = None
    if cert_path.exists() and key_path.exists():
        try:
            candidate = x509.load_pem_x509_certificate(cert_path.read_bytes())
            candidate_key = serialization.load_pem_private_key(key_path.read_bytes(), password=None)
            candidate.verify_directly_issued_by(ca)
            names = candidate.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
            addresses = names.get_values_for_type(x509.IPAddress)
            constraints = candidate.extensions.get_extension_for_class(x509.BasicConstraints).value
            usage = candidate.extensions.get_extension_for_class(x509.KeyUsage).value
            purpose = candidate.extensions.get_extension_for_class(x509.ExtendedKeyUsage).value
            if (ip in addresses and ipaddress.ip_address('127.0.0.1') in addresses
                    and 'localhost' in names.get_values_for_type(x509.DNSName)
                    and not constraints.ca and usage.digital_signature and not usage.key_cert_sign
                    and ExtendedKeyUsageOID.SERVER_AUTH in purpose
                    and candidate.not_valid_before_utc <= now
                    and candidate.not_valid_after_utc > now+timedelta(days=7)
                    and candidate.not_valid_after_utc <= ca.not_valid_after_utc
                    and candidate.public_key().public_numbers() == candidate_key.public_key().public_numbers()):
                cert = candidate
        except (ValueError, InvalidSignature, x509.ExtensionNotFound):
            pass
    if cert is None:
        leaf_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        cert = (x509.CertificateBuilder()
                .subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'Kimchily LAN Web App')]))
                .issuer_name(ca.subject).public_key(leaf_key.public_key())
                .serial_number(x509.random_serial_number()).not_valid_before(now-timedelta(minutes=5))
                .not_valid_after(min(now+timedelta(days=90), ca.not_valid_after_utc))
                .add_extension(x509.BasicConstraints(ca=False, path_length=None), critical=True)
                .add_extension(x509.KeyUsage(True, False, True, False, False, False, False, False, False), critical=True)
                .add_extension(x509.ExtendedKeyUsage([ExtendedKeyUsageOID.SERVER_AUTH]), critical=False)
                .add_extension(x509.SubjectAlternativeName([x509.IPAddress(ip), x509.IPAddress(ipaddress.ip_address('127.0.0.1')), x509.DNSName('localhost')]), critical=False)
                .add_extension(x509.AuthorityKeyIdentifier.from_issuer_public_key(key.public_key()), critical=False)
                .sign(key, hashes.SHA256()))
        write_private(key_path, leaf_key.private_bytes(serialization.Encoding.PEM, serialization.PrivateFormat.PKCS8, serialization.NoEncryption()))
        cert_path.write_bytes(cert.public_bytes(serialization.Encoding.PEM))
    der = ca.public_bytes(serialization.Encoding.DER)
    (directory/'ca.cer').write_bytes(der)
    public = {'httpsUrl': f'https://{ip}:{port}', 'caSha256': hashlib.sha256(der).hexdigest(),
              'expiresAt': cert.not_valid_after_utc.isoformat(), 'lanAddress': str(ip)}
    (directory/'public.json').write_text(json.dumps(public, indent=2), encoding='utf-8')
    return public


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path)
    parser.add_argument('--address')
    parser.add_argument('--port', type=int, default=8789)
    parser.add_argument('--check')
    parser.add_argument('--ca', type=Path)
    args = parser.parse_args()
    if args.check:
        context = ssl.create_default_context(cafile=str(args.ca))
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=context))
        with opener.open(args.check.rstrip('/')+'/health', timeout=1.5) as response:
            health = json.load(response)
        if health.get('service') != 'Kimchily local publisher' or health.get('status') != 'ok':
            raise ValueError('Unexpected HTTPS service.')
        print('HTTPS_HEALTH_VERIFIED')
    else:
        if not args.directory or not args.address:
            parser.error('--directory and --address are required for certificate setup')
        # Keep the lexical path so prepare can reject a redirected input path.
        print(json.dumps(prepare(args.directory.absolute(), args.address, args.port)))


if __name__ == '__main__':
    main()
