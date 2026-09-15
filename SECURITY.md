# Security Policy

## Supported Versions
| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability or credential exposure risk in StreamMaxx:
- Please report it via GitHub Security Advisories or contact the maintainers directly.
- **Do NOT open a public issue** with sensitive information.

## Credential Safety Guarantee
- All stream keys, ingest endpoints, and secrets are stored in `config/destinations.json`, which is explicitly excluded from version control via `.gitignore`.
- Pull Requests that attempt to track credentials or expose sensitive memory buffers will be rejected immediately by automated CI checks.
