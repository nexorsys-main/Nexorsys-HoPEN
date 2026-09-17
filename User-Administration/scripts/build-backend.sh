#!/bin/bash
# Backend build script (for when .NET SDK is available)

echo "Building PINÈDE IDENTITY backend..."
cd backend

# Restore NuGet packages
echo "Restoring NuGet packages..."
dotnet restore

# Build solution
echo "Building solution..."
dotnet build --configuration Release

echo "Backend build completed successfully!"