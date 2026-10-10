# Security Policy

## Supported versions

Only the latest release receives fixes.

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

Use GitHub's private reporting: **Security → Report a vulnerability** on this repository. Include what you found, steps to reproduce, and the affected version. You can expect an acknowledgement within a few days.

## Things worth knowing

- ByteTide makes no network connections of its own. The only outbound traffic is email alerts if you configure SMTP.
- SMTP passwords are currently stored as plain text in the local settings table (`app_settings`). Use a dedicated or app-specific password, not a primary account password. Improving this (e.g. Windows DPAPI) is a welcome contribution.
- The Windows Service runs with whatever account you install it under. Prefer a least-privilege account where possible.
