#!/bin/bash
# Frontend build script

echo "Building PINÈDE IDENTITY frontend..."
cd frontend/nexorsys-identity-ui

# Install dependencies
echo "Installing npm packages..."
npm install

# Run type check
echo "Running TypeScript type check..."
npm run typecheck

# Build production bundle
echo "Building production bundle..."
npm run build

echo "Frontend build completed successfully!"
echo "Output: frontend/nexorsys-identity-ui/dist"