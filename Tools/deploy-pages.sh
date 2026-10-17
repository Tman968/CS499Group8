#!/usr/bin/env bash
# Publishes the WebGL build to the gh-pages branch, which GitHub Pages serves at
# https://tman968.github.io/CS499Group8/
#
# 1. In Unity: JumpCraft > WebGL > Build
# 2. From the repo (Git Bash on Windows, or a macOS/Linux terminal):  ./Tools/deploy-pages.sh
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
build_dir="$repo_root/JumpCraftGame/Builds/WebGL"
remote_url="$(git -C "$repo_root" remote get-url origin)"
source_rev="$(git -C "$repo_root" describe --always --dirty)"

if [[ ! -f "$build_dir/index.html" ]]; then
  echo "No WebGL build found at $build_dir" >&2
  echo "Build it in Unity first: JumpCraft > WebGL > Build" >&2
  exit 1
fi

# GitHub rejects files over 100 MB.
if find "$build_dir" -type f -size +95M | grep -q .; then
  echo "Build contains files over 95 MB, which GitHub won't accept:" >&2
  find "$build_dir" -type f -size +95M >&2
  exit 1
fi

staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT

cp -R "$build_dir"/. "$staging"/
touch "$staging/.nojekyll"  # serve files as-is, without GitHub's Jekyll processing

git -C "$staging" init -q -b gh-pages
git -C "$staging" config core.autocrlf false  # never rewrite line endings in build files
git -C "$staging" add -A
git -C "$staging" commit -q -m "Deploy WebGL build from $source_rev"

# Force-push a single commit so old builds don't pile up in everyone's clone.
git -C "$staging" push -f "$remote_url" gh-pages

echo "Deployed. Live in a minute or two at https://tman968.github.io/CS499Group8/"
