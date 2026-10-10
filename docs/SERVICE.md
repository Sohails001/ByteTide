# Running ByteTide as a Windows Service

The service project (`ByteTideService.exe`) monitors traffic in the background with nobody logged in, writes the same database schema as the GUI app, and regenerates a plain-text usage report on a schedule.

> The service and the GUI app are **separate programs with separate databases by default**. See [Sharing one database](#sharing-one-database).

## 1. Publish

```powershell
dotnet publish src\ByteTide.Service -c Release -r win-x64 --self-contained false
```

Output: `src\ByteTide.Service\bin\Release\net8.0-windows\win-x64\publish\` containing `ByteTideService.exe` and `appsettings.json`. Because this build is framework-dependent, the target machine needs the [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). Use `--self-contained true` to avoid that.

Copy the publish folder somewhere permanent, such as `C:\Program Files\ByteTide Service\`.

## 2. Install (as Administrator)

```powershell
sc.exe create ByteTideService binPath= "C:\Program Files\ByteTide Service\ByteTideService.exe" start= auto
sc.exe start ByteTideService
```

Note the space after `binPath=` and `start=`; `sc.exe` requires it.

## 3. Configure

Edit `appsettings.json` next to the executable:

```json
{
  "ByteTide": {
    "DataDbPath": "",
    "ReportIntervalSeconds": 300,
    "ReportOutputPath": "C:\\ProgramData\\ByteTide\\report.txt"
  }
}
```

| Setting | Meaning |
|---|---|
| `DataDbPath` | Path to the SQLite file. Empty = `%ProgramData%\ByteTide\data.db`. |
| `ReportIntervalSeconds` | How often the summary report is rewritten. Default 300. |
| `ReportOutputPath` | Where the report is written. Leave empty to disable. Point at a web-served folder to check usage from a browser. |

Restart after editing:

```powershell
sc.exe stop ByteTideService
sc.exe start ByteTideService
```

## The report

A short month-to-date summary:

```
ByteTide Service Report -- generated 2026-01-15 09:30:00
Month to date (2026-01-01 to 2026-01-15):
  Downloaded: 12.4 GB
  Uploaded:   1.8 GB
  Total:      14.2 GB
```

## Sharing one database

Services usually run as `LocalSystem`, so `%AppData%` would not point at your user profile; that's why the default is `%ProgramData%`. To view service-collected history in the GUI, the GUI must read the same file. The GUI's path is currently fixed at `%AppData%\ByteTide\data.db`, so the simplest approach is to point the service's `DataDbPath` at that file (the account running the service needs read/write access). Making the GUI path configurable is a [good first issue](../CONTRIBUTING.md).

Avoid running the GUI and the service against the same database *and* both collecting; daily totals would be double-counted.

## Remove

```powershell
sc.exe stop ByteTideService
sc.exe delete ByteTideService
```

## Troubleshooting

- **Service won't start**: check Event Viewer → Windows Logs → Application, and confirm the .NET 8 runtime is installed (for framework-dependent builds).
- **No report file**: ensure `ReportOutputPath` is set and the service account can write to that folder.
- **Run it in a console to debug**: stop the service, then run `ByteTideService.exe` from a terminal; it behaves the same and logs to the console.
