#!/usr/bin/env bash
# Vendors the Firebase JS SDK browser builds into wwwroot/lib/firebase so the
# app works offline (no CDN). Usage: tools/update-firebase.sh [version]
set -euo pipefail

VERSION="${1:-13.0.0}"
DEST="$(cd "$(dirname "$0")/.." && pwd)/src/Bookshelf/wwwroot/lib/firebase"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

(cd "$WORK" && npm pack "firebase@${VERSION}" --silent >/dev/null && tar -xzf firebase-*.tgz)

rm -rf "$DEST" && mkdir -p "$DEST"
for f in firebase-app.js firebase-auth.js firebase-firestore-lite.js; do
    # Point cross-module imports at the local copy and drop source map references.
    sed -e "s#https://www.gstatic.com/firebasejs/${VERSION}/firebase-app.js#./firebase-app.js#g" \
        -e '/sourceMappingURL=/d' \
        "$WORK/package/$f" > "$DEST/$f"
done
cp "$WORK/package/LICENSE" "$DEST/LICENSE.txt" 2>/dev/null || echo "Apache-2.0 (see https://github.com/firebase/firebase-js-sdk)" > "$DEST/LICENSE.txt"
echo "$VERSION" > "$DEST/VERSION"

if grep -q "gstatic.com" "$DEST"/*.js; then
    echo "Unexpected remote import left in vendored files" >&2
    exit 1
fi
echo "Vendored Firebase ${VERSION} into ${DEST}"
