# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- In `--verbose` mode, report skipped unreadable directory enumeration errors to stderr with the failing path and reason while continuing the scan past inaccessible directories.
- `dsweep --version` prints `DupeSweep <version>` and exits 0 without scanning, including when a
  directory argument is present. The version is read from the assembly version metadata that the
  build sets from the single `Version` property in `DupeSweep.csproj`.
- Initial release: `dsweep <dir>` scan mode with size → quick-hash → full-SHA-256 duplicate
  detection funnel, parallel hashing, `--keep` strategies (first/oldest/newest/shortest-path),
  `--apply quarantine` (reversible) and `--apply delete` (permanent), JSON reporting, and
  `dsweep restore <manifest.json>` to undo a quarantine.
- Docker support: a multi-stage `Dockerfile` that runs the full test suite as a build gate and
  ships a minimal runtime image running as the non-root `app` user.
