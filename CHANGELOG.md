# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial release: `dsweep <dir>` scan mode with size → quick-hash → full-SHA-256 duplicate
  detection funnel, parallel hashing, `--keep` strategies (first/oldest/newest/shortest-path),
  `--apply quarantine` (reversible) and `--apply delete` (permanent), JSON reporting, and
  `dsweep restore <manifest.json>` to undo a quarantine.
