#!/bin/bash

# publish-packages.bash - Script to publish NuGet packages to NuGet.org
# Usage: ./scripts/publish-packages.bash [API_KEY]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
NUPKGS_DIR="$PROJECT_ROOT/nupkgs"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if API key is provided
API_KEY="${1:-$NUGET_API_KEY}"

if [ -z "$API_KEY" ]; then
    print_error "NuGet API key not provided!"
    echo "Usage: $0 [API_KEY]"
    echo "Or set the NUGET_API_KEY environment variable"
    echo ""
    echo "To get an API key:"
    echo "1. Go to https://www.nuget.org/"
    echo "2. Sign in to your account"
    echo "3. Go to Account Settings > API Keys"
    echo "4. Create a new API key with 'Push new packages and package versions' scope"
    exit 1
fi

# Change to project root
cd "$PROJECT_ROOT"

print_status "Publishing NuGet packages to NuGet.org..."

# Check if packages exist
if [ ! -d "$NUPKGS_DIR" ]; then
    print_error "Package directory not found: $NUPKGS_DIR"
    print_warning "Run './scripts/build-packages.bash' first to build packages"
    exit 1
fi

# Find all .nupkg files (excluding .snupkg symbol packages)
PACKAGES=($(find "$NUPKGS_DIR" -name "RtFlow.Pipelines.*.nupkg" ! -name "*.snupkg" | sort))

if [ ${#PACKAGES[@]} -eq 0 ]; then
    print_error "No packages found in $NUPKGS_DIR"
    print_warning "Run './scripts/build-packages.bash' first to build packages"
    exit 1
fi

print_status "Found ${#PACKAGES[@]} packages to publish:"
for package in "${PACKAGES[@]}"; do
    echo "  - $(basename "$package")"
done

# Ask for confirmation
echo ""
read -p "Do you want to publish these packages to NuGet.org? [y/N]: " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    print_warning "Publication cancelled by user"
    exit 0
fi

echo ""
print_status "Starting publication process..."

# Publish each package
for package in "${PACKAGES[@]}"; do
    package_name=$(basename "$package")
    print_status "Publishing $package_name..."
    
    if dotnet nuget push "$package" \
        --api-key "$API_KEY" \
        --source https://api.nuget.org/v3/index.json \
        --skip-duplicate; then
        print_success "Successfully published $package_name"
    else
        print_error "Failed to publish $package_name"
        exit 1
    fi
    
    echo ""
done

print_success "All packages published successfully!"
echo ""
print_status "Published packages:"
for package in "${PACKAGES[@]}"; do
    package_name=$(basename "$package" .nupkg)
    echo "  - https://www.nuget.org/packages/$package_name"
done

echo ""
print_status "Note: It may take a few minutes for packages to appear on NuGet.org"
print_status "Symbol packages (.snupkg) are automatically included with main packages"
