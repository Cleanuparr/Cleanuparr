#!/bin/sh
set -eu

htpasswd -bc /usr/local/apache2/conf/htpasswd proxyuser proxypass

# htdigest has no non-interactive flag, so we compute HA1 by hand.
ha1=$(printf 'proxyuser:authproxy:proxypass' | md5sum | cut -d' ' -f1)
printf 'proxyuser:authproxy:%s\n' "$ha1" > /usr/local/apache2/conf/htdigest

exec httpd-foreground
