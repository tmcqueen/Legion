#!/bin/bash

set -e

SCRIPT_SOURCE_DIR="$(dirname "${BASH_SOURCE[0]}")"
# echo "Script source directory: $SCRIPT_SOURCE_DIR"
# exit 1

APP_CONTEXT="AppDbContext"
AUTH_CONTEXT="AuthDbContext"

SCRIPT_DIR="$(cd "$SCRIPT_SOURCE_DIR" && pwd)"
PROJECT_NAME="Legion.Admin.Data.Sqlite"
MIGRATIONS_BASE_DIR="$SCRIPT_DIR/Migrations"

if [ $# -eq 0 ]; then
    echo "Usage: $0 <migration-name>"
    echo "Example: $0 AddUserSchema"
    exit 1
fi

MIGRATION_NAME="$1"

echo "Starting migration creation for $PROJECT_NAME"
echo "Migration name: $MIGRATION_NAME"
echo "Building project to ensure it's up to date..."
dotnet build "$SCRIPT_DIR/$PROJECT_NAME.csproj" #--configuration Release

if [ $? -ne 0 ]; then
    echo "Build failed. Please fix the build errors before creating migrations."
    exit 1
fi

echo "Creating migration: $MIGRATION_NAME for $PROJECT_NAME"
echo "Working directory: $SCRIPT_DIR"

dotnet ef migrations add "$MIGRATION_NAME" \
    --context "$APP_CONTEXT" \
    --output-dir "$MIGRATIONS_BASE_DIR/App" \
    --no-build

echo "✓ Migration '$MIGRATION_NAME' for $APP_CONTEXT created successfully"
echo "Migration files are in: $MIGRATIONS_BASE_DIR/App"


dotnet ef migrations add "$MIGRATION_NAME" \
    --context "$AUTH_CONTEXT" \
    --output-dir "$MIGRATIONS_BASE_DIR/Auth" \
    --no-build

echo "✓ Migration '$MIGRATION_NAME' for $AUTH_CONTEXT created successfully"
echo "Migration files are in: $MIGRATIONS_BASE_DIR/Auth"