#!/usr/bin/env bash
set -euo pipefail

PROJECT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$PROJECT/Builds/web"
REMOTE="${REMOTE:-origin}"
BRANCH="${BRANCH:-gh-pages}"
REPO_SLUG="$(git -C "$PROJECT" remote get-url "$REMOTE" | sed -E 's#.*[:/]([^/]+/[^/]+)$#\1#; s#\.git$##')"
OWNER="${REPO_SLUG%/*}"
NAME="${REPO_SLUG#*/}"
URL="https://${OWNER}.github.io/${NAME}/"

cd "$PROJECT"
echo "==> clean build -> $OUT"
rm -rf "$OUT"

if unity status 2>/dev/null | grep -q "$PROJECT"; then
  echo "==> editor open, building via pipeline"
  unity command build --target WebGL --outputPath Builds/web --confirm true >/dev/null
  for _ in $(seq 1 200); do
    sleep 5
    status="$(unity command build_status 2>/dev/null | tail -1 | grep -o '"status":"[a-z_]*"' | head -1 || true)"
    case "$status" in *completed*|*failed*|*idle*) break ;; esac
  done
  report="$(unity command build_status 2>/dev/null | tail -1)"
  echo "$report" | grep -q '"result":"Succeeded"' || { echo "BUILD FAILED"; echo "$report" | grep -o '"errors":\[[^]]*\]' | head -c 2000; exit 1; }
else
  echo "==> no editor, building headless"
  unity build . --target WebGL --execute-method Marchio.Editor.BuildScript.BuildWeb --output-path Builds/web --allow-dirty-build --no-tail --log-file build-web.log
fi

[ -f "$OUT/index.html" ] || { echo "no index.html in $OUT"; exit 1; }

echo "==> deploy to $REMOTE/$BRANCH"
SHA="$(git rev-parse --short HEAD)"
STAGE="$(mktemp -d)"
rsync -a --exclude '*_BurstDebugInformation_DoNotShip' "$OUT/" "$STAGE/"
touch "$STAGE/.nojekyll"
git -C "$STAGE" init -q -b "$BRANCH"
git -C "$STAGE" add -A
git -C "$STAGE" -c user.name="$(git config user.name)" -c user.email="$(git config user.email)" commit -q -m "web build from $SHA ($(date -u +%Y-%m-%dT%H:%MZ))"
git -C "$STAGE" push -q -f "$(git remote get-url "$REMOTE")" "$BRANCH:$BRANCH"
rm -rf "$STAGE"

if ! gh api "repos/$REPO_SLUG/pages" >/dev/null 2>&1; then
  echo "==> enabling GitHub Pages on $BRANCH"
  gh api -X POST "repos/$REPO_SLUG/pages" -f "source[branch]=$BRANCH" -f "source[path]=/" >/dev/null 2>&1 || true
fi

LOADER="$(grep -o 'Build/[^"]*loader\.js' "$OUT/index.html" | head -1)"
echo "==> waiting for Pages to serve $LOADER (CDN cache is 10 min)"
for _ in $(seq 1 130); do
  sleep 5
  if curl -fsS -H 'Cache-Control: no-cache' "$URL" 2>/dev/null | grep -q "$LOADER"; then echo "LIVE: ${URL}?v=${SHA}"; exit 0; fi
done
echo "Pushed; Pages still propagating. URL: $URL"
