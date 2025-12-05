# HistoryExport Solution (.NET 8 Migration)

## Project Overview

This solution manages the extraction, storage, and reporting of historical point data from the **Honeywell EBI** system. It consists of a background data collector and a web-based user interface for configuration and reporting.

**Original Framework:** .NET 4.0 / 4.5
**Current Framework:** .NET 8.0

### Components

1.  **HistoryExport.Web (formerly HistoryExport):**
    * A Web Interface for viewing system points.
    * Allows operators to edit descriptions and toggle archiving flags (`Arch`).
    * Generates PDF and Excel reports of the point configuration.
    * *Note:* Can trigger external processes (`bckbld.exe`) to refresh point definitions.

2.  **HistoryExport.Worker (formerly HistoryExportCMD):**
    * A Console/Worker application that acts as the ETL engine.
    * Connects to EBI via ODBC to fetch snapshots (5sec, 1min, 1hour).
    * Stores data into the local SQL `PointsHistory` database.
    * Handles High Availability (HA) synchronization between Primary and Backup servers.

---

## Database Architecture

**IMPORTANT:** The database schema is legacy and **immutable**. No migrations or schema changes should be applied via code.

### Connection Strings
* `PointsHistory`: The main SQL Server database storing configuration and historical data.
* `EBI_ODBC`: The source ODBC connection to the Honeywell system.
* `EBI_SQL`: Connection to the `master` or `hwsystem` DB to check Primary/Backup status.

### Key Tables
| Table | Description | Access Type |
| :--- | :--- | :--- |
| `Point` | Configuration of all points. Contains metadata and flags. | Read/Write (Update only) |
| `Parameter` | System configuration (e.g., global flags). | Read/Write |
| `History_5sec` | High-frequency historical data. | Insert/Select |
| `History_1min` | Medium-frequency historical data. | Insert/Select |
| `History_1hour` | Low-frequency historical data. | Insert/Select |
| `History_15min` | Calculated aggregation (derived from 1min). | Insert (Calculated) |

### Key Stored Procedures
* `sp_ReadEbiPoints`: Invoked by the Web App to refresh the `Point` table from the external `.pnt` file import.

---

## Functionality & Logic

### 1. Data Collection (Worker)
The worker checks the `hwsystem` database to determine if it is the **Primary** node.
* **If Primary:**
    * Iterates through enabled history types (Fast, Slow, Extended).
    * Queries EBI ODBC for the time range since the last successful fetch.
    * Inserts data into `History_X` tables.
* **If Backup (Secondary):**
    * Checks the `RedundantPointHistory` flag.
    * Connects to the *Primary* server's SQL database.
    * Syncs `Point` configuration and missing rows in `History` tables to the local database.

### 2. User Interface (Web)
* **Point Management:**
    * Users can edit: `Descriptor`, `Device`, `HistoryFastArch`, `HistorySlowArch`, `HistoryExtdArch`.
    * *Constraint:* `PointName` and `ParamName` are read-only keys linked to EBI.
* **Reporting:**
    * Exports the current grid configuration to Excel or PDF.
    * *Migration Note:* The original `.rdlc` logic has been replaced with [Insert New Library Name, e.g., QuestPDF/EPPlus].

---

## Configuration

Configuration is managed via `appsettings.json` (replacing `app.config` and `web.config`).

```json
{
  "ConnectionStrings": {
    "PointsHistory": "Server=(local); Database=PointsHistory; Integrated Security=true; TrustServerCertificate=True;",
    "EBI_ODBC": "DSN=EBIDatasource; RedundantLAN=0; RedundantCPU=0;",
    "EBI_SQL": "Server=(local); Database=master; Integrated Security=true; TrustServerCertificate=True;"
  },
  "AppSettings": {
    "ProcessPnt": "C:\\Proyectos\\HistoryExportCmd\\ProcessPnt.exe",
    "OldestDayFromToday": 1295,
    "RedundantPointHistory": false,
    "LogPath": "Logs",
    "LogMaxDays": 15
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
