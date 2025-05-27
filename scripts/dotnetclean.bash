#!/usr/bin/env bash

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "Removing old 'bin' and 'obj' directories from solution directory '$SOLUTION_DIR'..."

#--------------------------------------------------------------------------------

# Remove all bin and obj folders from the solution directory
find "$SOLUTION_DIR" -type d -name bin -print0 | xargs -0 rm -rf
find "$SOLUTION_DIR" -type d -name obj -print0 | xargs -0 rm -rf

#--------------------------------------------------------------------------------
# Done
#--------------------------------------------------------------------------------
echo "Done! Bin and obj folders cleaned up."