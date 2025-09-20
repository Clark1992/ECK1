#!/usr/bin/env bash
set -euo pipefail

# Exit if no argument provided
if [ $# -lt 1 ]; then
  echo "Usage: $0 path/to/Project.csproj" >&2
  exit 1
fi

# Take the first argument as the csproj path
CSproj="$1"

# Extract PackageId and base version (Major.Minor) from csproj
PACKAGE_ID=$(xmllint --xpath "string(//Project/PropertyGroup/PackageId)" "$CSproj")
BASE_VERSION=$(xmllint --xpath "string(//Project/PropertyGroup/Version)" "$CSproj" | cut -d'.' -f1,2)

echo "PackageId: $PACKAGE_ID"
echo "BaseVersion: $BASE_VERSION"

# Make sure GitHub NuGet source is configured
dotnet nuget remove source github || true
dotnet nuget add source \
  --username "$GITHUB_ACTOR" \
  --password "$GITHUB_TOKEN" \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/${GITHUB_REPOSITORY_OWNER}/index.json"

# Discover the PackageBaseAddress endpoint from the NuGet service index
URL="https://nuget.pkg.github.com/${GITHUB_REPOSITORY_OWNER}/index.json"
SOURCE=$(curl -s -u "$GITHUB_ACTOR:$GITHUB_TOKEN" "$URL" \
  | jq -r '.resources[] | select(.["@type"]=="PackageBaseAddress/3.0.0") | .["@id"]')

# Fetch versions of the package (if it already exists)
STATUS=$(curl -s -o versions.json -w "%{http_code}" -u "$GITHUB_ACTOR:$GITHUB_TOKEN" "$SOURCE$PACKAGE_ID/index.json")

echo "$SOURCE$PACKAGE_ID/index.json"
cat versions.json

if [[ "$STATUS" == "404" ]]; then
  # Package does not exist yet → create empty version list
  echo '{"versions":[]}' > versions.json
elif [[ "$STATUS" != "200" ]]; then
  # Any other status code is an error
  echo "Error: unexpected HTTP status $STATUS when fetching versions" >&2
  exit 1
fi

# Determine the new patch version
LAST=$(jq -r '.versions[]' versions.json | sort -V | tail -n 1)

PATCH=0
if [[ -n "$LAST" && "$LAST" != "null" ]]; then
  LAST_BASE=$(echo "$LAST" | cut -d'.' -f1,2)
  LAST_PATCH=$(echo "$LAST" | cut -d'.' -f3)

  if [[ "$LAST_BASE" == "$BASE_VERSION" ]]; then
    # Same base → bump patch
    PATCH=$((LAST_PATCH+1))
  elif [[ "$(printf '%s\n' "$LAST_BASE" "$BASE_VERSION" | sort -V | tail -n1)" == "$BASE_VERSION" ]]; then
    # Local base is greater → start with patch=0
    PATCH=0
  else
    # Local base is smaller than remote → fail
    echo "Error: base version in csproj ($BASE_VERSION) is lower than published ($LAST_BASE)" >&2
    exit 1
  fi
fi

NEW_VERSION="$BASE_VERSION.$PATCH"
echo "NewVersion=$NEW_VERSION"

# Patch the csproj file: update Version and add RepositoryUrl
xmlstarlet ed -L \
  -u "//Project/PropertyGroup/Version" -v "$NEW_VERSION" \
  -s "//Project/PropertyGroup" -t elem -n RepositoryUrl -v "https://github.com/$GITHUB_REPOSITORY" \
  "$CSproj"
