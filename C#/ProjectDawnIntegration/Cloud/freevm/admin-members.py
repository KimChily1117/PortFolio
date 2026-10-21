#!/usr/bin/env python3
"""Root-only, local MySQL member administration. Never accepts a password argument."""
import argparse
import getpass
import hashlib
import os
import re
import secrets
import subprocess
import sys
import unicodedata


def login_id(value):
    if not re.fullmatch(r"[a-zA-Z0-9_-]{3,32}", value, flags=re.ASCII):
        raise argparse.ArgumentTypeError("ID must be 3-32 ASCII letters, digits, _ or -.")
    return value.lower()


def valid_password(value):
    # Match .NET string.Length (UTF-16 units) and char.IsControl.
    try:
        length = len(value.encode("utf-16-le")) // 2
        value.encode("utf-8")
    except UnicodeError:
        return False
    return 8 <= length <= 128 and bool(value.strip()) and not any(
        unicodedata.category(c) == "Cc" for c in value)


def sql(query, headers=False):
    args = ["mysql", "--protocol=socket", "--user=root", "--database=projectdawn",
            "--default-character-set=utf8mb4", "--batch"]
    if not headers:
        args.append("--skip-column-names")
    result = subprocess.run(args, input=query, text=True, encoding="utf-8",
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15)
    if result.returncode:
        # Do not echo errors: they can contain SQL with salt/hash values.
        raise RuntimeError("Local MySQL command failed. Check socket access/schema; SQL details suppressed.")
    return result.stdout.rstrip("\n")


def account_pk(account):
    # account is validated even when functions are called outside the CLI.
    account = login_id(account)
    row = sql("SELECT AccountDbId FROM accountcredential WHERE LoginId='" + account + "';")
    if not row.isascii() or not row.isdecimal():
        raise RuntimeError("Account not found; nothing changed.")
    return int(row)


def reset_password(account):
    pk = account_pk(account)
    if not sys.stdin.isatty():
        raise RuntimeError("Password reset requires an interactive SSH terminal (ssh -t).")
    first = getpass.getpass("New game password (8-128 characters): ")
    second = getpass.getpass("Repeat new password: ")
    if first != second or not valid_password(first):
        raise RuntimeError("Passwords differ or violate policy; nothing changed.")
    salt = secrets.token_bytes(32)
    hashed = hashlib.pbkdf2_hmac("sha256", first.encode("utf-8"), salt, 600000, 32)
    del first, second
    changed = sql("UPDATE accountcredential SET PasswordSalt=UNHEX('" + salt.hex() +
                  "'),PasswordHash=UNHEX('" + hashed.hex() +
                  "'),Iterations=600000,HashAlgorithm='PBKDF2-SHA256' WHERE AccountDbId=" +
                  str(pk) + "; SELECT ROW_COUNT();")
    if changed != "1":
        raise RuntimeError("Password update did not affect one account; verify account existence.")
    print("Password reset. Characters/equipment and Disabled status unchanged. Existing sessions are not disconnected.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="action", required=True)
    listing = commands.add_parser("list", help="List IDs/status/character counts; no credentials")
    listing.add_argument("account", nargs="?", type=login_id)
    for name in ("characters", "reset-password", "disable", "enable"):
        command = commands.add_parser(name)
        command.add_argument("account", type=login_id)
    args = parser.parse_args()
    if not hasattr(os, "geteuid") or os.geteuid() != 0:
        parser.error("Run on the game server with sudo; MySQL stays private.")
    if args.action == "list":
        where = " WHERE c.LoginId='" + args.account + "'" if args.account else ""
        print(sql("SELECT c.LoginId,c.AccountDbId,c.Disabled,c.CreatedUtc,c.LastLoginUtc,"
                  "(SELECT COUNT(*) FROM player p WHERE p.AccountDbId=c.AccountDbId) AS CharacterCount "
                  "FROM accountcredential c" + where + " ORDER BY c.LoginId;", headers=True))
    elif args.action == "characters":
        pk = account_pk(args.account)
        print(sql("SELECT PlayerDbId,PlayerName FROM player WHERE AccountDbId=" + str(pk) +
                  " ORDER BY PlayerDbId;", headers=True))
    elif args.action == "reset-password":
        reset_password(args.account)
    else:
        pk = account_pk(args.account)
        disabled = 1 if args.action == "disable" else 0
        sql("UPDATE accountcredential SET Disabled=" + str(disabled) + " WHERE AccountDbId=" + str(pk) + ";")
        print("Account " + args.action + "d. Applies to future logins; existing sessions are not disconnected.")


if __name__ == "__main__":
    try:
        main()
    except (RuntimeError, OSError, subprocess.TimeoutExpired, EOFError, KeyboardInterrupt) as error:
        print(str(error) if isinstance(error, RuntimeError) else "Administration failed or cancelled.", file=sys.stderr)
        sys.exit(1)
