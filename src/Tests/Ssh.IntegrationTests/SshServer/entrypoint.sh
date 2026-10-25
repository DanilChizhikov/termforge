#!/bin/sh
set -eu

ssh-keygen -q -t ed25519 -N '' -f /etc/ssh/ssh_host_ed25519_key

mkdir -p /keys /home/tester/.ssh
ssh-keygen -q -t ed25519 -N '' -C tester -f /keys/tester_ed25519
cp /keys/tester_ed25519.pub /home/tester/.ssh/authorized_keys

chown -R tester:tester /home/tester/.ssh
chmod 700 /home/tester/.ssh
chmod 600 /home/tester/.ssh/authorized_keys

exec /usr/sbin/sshd -D -e
