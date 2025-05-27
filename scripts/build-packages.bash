#!/bin/bash

# RtFlow.Pipelines Build and Package Script

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}🚀 RtFlow.Pipelines Build and Package Script${NC}"
echo "================================================="

# Configuration
SOLUTION_FILE="RtFlow.sln"
CONFIGURATION="Release"
OUTPUT_DIR="nupkgs"

# Clean previous builds
echo -e "${YELLOW}🧹 Cleaning previous builds...${NC}"
dotnet clean $SOLUTION_FILE --configuration $CONFIGURATION --verbosity minimal

# Create output directory if it doesn't exist
mkdir -p $OUTPUT_DIR

# Restore dependencies
echo -e "${YELLOW}📦 Restoring dependencies...${NC}"
dotnet restore $SOLUTION_FILE --verbosity minimal

# Build solution
echo -e "${YELLOW}🔨 Building solution...${NC}"
dotnet build $SOLUTION_FILE --configuration $CONFIGURATION --no-restore --verbosity minimal

# Run tests
echo -e "${YELLOW}🧪 Running tests...${NC}"
dotnet test $SOLUTION_FILE --configuration $CONFIGURATION --no-build --verbosity minimal

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ All tests passed!${NC}"
else
    echo -e "${RED}❌ Tests failed! Aborting package creation.${NC}"
    exit 1
fi

# Package projects
echo -e "${YELLOW}📦 Creating NuGet packages...${NC}"

echo -e "${BLUE}  📦 Packaging RtFlow.Pipelines.Core...${NC}"
dotnet pack RtFlow.Pipelines.Core/RtFlow.Pipelines.Core.csproj \
    --configuration $CONFIGURATION \
    --no-build \
    --output $OUTPUT_DIR \
    --verbosity minimal

echo -e "${BLUE}  📦 Packaging RtFlow.Pipelines.Extensions...${NC}"
dotnet pack RtFlow.Pipelines.Extensions/RtFlow.Pipelines.Extensions.csproj \
    --configuration $CONFIGURATION \
    --no-build \
    --output $OUTPUT_DIR \
    --verbosity minimal

echo -e "${BLUE}  📦 Packaging RtFlow.Pipelines.Hosting...${NC}"
dotnet pack RtFlow.Pipelines.Hosting/RtFlow.Pipelines.Hosting.csproj \
    --configuration $CONFIGURATION \
    --no-build \
    --output $OUTPUT_DIR \
    --verbosity minimal

echo ""
echo -e "${GREEN}🎉 Package creation completed successfully!${NC}"
echo -e "${BLUE}📁 Packages created in: ${OUTPUT_DIR}/${NC}"
echo ""

# List created packages
echo -e "${YELLOW}📋 Created packages:${NC}"
ls -la $OUTPUT_DIR/*.nupkg | while read line; do
    echo -e "${GREEN}  ✓ $line${NC}"
done

echo ""
echo -e "${BLUE}🚀 To publish to NuGet.org:${NC}"
echo -e "${YELLOW}  dotnet nuget push ${OUTPUT_DIR}/*.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json${NC}"
echo ""
echo -e "${BLUE}🔍 To verify packages locally:${NC}"
echo -e "${YELLOW}  dotnet nuget add source ${PWD}/${OUTPUT_DIR} --name local-packages${NC}"
echo ""
