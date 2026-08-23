# Installer plan

The production distribution should use a small bootstrap installer rather than making the only download a self-contained .NET application.

Planned responsibilities:

1. OS/architecture validation
2. .NET Desktop Runtime detection/install
3. GitHub release-manifest download
4. payload download
5. SHA-256 verification
6. UI + service install/update
7. optional PawnIO prerequisite when a verified feature needs it
8. launch ThinkControl after install when requested

The bootstrapper is also the on-demand elevated updater path for replacing the Windows service. There should be no permanent updater service.

Implementation is intentionally deferred until the v0.1 architecture and release format are stable.
