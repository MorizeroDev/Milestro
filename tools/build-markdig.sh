#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
markdig_src_dir="$root_dir/ext/markdig/src"
output_dir="$root_dir/apps/unity-plugins/Milestro.RichText.Markdown/Plugins"

# ext/markdig/src/global.json requires .NET SDK 10.0.100 with latestMinor roll-forward.
cd "$markdig_src_dir"
dotnet build Markdig/Markdig.csproj -c Release -f netstandard2.1

mkdir -p "$output_dir"
cp "$markdig_src_dir/Markdig/bin/Release/netstandard2.1/Markdig.dll" "$output_dir/Markdig.dll"
