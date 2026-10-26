#!/bin/sh
# Replace an installed Quiet Home with a newer local build. Input: the install's snapshot name.
# Stage .local/package.tar.gz as quiet-tizen/package.tar.gz first. Routing is left as it is.
set -eu
test "$(id -u)" = 0
name=${1:?Snapshot name required}
case "$name" in ''|*[!A-Za-z0-9_-]*) exit 1;; esac
D=/opt/usr/share/quiet-tizen-backups/$name
A=/opt/usr/apps/com.samsung.tv.bixby-promotion
S=/home/owner/share/tmp/sdk_tools/quiet-tizen
test -s "$D/bixby-promotion-original/bin/BixbyPromotion.dll"
test -s "$S/package.tar.gz"
test -d "$A/res/quiet-home"
# Only update our own install: the live assembly must be the one this snapshot recorded.
sha256sum -c "$D/installed-home.sha256"
rm -rf "$S/unpacked" "$A/bin/BixbyPromotion.dll.new" "$A/res/quiet-home.new" "$A/res/quiet-home.old"
mkdir "$S/unpacked"
tar -xzf "$S/package.tar.gz" -C "$S/unpacked"
test -s "$S/unpacked/BixbyPromotion.dll"
test -s "$S/unpacked/quiet-home/apps.json"
# As the Home app, Quiet Home is restarted by the TV within a second of being
# killed. Stage and label the new files beside the live ones and swap them in by
# rename before the kill, so the restarted instance never finds a half-copied
# folder; an empty fonts folder there left it in the fallback font.
cp "$S/unpacked/BixbyPromotion.dll" "$A/bin/BixbyPromotion.dll.new"
chown 201:0 "$A/bin/BixbyPromotion.dll.new"
chmod 644 "$A/bin/BixbyPromotion.dll.new"
chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO "$A/bin/BixbyPromotion.dll.new"
cp -R "$S/unpacked/quiet-home" "$A/res/quiet-home.new"
find "$A/res/quiet-home.new" -type d -exec chmod 755 {} \;
find "$A/res/quiet-home.new" -type f -exec chmod 644 {} \;
find "$A/res/quiet-home.new" -exec chown 201:0 {} \;
find "$A/res/quiet-home.new" -exec chsmack -a User::Pkg::com.samsung.tv.bixby-promotion::RO {} \;
mv -f "$A/bin/BixbyPromotion.dll.new" "$A/bin/BixbyPromotion.dll"
sha256sum "$A/bin/BixbyPromotion.dll" > "$D/installed-home.sha256"
mv "$A/res/quiet-home" "$A/res/quiet-home.old"
mv "$A/res/quiet-home.new" "$A/res/quiet-home"
app_launcher -u 5001 -k com.samsung.tv.bixby-promotion || true
rm -rf "$A/res/quiet-home.old" "$S/unpacked"
app_launcher -u 5001 -s com.samsung.tv.bixby-promotion
echo "Updated and launch requested. restore-launcher.sh $name still restores the original from $D."
