#!/usr/bin/env bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Define the .NET target version in a variable for easy updates later
DOTNET_TARGET="net8.0"

# Get solution name from the .sln file (if it exists and not recursive)
SOLUTION_NAME=$(find "$SOLUTION_DIR" -maxdepth 1 -type f -name '*.sln' -exec basename {} \;)

echo "Solution directory: $SOLUTION_DIR"
echo "Solution name: $SOLUTION_NAME"
echo "Target .NET version: $DOTNET_TARGET"

# ------------------------------------------------------------------------------
# 1) Remove old 'packages' and 'nupkgs' directories
# ------------------------------------------------------------------------------
echo "Removing old 'packages' and 'nupkgs' directories..."
rm -rf "${SOLUTION_DIR}/packages"
rm -rf "${SOLUTION_DIR}/nupkgs"

# ------------------------------------------------------------------------------
# 2) Update each .csproj to DOTNET_TARGET
# ------------------------------------------------------------------------------
update_project_framework() {
  local csprojFile="$1"
  echo "Updating target framework for project: $csprojFile to ${DOTNET_TARGET}..."

  # Replace <TargetFramework> with DOTNET_TARGET
  sed -i "s|<TargetFramework>.*</TargetFramework>|<TargetFramework>${DOTNET_TARGET}</TargetFramework>|" "$csprojFile"
  # If multi-target, replace <TargetFrameworks> as well
  sed -i "s|<TargetFrameworks>.*</TargetFrameworks>|<TargetFrameworks>${DOTNET_TARGET}</TargetFrameworks>|" "$csprojFile"
}

echo "Searching for .csproj files in '$SOLUTION_DIR'..."
find "$SOLUTION_DIR" -type f -name '*.csproj' | while read -r csprojFile; do
  update_project_framework "$csprojFile"
done

echo "All .csproj files updated to ${DOTNET_TARGET}."

# ------------------------------------------------------------------------------
# 3) Use 'dotnet-outdated' to update packages to the latest compatible versions
# ------------------------------------------------------------------------------
echo "Installing dotnet-outdated-tool (if not installed)..."
dotnet tool install --global dotnet-outdated-tool || echo "dotnet-outdated-tool already installed."

echo "Running dotnet-outdated to automatically upgrade packages..."
if [ -f "${SOLUTION_DIR}/$SOLUTION_NAME" ]; then
  echo "Found .sln file at ${SOLUTION_DIR}/$SOLUTION_NAME; scanning solution."
  dotnet outdated "${SOLUTION_DIR}/$SOLUTION_NAME" --upgrade
else
  echo "No .sln file found at ${SOLUTION_DIR}/$SOLUTION_NAME; scanning individual projects."
  find "$SOLUTION_DIR" -type f -name '*.csproj' -exec dotnet outdated {} --upgrade \;
fi

echo "All package references updated to latest compatible versions."

# ------------------------------------------------------------------------------
# 4) Restore packages into './packages'
# ------------------------------------------------------------------------------
echo "Running 'dotnet restore --packages ${SOLUTION_DIR}/packages'..."
dotnet restore --packages "${SOLUTION_DIR}/packages"

# ------------------------------------------------------------------------------
# 5) Copy all .nupkg files from ./packages to ./nupkgs
# ------------------------------------------------------------------------------
echo "Copying .nupkg files into ./nupkgs..."
mkdir -p "${SOLUTION_DIR}/nupkgs"
find "${SOLUTION_DIR}/packages" -type f -name '*.nupkg' -exec cp {} "${SOLUTION_DIR}/nupkgs" \;

# ------------------------------------------------------------------------------
# 6) Delete the packages folder (since we've extracted what we need)
# ------------------------------------------------------------------------------
echo "Deleting the packages folder to clean up..."
rm -rf "${SOLUTION_DIR}/packages"

#------------------------------------------------------------------------------
# 7) Clean up the dotnet bin and obj folders
# ------------------------------------------------------------------------------
echo "Removing old 'bin' and 'obj' directories from solution directory '$SOLUTION_DIR'..."

# wait for 2 seconds before removing bin and obj folders
sleep 2

find "$SOLUTION_DIR" -type d -name bin | xargs rm -rf
find "$SOLUTION_DIR" -type d -name obj | xargs rm -rf

#--------------------------------------------------------------------------------
# Done
#--------------------------------------------------------------------------------
echo "Done! Projects target ${DOTNET_TARGET}, packages updated via dotnet-outdated, restored, and cleaned up."
