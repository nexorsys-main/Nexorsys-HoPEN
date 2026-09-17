#!/bin/bash
# Development environment runner

echo "Starting PINÈDE IDENTITY development environment..."

# Start frontend
echo "Starting frontend development server..."
cd frontend/nexorsys-identity-ui
npm run dev &
FRONTEND_PID=$!

# Wait for frontend to start
sleep 3

# Start backend (if .NET is available)
if command -v dotnet &> /dev/null; then
    echo "Starting backend API..."
    cd ../../backend/src/Nexorsys.Identity.API
    dotnet watch run &
    BACKEND_PID=$!
else
    echo "WARNING: .NET SDK not found. Backend API will not start."
    echo "Please install .NET 8 SDK to run the backend."
fi

# Wait for any key to exit
echo "Development environment running."
echo "Press any key to stop all services..."
read -r

# Stop processes
if [ -n "$FRONTEND_PID" ]; then
    kill $FRONTEND_PID 2>/dev/null
fi
if [ -n "$BACKEND_PID" ]; then
    kill $BACKEND_PID 2>/dev/null
fi

echo "Development environment stopped."