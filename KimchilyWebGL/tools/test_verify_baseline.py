import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('verify_baseline', Path(__file__).with_name('verify_baseline.py'))
verify = importlib.util.module_from_spec(spec)
spec.loader.exec_module(verify)


class BaselineVerificationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.base = Path(self.temp.name)
        self.source = self.base / 'original'
        self.clone = self.base / 'clone'
        self.source.mkdir()
        self.clone.mkdir()
        self.original = self.source / 'scene.unity'
        self.original.write_bytes(b'original scene')
        self.record = {'path': 'scene.unity', 'size': self.original.stat().st_size,
                       'sha256': verify.digest(self.original)}
        self.manifest = {'source': str(self.source), 'files': [self.record], 'evidence': []}

    def tearDown(self):
        self.temp.cleanup()

    def manifest_file(self, reference):
        file = self.clone / 'Creator/Packages/manifest.json'
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_text(json.dumps({'dependencies': {'test': reference}}))
        return file

    def test_original_is_unchanged_but_clone_may_change(self):
        (self.clone / 'scene.unity').write_bytes(b'intentional upgraded clone')
        self.assertEqual([], verify.verify_original(self.manifest, self.clone))

    def test_same_length_original_change_is_detected(self):
        self.original.write_bytes(b'changed! scene')
        self.assertIn('changed file', verify.verify_original(self.manifest, self.clone)[0])

    def test_missing_original_is_detected(self):
        self.original.unlink()
        self.assertIn('missing file', verify.verify_original(self.manifest, self.clone)[0])

    def test_preserved_evidence_is_checked_separately(self):
        self.manifest['evidence'] = [self.record]
        error = verify.verify_original(self.manifest, self.clone)
        self.assertTrue(any('preserved evidence' in value for value in error))

    def test_relative_package_stays_inside_clone(self):
        (self.clone / 'SDK/Packages/test').mkdir(parents=True)
        self.manifest_file('file:../../SDK/Packages/test')
        self.assertEqual(([], 1), verify.audit_local_references(self.clone))

    def test_relative_original_escape_is_rejected(self):
        self.manifest_file('file:../../../original')
        errors, count = verify.audit_local_references(self.clone)
        self.assertEqual(1, count)
        self.assertIn('escapes clone', errors[0])

    def test_absolute_original_escape_is_rejected(self):
        self.manifest_file('file:' + self.source.as_posix())
        self.assertIn('escapes clone', verify.audit_local_references(self.clone)[0][0])

    def test_missing_internal_package_is_rejected(self):
        self.manifest_file('file:missing')
        self.assertIn('is missing', verify.audit_local_references(self.clone)[0][0])


if __name__ == '__main__':
    unittest.main()
