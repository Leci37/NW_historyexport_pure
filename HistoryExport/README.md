# HistoryExport (Web Application) - Technical Documentation & Migration Blueprint

## 1. Project Overview & Architecture

**HistoryExport** is the legacy ASP.NET Web Forms application that serves as the user interface for the History Export solution. It allows operators to configure which system points are archived and generates compliance reports.

* **Current Framework:** .NET Framework 4.5 (ASP.NET Web Forms)
* **Target Framework:** .NET 8.0 (ASP.NET Core Blazor Server or MVC)
* **Primary Function:** CRUD interface for Point configuration and RDLC-based Reporting.

### Dependencies
* **External Processes:** Relies on `bckbld.exe` and a configured `ProcessPnt.exe` to rebuild the point database from source files if the database is empty.
* **Libraries:**
    * `System.Web` (Legacy Web Forms logic).
    * `Microsoft.ReportViewer.WebForms` (Legacy reporting engine, **incompatible with .NET Core**).

---

## 2. Database & Data Access Layer (DAL)

**Constraint:** The database schema is **immutable**. The application acts as an interface to the existing SQL Server database.

### Connection String
* **Key:** `PointsHistory`
* **Target:** SQL Server

### Tables & Operations
| Table | Operation | Columns Accessed | Notes |
| :--- | :--- | :--- | :--- |
| **`Point`** | `SELECT` | `PointId`, `PointName`, `ParamName`, `Description`, `Device`, `History*`, `History*Arch` | Populates the main GridView. |
| **`Point`** | `UPDATE` | `Description`, `Device`, `HistoryFastArch`, `HistorySlowArch`, `HistoryExtdArch` | User-editable columns. **Note:** `PointName` and `ParamName` are Read-Only identity fields. |
| **`Parameter`** | `SELECT`/`UPDATE` | `Name`, `Value` | Used for global configuration settings. |

### Stored Procedures & Queries
1.  **`sp_ReadEbiPoints`**:
    * **Usage:** Called when the point list needs refreshing (manually or if empty).
    * **Parameters:** `@ProcessPnt` (Path to the external executable).
2.  **Hardcoded SQL:**
    * The `DBAccess.cs` class contains raw SQL strings for `SELECT` and `UPDATE` operations. These must be preserved exactly to ensure compatibility.

---

## 3. Core Application Logic

### A. Initialization & Auto-Discovery
On `Page_Load`, the application checks if the `Point` table is empty.
* **Logic:** If `Count == 0`, it triggers a localized process chain:
    1.  Runs `bckbld.exe -out [temp_file]` to dump points from the control system.
    2.  Runs the executable defined in `AppSettings["ProcessPnt"]` to parse that file.
    3.  Calls `sp_ReadEbiPoints` (via `DBAccess.Refresh`) to load the data into SQL.
* **Migration Note:** This dependency on local Windows executables likely prevents this app from running in a Linux container. It should be hosted on a Windows Server or Windows Container.

### B. User Workflows (Grid)
The UI presents a grid where specific columns are editable:
* **Editable:** `Descriptor` (Text), `Device` (Text), and Archiving Flags (`FastArch`, `SlowArch`, `ExtdArch`).
* **Read-Only:** System Flags (`HistoryFast`, `HistorySlow`, `HistoryExtd`) indicate if the point *can* be logged, while the "Arch" flags control if it *should* be logged.

### C. Reporting (Crucial Roadblock)
* **Current Impl:** Uses `Microsoft.ReportViewer.WebForms` and an `.rdlc` file (`HistoryExport.Points.rdlc`).
* **Function:** Generates PDF or Excel downloads of the current grid state.
* **Issue:** RDLC Local Reports are **not supported** in .NET Core.

### D. Logging
* **Current Impl:** Custom `LogFile` class using bitmask flags (`LogFlags.cs`).
* **Logic:** Logs to a text file in `AppSettings["LogPath"]` with log rotation based on `LogMaxDays`.

---

## 4. Migration Blueprint (.NET 8)

### Recommended Architecture: Blazor Server
Blazor Server is the closest modern equivalent to Web Forms, allowing stateful interaction (editing rows) without writing complex JavaScript APIs.

### Step-by-Step Migration Plan

#### 1. Data Access Layer (DAL) Replacement
* **Action:** Replace `DBAccess.cs` `DataSet/DataReader` code with **Dapper**.
* **Why:** Dapper allows you to copy-paste the existing raw SQL queries directly, ensuring exact compatibility with the immutable schema.
* **Task:** Create a `PointRepository` service.

#### 2. Reporting Engine Replacement
Since `ReportViewer` is obsolete, replace the "Export" button logic with:
* **For Excel:** Use **ClosedXML** (Open Source). It is lighter and easier than the legacy Interop or RDLC.
* **For PDF:** Use **QuestPDF** (Open Source) or **DinkToPdf**.
* **Logic Change:** Instead of rendering an `.rdlc`, the generic `List<Point>` data will be passed to a service that builds the file in memory and returns a `FileStreamResult`.

#### 3. Handling External Processes
* **Action:** Port the `RefreshPoints` method to a `PointDiscoveryService`.
* **Security:** Ensure the Application Pool Identity (IIS) or the Service User (Kestrel) has **Execute** permissions on `bckbld.exe` and **Write** permissions to `Path.GetTempPath()`.

#### 4. Logging Modernization
* **Action:** Replace `LogFile.cs` with **Serilog**.
* **Mapping:**
    * `LogFlags.TzINFORMATION` -> `Log.Information()`
    * `LogFlags.TzEXCEPTION` -> `Log.Error()`
    * `LogFlags.TzSQL` -> `Log.Debug()`
* **Config:** Configure Serilog to write to files (`.WriteTo.File()`) to mimic the legacy daily rotation.

### NuGet Package Requirements
| Package | Purpose |
| :--- | :--- |
| `Dapper` | Replacement for raw ADO.NET `SqlDataReader`. |
| `System.Data.SqlClient` | SQL Server connectivity. |
| `Serilog.AspNetCore` | Structured logging. |
| `Serilog.Sinks.File` | File logging support. |
| `ClosedXML` | For generating Excel reports (.xlsx). |
| `QuestPDF` | For generating PDF reports. |

---

## 5. Developer Notes & Quirks

1.  **Legacy Bitmasks:** The code uses `(Convert.ToUInt32(flag) & m_Mask) != 0` to filter logs. In .NET 8, rely on standard Log Levels (`Information`, `Debug`, `Error`) instead of porting this bitmask logic.
2.  **Explicit Redundancy Logic:** Ensure the `ProcessPnt` path in `appsettings.json` points to the correct location on the new server, as this file is critical for the initial data population.
3.  **Nullable Types:** The legacy `SqlDataReaderExtension.cs` handles DB nulls manually. Dapper handles this automatically, so that class can be deleted.