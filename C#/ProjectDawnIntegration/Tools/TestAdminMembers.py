"""Validate admin boundaries without modifying live member accounts."""
import contextlib
import argparse
import hashlib
import importlib.util
import io
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location('admin', Path(__file__).resolve().parents[1] / 'Cloud/freevm/admin-members.py')
admin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(admin)


class AdminTests(unittest.TestCase):
    def test_ids_and_injection(self):
        self.assertEqual(admin.login_id('Friend_01'), 'friend_01')
        for value in ('ab', "abc' OR 1=1 --", 'abc\n', 'a' * 33, '가나다'):
            with self.assertRaises(argparse.ArgumentTypeError):
                admin.login_id(value)

    def test_password_boundaries(self):
        for value in ('a' * 7, 'a' * 129, ' ' * 8, 'valid123\n', 'abc12345\x7f'):
            self.assertFalse(admin.valid_password(value))
        for value in ('a' * 8, 'a' * 128, '가나다라마바사아', '😀' * 4):
            self.assertTrue(admin.valid_password(value))
        self.assertFalse(admin.valid_password('😀' * 65))

    def test_missing_account_never_prompts(self):
        with patch.object(admin, 'sql', return_value=''), patch.object(admin.getpass, 'getpass') as prompt:
            with self.assertRaises(RuntimeError):
                admin.reset_password('friend')
            prompt.assert_not_called()

    def test_noninteractive_never_prompts(self):
        with patch.object(admin, 'account_pk', return_value=23), patch.object(admin.sys.stdin, 'isatty', return_value=False), patch.object(admin.getpass, 'getpass') as prompt:
            with self.assertRaises(RuntimeError):
                admin.reset_password('friend')
            prompt.assert_not_called()

    def test_invalid_confirmation_never_writes(self):
        for pair in (['valid123', 'different'], ['short', 'short']):
            with patch.object(admin, 'account_pk', return_value=23), patch.object(admin.sys.stdin, 'isatty', return_value=True), patch.object(admin.getpass, 'getpass', side_effect=pair), patch.object(admin, 'sql') as query:
                with self.assertRaises(RuntimeError):
                    admin.reset_password('friend')
                query.assert_not_called()

    def test_reset_preserves_identity_and_never_outputs_secret(self):
        # Test-only fixed data. No real member credentials.
        password = 'test1234'
        salt = bytes(range(32))
        output = io.StringIO()
        with patch.object(admin, 'account_pk', return_value=23), patch.object(admin.sys.stdin, 'isatty', return_value=True), patch.object(admin.getpass, 'getpass', side_effect=[password, password]), patch.object(admin.secrets, 'token_bytes', return_value=salt), patch.object(admin, 'sql', return_value='1') as query, contextlib.redirect_stdout(output):
            admin.reset_password('friend')
        statement = query.call_args.args[0]
        self.assertIn('WHERE AccountDbId=23;', statement)
        self.assertNotIn('Disabled=', statement)
        self.assertNotIn('DELETE', statement)
        self.assertNotIn(password, statement + output.getvalue())
        digest = hashlib.pbkdf2_hmac('sha256', password.encode(), salt, 600000, 32).hex()
        self.assertIn(digest, statement)
        self.assertNotIn(digest, output.getvalue())

    def test_mysql_failure_does_not_leak_sql(self):
        with patch.object(admin.subprocess, 'run', return_value=subprocess.CompletedProcess([], 1, '', 'secret SQL')):
            with self.assertRaises(RuntimeError) as error:
                admin.sql('sensitive query')
            self.assertNotIn('secret SQL', str(error.exception))

    def test_password_sql_uses_stdin_not_process_args(self):
        with patch.object(admin.subprocess, 'run', return_value=subprocess.CompletedProcess([], 0, '1\n', '')) as run:
            self.assertEqual(admin.sql('sensitive query'), '1')
            self.assertNotIn('sensitive query', repr(run.call_args.args))
            self.assertEqual(run.call_args.kwargs['input'], 'sensitive query')

    def test_requires_root(self):
        with patch.object(admin.os, 'geteuid', return_value=1000, create=True), patch.object(admin.sys, 'argv', ['admin', 'list']), patch.object(admin, 'sql') as query, contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                admin.main()
            query.assert_not_called()

    def test_list_excludes_hash_and_salt(self):
        with patch.object(admin.os, 'geteuid', return_value=0, create=True), patch.object(admin.sys, 'argv', ['admin', 'list']), patch.object(admin, 'sql', return_value='') as query, contextlib.redirect_stdout(io.StringIO()):
            admin.main()
        statement = query.call_args.args[0]
        self.assertNotIn('Password', statement)
        self.assertNotIn('SELECT *', statement)

    def test_lock_and_unlock_target_only_one_id(self):
        for action, disabled in [('disable', 1), ('enable', 0)]:
            with patch.object(admin.os, 'geteuid', return_value=0, create=True), patch.object(admin.sys, 'argv', ['admin', action, 'friend']), patch.object(admin, 'account_pk', return_value=23), patch.object(admin, 'sql', return_value='') as query, contextlib.redirect_stdout(io.StringIO()):
                admin.main()
            self.assertEqual(query.call_args.args[0], f'UPDATE accountcredential SET Disabled={disabled} WHERE AccountDbId=23;')


if __name__ == '__main__':
    unittest.main(verbosity=2)
