# Importing RAS Graph & Stats history

ByteTide can read the plain-text `.dat` archive files written by RAS Graph & Stats and merge them into its own database, so old and new history appear together in Reports.

## Steps

1. Locate your old data folder (the one containing the `.dat` archive files).
2. In ByteTide choose **File → Import Legacy Data...**
3. Click **Browse** and select that folder.
4. Click **Import**.

## Safe to repeat

Rows are *upserted* by (date, adapter name, MAC address). Re-importing the same files updates existing rows instead of duplicating them.

## File format notes

Each file holds several `[dateSerial]` sections. The serial is an OLE Automation date (days since 1899-12-30). Within a section, each adapter has an `Adapter Name @ MAC=seconds` line plus `Hours`, `Recv` and `Xmit` values. The importer keeps values for different adapters on the same day separate; this is covered by unit tests in `tests/ByteTide.Tests/LegacyImporterTests.cs`.

## Troubleshooting

- **Nothing imported**: confirm the folder contains `.dat` files and that you chose the folder, not a single file.
- **Odd dates**: the date comes from the section header serial; files edited by hand may have been altered.
- **Found a file that fails?** Please open an issue and attach a *sanitised* sample (replace MAC addresses if you like); more real-world samples make the importer better.
