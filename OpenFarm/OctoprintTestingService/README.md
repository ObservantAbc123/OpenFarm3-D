# Prusa MK4 Simulation Environment

This package provides a portable simulation environment for a Prusa MK4 3D printer using Docker and OctoPrint. It allows
for testing a job management system without needing physical hardware.

## Contents

- `OctoprintTestingService`: Directory containing microservice.

## Requirements

- Docker Desktop
- ~2.0 GB free disk space
- Network connectivity (for initial image download)

## Quick Start

1. Ensure Docker Desktop is installed and running.
2. Run 'docker-compose up --build' which will also download the OctoPrint image if it hasn't been already, and build it.
3. Access the OctoPrint web UI by navigating to 'http://localhost:5000'.
4. Create an account via the wizard, and once complete, you may proceed using the instance.

## Customization

Edit `compose.yml` to:

- Change port mappings
- Modify environment variables
- Add volumes

## Usage Notes

This environment simulates basic Prusa MK4 behavior but has the following limitations:

- Limited firmware-specific features
- Simplified temperature simulation
- No actual motor movement simulation (obviously)

For the most realistic testing, use G-code files generated specifically for the Prusa MK4.