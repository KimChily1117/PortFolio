#!/usr/bin/env bash
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
test "$(id -u)" = 0
test "$(. /etc/os-release; echo "$VERSION_CODENAME")" = noble
apt-get update -qq
apt-get install -y -qq ca-certificates curl gnupg unzip python3-venv openssl libicu74 libssl3t64 zlib1g libgcc-s1 libstdc++6
install -d -m 0755 /etc/mysql/mysql.conf.d
cat >/etc/mysql/mysql.conf.d/projectdawn.cnf <<'EOF'
[mysqld]
bind-address=127.0.0.1
mysqlx=0
require_secure_transport=ON
innodb_buffer_pool_size=128M
max_connections=40
performance_schema=OFF
default-time-zone=+00:00
character-set-server=utf8mb4
collation-server=utf8mb4_0900_ai_ci
EOF
curl --fail --silent --show-error --location https://dev.mysql.com/get/mysql-apt-config_0.8.40-1_all.deb -o /var/tmp/mysql-apt-config.deb
printf '%s\n' 'mysql-apt-config mysql-apt-config/select-server select mysql-8.4-lts' 'mysql-apt-config mysql-apt-config/select-product select Ok' | debconf-set-selections
dpkg -i /var/tmp/mysql-apt-config.deb
apt-get update -qq
apt-get install -y -qq mysql-community-server
mysql --version
python3 -m venv /opt/certbot
/opt/certbot/bin/pip install --disable-pip-version-check certbot
/opt/certbot/bin/certbot --version
for service in dawn-server dawn-gateway; do
  id "$service" >/dev/null 2>&1 || useradd --system --no-create-home --shell /usr/sbin/nologin "$service"
done
install -d -m 0755 /opt/projectdawn /etc/projectdawn
echo '[DAWN_BASE_READY]'
