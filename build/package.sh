#!/usr/bin/env bash
# Builds both plugin zips and writes manifest.json with their MD5 checksums.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
export DOTNET_ROOT="${HOME}/.dotnet"
cd "${root}"

dotnet test "${root}/Jellyfin.Plugin.CensoredLyrics.slnx" -c Release --nologo

dist="${root}/dist"
rm -rf "${dist}"
mkdir -p "${dist}"
stamp="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"

package_one() {
  local tfm="$1"
  local version="$2"
  local abi="$3"
  local changelog="$4"
  local dll="${root}/Jellyfin.Plugin.CensoredLyrics/bin/Release/${tfm}/Jellyfin.Plugin.CensoredLyrics.dll"
  local stage
  stage="$(mktemp -d)"
  cp "${dll}" "${stage}/Jellyfin.Plugin.CensoredLyrics.dll"
  python3 - "${stage}/meta.json" "${version}" "${abi}" "${stamp}" "${changelog}" <<'PY'
import json, sys
path, version, abi, stamp, changelog = sys.argv[1:]
meta = {
    "category": "Music",
    "changelog": changelog,
    "description": "Downloads timed lyrics from LRCLIB and censors words from an editable list.",
    "guid": "d045e257-f274-46e5-a2a7-b88f72c725e9",
    "name": "Censored LRCLIB",
    "overview": "Timed LRCLIB lyrics with swear words censored.",
    "owner": "TidBits16",
    "targetAbi": abi,
    "timestamp": stamp,
    "version": version,
}
with open(path, "w") as handle:
    json.dump(meta, handle, indent=4)
    handle.write("\n")
PY
  local zip="${dist}/censored-lyrics_${version}.zip"
  (cd "${stage}" && zip -X -q "${zip}" meta.json Jellyfin.Plugin.CensoredLyrics.dll)
  rm -rf "${stage}"
}

package_one net10.0 1.0.23.0 12.0.0.0 "Jellyfin 12. Dropdown help uses the dashboard field description."
package_one net9.0 1.0.22.0 10.11.0.0 "Jellyfin 10.11. Dropdown help uses the dashboard field description."

python3 - "${dist}" "${stamp}" <<'PY'
import hashlib, json, pathlib, sys
dist, stamp = sys.argv[1:]
repo = "https://github.com/TidBits16/jellyfin-plugin-censored-lyrics/releases/download/v1.0.23.0"
entries = [
    ("1.0.23.0", "12.0.0.0", "Jellyfin 12. Dropdown help uses the dashboard field description."),
    ("1.0.22.0", "10.11.0.0", "Jellyfin 10.11. Dropdown help uses the dashboard field description."),
]
manifest_path = pathlib.Path(dist).parent / "manifest.json"
previous = json.loads(manifest_path.read_text())[0]["versions"] if manifest_path.exists() else []
versions = {item["version"]: item for item in previous}
for version, abi, changelog in entries:
    zip_path = pathlib.Path(dist) / f"censored-lyrics_{version}.zip"
    digest = hashlib.md5(zip_path.read_bytes()).hexdigest()
    versions[version] = {
        "version": version,
        "changelog": changelog,
        "targetAbi": abi,
        "sourceUrl": f"{repo}/censored-lyrics_{version}.zip",
        "checksum": digest,
        "timestamp": stamp,
    }
    print(f"{version} {digest} {zip_path}")

def version_key(item):
    return tuple(int(part) for part in item["version"].split("."))

versions = sorted(versions.values(), key=version_key, reverse=True)

manifest = [{
    "guid": "d045e257-f274-46e5-a2a7-b88f72c725e9",
    "name": "Censored LRCLIB",
    "description": "Downloads timed lyrics from LRCLIB and censors words from an editable list.\n",
    "overview": "Timed LRCLIB lyrics with a word list you can edit.",
    "owner": "TidBits16",
    "category": "Music",
    "versions": versions,
}]
path = pathlib.Path(dist).parent / "manifest.json"
path.write_text(json.dumps(manifest, indent=4) + "\n")
print(path)
PY
