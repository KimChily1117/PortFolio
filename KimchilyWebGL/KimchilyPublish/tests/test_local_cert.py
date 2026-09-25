"""Certificate lifecycle tests use disposable keys; optional live checks never install trust."""
from datetime import datetime, timedelta, timezone
import hashlib
import importlib.util
import ipaddress
import json
import os
from pathlib import Path
import shutil
import ssl
import subprocess
import tempfile
import unittest
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('local_cert', ROOT/'tools/local-cert.py')
local_cert = importlib.util.module_from_spec(spec)
spec.loader.exec_module(local_cert)
from cryptography import x509
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.x509.oid import ExtendedKeyUsageOID, NameOID


class LocalCertificateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.baseline = tempfile.TemporaryDirectory(prefix='kimchily-cert-baseline-')
        cls.seed = Path(cls.baseline.name)/'tls'
        local_cert.prepare(cls.seed, '192.168.1.20', 8789)

    @classmethod
    def tearDownClass(cls):
        cls.baseline.cleanup()

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='kimchily-cert-test-')
        self.directory = Path(self.temporary.name)/'tls'
        shutil.copytree(self.seed, self.directory)
        self.now = datetime.now(timezone.utc)

    def tearDown(self):
        self.temporary.cleanup()

    def cert(self, name='ca'):
        return x509.load_pem_x509_certificate((self.directory/f'{name}.pem').read_bytes())

    def key(self, name='ca'):
        return serialization.load_pem_private_key((self.directory/f'{name}-key.pem').read_bytes(), password=None)

    def fingerprint(self, name='ca'):
        return self.cert(name).fingerprint(hashes.SHA256())

    def prepare(self):
        return local_cert.prepare(self.directory, '192.168.1.20', 8789)

    def replace_ca(self, *, is_ca=True, can_sign=True, starts=None, ends=None, issuer=None):
        key = self.key()
        subject = self.cert().subject
        cert = (x509.CertificateBuilder().subject_name(subject).issuer_name(issuer or subject)
                .public_key(key.public_key()).serial_number(x509.random_serial_number())
                .not_valid_before(starts or self.now-timedelta(minutes=5))
                .not_valid_after(ends or self.now+timedelta(days=365))
                .add_extension(x509.BasicConstraints(ca=is_ca, path_length=0 if is_ca else None), critical=True)
                .add_extension(x509.KeyUsage(False, False, False, False, False, can_sign, can_sign, False, False), critical=True)
                .sign(key, hashes.SHA256()))
        (self.directory/'ca.pem').write_bytes(cert.public_bytes(serialization.Encoding.PEM))

    def replace_leaf(self, *, starts=None, ends=None, purpose=None, names=None, signing_key=None, is_ca=False):
        ca, key = self.cert(), self.key('server')
        cert = (x509.CertificateBuilder().subject_name(self.cert('server').subject).issuer_name(ca.subject)
                .public_key(key.public_key()).serial_number(x509.random_serial_number())
                .not_valid_before(starts or self.now-timedelta(minutes=5))
                .not_valid_after(ends or self.now+timedelta(days=30))
                .add_extension(x509.BasicConstraints(ca=is_ca, path_length=None), critical=True)
                .add_extension(x509.KeyUsage(True, False, True, False, False, False, False, False, False), critical=True)
                .add_extension(x509.ExtendedKeyUsage(purpose or [ExtendedKeyUsageOID.SERVER_AUTH]), critical=False)
                .add_extension(x509.SubjectAlternativeName(names or [x509.IPAddress(ipaddress.ip_address('192.168.1.20')),
                    x509.IPAddress(ipaddress.ip_address('127.0.0.1')), x509.DNSName('localhost')]), critical=False)
                .sign(signing_key or self.key(), hashes.SHA256()))
        (self.directory/'server.pem').write_bytes(cert.public_bytes(serialization.Encoding.PEM))

    def test_generated_chain_has_ca_authority_server_auth_and_all_sans(self):
        ca, leaf = self.cert(), self.cert('server')
        ca.verify_directly_issued_by(ca)
        leaf.verify_directly_issued_by(ca)
        self.assertTrue(ca.extensions.get_extension_for_class(x509.BasicConstraints).critical)
        self.assertTrue(ca.extensions.get_extension_for_class(x509.BasicConstraints).value.ca)
        self.assertEqual(ca.extensions.get_extension_for_class(x509.BasicConstraints).value.path_length, 0)
        self.assertTrue(ca.extensions.get_extension_for_class(x509.KeyUsage).value.key_cert_sign)
        self.assertFalse(leaf.extensions.get_extension_for_class(x509.BasicConstraints).value.ca)
        self.assertIn(ExtendedKeyUsageOID.SERVER_AUTH, leaf.extensions.get_extension_for_class(x509.ExtendedKeyUsage).value)
        names = leaf.extensions.get_extension_for_class(x509.SubjectAlternativeName).value
        self.assertEqual(set(names.get_values_for_type(x509.IPAddress)), {ipaddress.ip_address('192.168.1.20'), ipaddress.ip_address('127.0.0.1')})
        self.assertIn('localhost', names.get_values_for_type(x509.DNSName))
        self.assertLessEqual(leaf.not_valid_before_utc, self.now)
        self.assertLessEqual(leaf.not_valid_after_utc, ca.not_valid_after_utc)

    def test_reuse_keeps_both_private_keys_and_certificate_fingerprints(self):
        before = {name: hashlib.sha256((self.directory/name).read_bytes()).digest() for name in ('ca.pem', 'ca-key.pem', 'server.pem', 'server-key.pem')}
        public = self.prepare()
        after = {name: hashlib.sha256((self.directory/name).read_bytes()).digest() for name in before}
        self.assertEqual(before, after)
        der = (self.directory/'ca.cer').read_bytes()
        self.assertEqual(public['caSha256'], hashlib.sha256(der).hexdigest())
        self.assertEqual(x509.load_der_x509_certificate(der).fingerprint(hashes.SHA256()), self.fingerprint())
        self.assertEqual(set(public), {'httpsUrl', 'caSha256', 'expiresAt', 'lanAddress'})

    def test_existing_ca_must_have_ca_role_signing_usage_and_valid_start(self):
        for change in ({'is_ca': False}, {'can_sign': False}, {'starts': self.now+timedelta(days=1)}):
            with self.subTest(change=tuple(change)):
                self.replace_ca(**change)
                fingerprint = self.fingerprint()
                with self.assertRaisesRegex(ValueError, 'certificate authority'):
                    self.prepare()
                self.assertEqual(fingerprint, self.fingerprint(), 'Existing CA must never be silently rotated')

    def test_existing_ca_must_be_self_signed(self):
        self.replace_ca(issuer=x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, 'Unknown CA')]))
        with self.assertRaisesRegex(ValueError, 'certificate authority'):
            self.prepare()

    def test_expiring_or_incomplete_ca_is_not_replaced(self):
        self.replace_ca(ends=self.now+timedelta(days=6))
        with self.assertRaisesRegex(ValueError, 'expiring'):
            self.prepare()
        (self.directory/'ca-key.pem').unlink()
        with self.assertRaisesRegex(ValueError, 'incomplete'):
            self.prepare()

    def test_future_or_client_only_leaf_is_renewed_without_rotating_ca(self):
        ca_fingerprint = self.fingerprint()
        for change in ({'starts': self.now+timedelta(days=1)}, {'purpose': [ExtendedKeyUsageOID.CLIENT_AUTH]}, {'is_ca': True}):
            with self.subTest(change=tuple(change)):
                self.replace_leaf(**change)
                invalid = self.fingerprint('server')
                self.prepare()
                self.assertNotEqual(invalid, self.fingerprint('server'))
                self.assertEqual(ca_fingerprint, self.fingerprint())

    def test_leaf_needs_loopback_sans_for_verified_health_checks(self):
        self.replace_leaf(names=[x509.IPAddress(ipaddress.ip_address('192.168.1.20'))])
        before = self.fingerprint('server')
        self.prepare()
        self.assertNotEqual(before, self.fingerprint('server'))
        names = self.cert('server').extensions.get_extension_for_class(x509.SubjectAlternativeName).value
        self.assertIn(ipaddress.ip_address('127.0.0.1'), names.get_values_for_type(x509.IPAddress))
        self.assertIn('localhost', names.get_values_for_type(x509.DNSName))

    def test_leaf_signature_is_verified_not_just_issuer_name(self):
        self.replace_leaf(signing_key=rsa.generate_private_key(public_exponent=65537, key_size=2048))
        before = self.fingerprint('server')
        self.prepare()
        self.assertNotEqual(before, self.fingerprint('server'))
        self.cert('server').verify_directly_issued_by(self.cert())

    def test_leaf_expiry_is_bounded_by_ca_and_near_expiry_is_renewed(self):
        self.replace_leaf(ends=self.now+timedelta(days=2))
        before = self.fingerprint('server')
        self.prepare()
        self.assertNotEqual(before, self.fingerprint('server'))
        self.assertLessEqual(self.cert('server').not_valid_after_utc, self.cert().not_valid_after_utc)

    def test_invalid_address_and_port_are_rejected_without_creating_keys(self):
        target = Path(self.temporary.name)/'unused'
        for address, port in [('8.8.8.8', 8789), ('127.0.0.1', 8789), ('::1', 8789), ('0.0.0.0', 8789), ('192.168.1.20', 0)]:
            with self.subTest(address=address, port=port), self.assertRaises(ValueError):
                local_cert.prepare(target, address, port)
        self.assertFalse(target.exists())

    @unittest.skipUnless(os.name == 'nt', 'Windows ACL assertion')
    def test_windows_private_acl_removes_inherited_broad_access(self):
        self.prepare()
        powershell = Path(os.environ['SystemRoot'])/'System32/WindowsPowerShell/v1.0/powershell.exe'
        literal = str(self.directory).replace("'", "''")
        command = "$dir='" + literal + "'; $current=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value; " + \
            "$paths=@($dir,(Join-Path $dir 'ca-key.pem'),(Join-Path $dir 'server-key.pem')); " + \
            "$out=foreach($p in $paths){$acl=Get-Acl -LiteralPath $p; [pscustomobject]@{protected=$acl.AreAccessRulesProtected; " + \
            "sids=@($acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier]) | ForEach-Object {$_.IdentityReference.Value})}}; " + \
            "@{current=$current;items=@($out)} | ConvertTo-Json -Depth 4 -Compress"
        environment = dict(os.environ, PSModulePath=str(powershell.parent/'Modules'))
        run = subprocess.run([str(powershell), '-NoProfile', '-NonInteractive', '-Command', command],
                             capture_output=True, text=True, check=True, env=environment)
        result = json.loads(run.stdout)
        expected = {result['current'], 'S-1-5-18', 'S-1-5-32-544'}
        for item in result['items']:
            self.assertTrue(item['protected'])
            self.assertEqual(set(item['sids']), expected)


@unittest.skipUnless(os.environ.get('KIMCHILY_LIVE_TLS_TEST') == '1', 'Opt-in, read-only running-server checks')
class RunningHttpsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tls = ROOT/'.local/tls'
        cls.metadata = json.loads((cls.tls/'public.json').read_text(encoding='utf-8-sig'))
        cls.base = cls.metadata['httpsUrl']
        cls.context = ssl.create_default_context(cafile=str(cls.tls/'ca.pem'))
        cls.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=cls.context))

    def get(self, path):
        return self.opener.open(self.base+path, timeout=5)

    def test_real_https_chain_and_public_endpoints(self):
        with self.get('/health') as response:
            self.assertEqual(json.load(response)['status'], 'ok')
        with self.get('/dev/connection') as response:
            public = json.load(response)
        self.assertEqual(set(public), {'httpsUrl', 'caSha256', 'expiresAt', 'lanAddress'})
        self.assertEqual(public['httpsUrl'], self.base)
        with self.get('/dev/ca.cer') as response:
            self.assertEqual(response.headers.get_content_type(), 'application/x-x509-ca-cert')
            der = response.read()
        self.assertEqual(hashlib.sha256(der).hexdigest(), public['caSha256'])
        self.assertEqual(x509.load_der_x509_certificate(der).fingerprint(hashes.SHA256()),
                         x509.load_pem_x509_certificate((self.tls/'ca.pem').read_bytes()).fingerprint(hashes.SHA256()))
        with self.get('/dev/setup') as response:
            text = response.read().decode('utf-8')
        self.assertIn('/dev/ca.cer', text)
        self.assertNotIn('PRIVATE KEY', text)

    def test_private_keys_and_arbitrary_tls_files_are_not_public(self):
        for path in ('/dev/ca-key.pem', '/dev/server-key.pem', '/dev/ca.pem', '/dev/server.pem',
                     '/tls/ca-key.pem', '/.local/tls/ca-key.pem', '/app/../.local/tls/ca-key.pem',
                     '/dev/%2e%2e/tls/ca-key.pem', '/dev/connection?path=ca-key.pem'):
            with self.subTest(path=path), self.assertRaises(urllib.error.HTTPError) as raised:
                self.get(path)
            self.assertEqual(raised.exception.code, 404)


if __name__ == '__main__':
    unittest.main()
