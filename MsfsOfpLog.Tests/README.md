# MSFS OFP Log Tests

This directory contains unit tests for the MSFS OFP Log application.

## Running Tests

To run all tests:
```bash
dotnet test
```

To run tests with verbose output:
```bash
dotnet test --verbosity detailed
```

## Test Coverage

The test suite includes:

- **SystemClock Tests**: Verify time simulation functionality
- **GpsFixTracker Tests**: Test GPS waypoint detection and tracking
- **DataLogger Tests**: Verify OFP summary generation with comprehensive flight plans
- **Flight Simulation Tests**: End-to-end testing of the flight tracking workflow

## Test Structure

- Tests use xUnit framework
- TestSystemClock is used for time simulation
- Tests include all 20 waypoints from the LGRP-ESSA flight plan
- Tests verify proper OFP formatting and fuel consumption calculations
