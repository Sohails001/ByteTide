# Contributing to ByteTide

Thanks for helping! All contributions are welcome: bug reports, docs, tests, features.

## Getting set up

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.
2. Fork and clone the repo.
3. Build and test:
   ```powershell
   dotnet build ByteTide.sln
   dotnet test tests/ByteTide.Tests/ByteTide.Tests.csproj
   dotnet run --project src/ByteTide.App
   ```

## Where things live

| Folder | Purpose |
|---|---|
| `src/ByteTide.Core` | Logic with no UI dependency (shared by App and Service). Put new logic here when you can, so it's testable. |
| `src/ByteTide.App` | WinForms UI |
| `src/ByteTide.Service` | Windows Service host |
| `tests/ByteTide.Tests` | xUnit tests |

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for more.

## Making a change

1. Open an issue first for anything big, so we can agree on the approach.
2. Create a branch: `git checkout -b feature/short-description`.
3. Keep changes focused; one topic per pull request.
4. Add or update tests when you change Core logic.
5. Make sure `dotnet build` and `dotnet test` pass. CI runs both on every PR.
6. Update `CHANGELOG.md` under an "Unreleased" heading and any affected docs.
7. Open the pull request and fill in the template.

## Style

- C# with nullable reference types enabled; follow the style of the surrounding code.
- Prefer clear names and short methods over clever code.
- Avoid per-tick allocations in code that runs every second (graph painting, sampling).

## Good first issues

- Persist per-day byte baselines so restarts don't under-count (see Known limitations in the README).
- Add ▲/▼ sort indicators to Reports column headers.
- Make the GUI database path configurable.
- Protect the stored SMTP password (e.g. DPAPI).
- More unit tests for `DailyRollupTracker`, `Storage` and `NotificationEngine`.

## Reporting bugs

Use the bug report template. Include your Windows version, ByteTide version, and steps to reproduce.

By contributing you agree that your work is released under the project's [MIT license](LICENSE).
