#!/usr/bin/env bash
set -euo pipefail
test "$(id -u)" = 0
lineage="${RENEWED_LINEAGE:-/etc/letsencrypt/live/projectdawn-ip}"
test "$lineage" = /etc/letsencrypt/live/projectdawn-ip
install -d -m 0750 -o root -g dawn-gateway /etc/projectdawn/tls
umask 077
temp=$(mktemp /etc/projectdawn/tls/server.XXXXXX)
trap 'rm -f "$temp"' EXIT
openssl pkcs12 -export -out "$temp" -inkey "$lineage/privkey.pem" -in "$lineage/cert.pem" -certfile "$lineage/chain.pem" -passout pass:
chown root:dawn-gateway "$temp"
chmod 0640 "$temp"
mv -f "$temp" /etc/projectdawn/tls/server.pfx
systemctl try-restart dawn-member-gateway.service
