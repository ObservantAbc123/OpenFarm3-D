#!/bin/bash
# Start Xvfb in the background
Xvfb :99 -screen 0 1024x768x24 > /dev/null 2>&1 &
sleep 1

# Execute the renderer with all arguments
exec python main.py "$@"

