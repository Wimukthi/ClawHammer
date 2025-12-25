# ClawHammer

ClawHammer is a Windows CPU stress tester with live hardware telemetry, profiles, and optional CSV logging.

## Features
- Stress workloads: Floating point, integer primes, AVX, mixed.
- Live CPU temperature and usage tracking (LibreHardwareMonitor).
- Auto-stop on temperature threshold or throttling.
- Profiles, timed runs, and validation loop.
- Optional CSV telemetry and temperature plot window.

## Requirements
- Windows 10/11
- .NET 10 SDK

## Build
- Open `ClawHammer.sln` in Visual Studio 2022 or later.
- Build the `ClawHammer` project (x86/x64 configs).

## Run
- Run the built executable from `ClawHammer/bin/.../ClawHammer.exe`.

## Notes
- Sensor icons are loaded from the `ClawHammer/icons` folder at runtime.
- Non-runtime assets are stored in `assets/`.

## License
GPL-3.0. See `LICENSE`.
