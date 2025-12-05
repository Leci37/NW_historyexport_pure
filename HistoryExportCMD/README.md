# HistoryExportCMD (Worker Application) - Technical Documentation & Migration Blueprint

## 1. Project Overview & Architecture

**HistoryExportCMD** is a legacy Console Application acting as the backend ETL (Extract, Transform, Load) engine for the History Export solution. It is responsible for fetching historical data from the Honeywell EBI system and ensuring data redundancy across servers.

* **Current Framework:** .NET Framework 4.0 (Console Application)
* **Target Framework:** .NET 8.0 (Worker Service)
* **Primary Function:** ETL Process (ODBC -> SQL) and High Availability (HA) Synchronization.

### Dependencies
* **Honeywell EBI ODBC Driver:** Required to fetch historical snapshots.
* **SQL Server:** Acts as the destination for historical data and source for redundancy checks.

---

## 2. Database & Data Access Layer (DAL)

**Constraint:** The database schema is **immutable**. No Entity Framework Core migrations should be used.

### Connection Strings
* **`PointsHistory`**: Local SQL Server storage (Destination).
* **`EBI_ODBC`**: ODBC DSN to the Honeywell EBI system (Source).
* **`EBI_SQL`**: Connection to check the EBI system status (`IsPrimary`).

### Tables & Operations
| Table | Operation | Usage |
| :--- | :--- | :--- |
| **`Point`** | `SELECT` | configurations (Fast/Slow/Extd flags). |
| **`History_5sec`** | `SELECT`/`INSERT` | Fast history data storage. |
| **`History_1min`** | `SELECT`/`INSERT` | Slow history data storage. |
| **`History_1hour`** | `SELECT`/`INSERT` | Extended history data storage. |
| **`History_15min`** | `INSERT` (Calculated) | Aggregated average from `History_1min`. |

### Hardcoded SQL Patterns
* **Cross-Server Queries (Sync):** The `DBSync.cs` class constructs SQL strings dynamically to query a remote server directly:
    * `insert into {1}... select ... from {0}...`
    * *Migration Note:* This relies on SQL Server Linked Servers or direct network visibility between the Primary and Backup SQL instances.

---

## 3. Core Application Logic

The application logic is driven by the **EBI Status** (`GetEBIStatus`).

### A. Primary Mode (ETL Process)
If the current server is the **Primary** EBI node:
1.  **Iterate History Types:** Loops through Fast (1), Slow (2), and Extended (3) configurations.
2.  **Determine Time Window:**
    * Checks the last recorded timestamp in SQL (`GetLastDatetime`).
    * Calculates the gap between *Last Record* and *Now*.
    * *Limit:* Respects `OldestDayFromToday` config to avoid fetching ancient data.
3.  **Fetch Data (ODBC):**
    * Constructs a dynamic query: `SELECT ... FROM HistoryXSnapshot WHERE ...`.
    * Maps columns dynamically: `Parameter01`, `Value01`, `Quality01`.
4.  **Store Data (SQL):**
    * Uses a temporary table strategy (`#History`) to bulk insert data.
    * Moves data from `#History` to the final `History_X` table.

### B. Secondary Mode (Redundancy Sync)
If the current server is the **Backup** (and `RedundantPointHistory` is true):
1.  **Identify Peer:** Determines the Primary server name (swaps 'A' suffix for 'B' or vice versa).
2.  **Sync Points:**
    * Inserts missing points from Primary to Local.
    * Updates metadata (Descriptions, Device) from Primary to Local.
3.  **Sync History:**
    * Checks `MAX(USTTimestamp)` on both servers.
    * Pulls missing rows from the Primary SQL Server to the Local SQL Server in 1-hour chunks.

---

## 4. Migration Blueprint (.NET 8)

### Recommended Architecture: Windows Worker Service
Convert the Console App into a **Windows Service** (`Microsoft.Extensions.Hosting`) to ensure it runs continuously in the background and restarts automatically on failure.

### Step-by-Step Migration Plan

#### 1. Configuration Modernization
* **Action:** Migrate `app.config` XML to `appsettings.json`.
* **Task:** Create a strongly-typed `WorkerSettings` class to hold `OldestDayFromToday`, `RedundantPointHistory`, etc.

#### 2. Data Access Layer (DAL) Refactoring
* **Action:** Rewrite `DBAccess.cs` using **Dapper**.
* **ODBC Handling:** Use `System.Data.Odbc` (NuGet package) for the EBI connection.
    * *Critical Check:* Ensure the ODBC Driver version (32-bit vs 64-bit) matches the .NET 8 runtime (x64 or x86).
* **SQL Handling:** Use `Microsoft.Data.SqlClient`.
    * Preserve the `#History` temp table logic using `Execute` commands in Dapper to maintain performance.

#### 3. Redundancy Logic
* **Action:** Refactor `DBSync.cs`.
* **Challenge:** The string formatting `{0}.PointsHistory.dbo...` implies direct server addressing.
* **Task:** Ensure the Connection String user has permissions to access the linked/remote server in the SQL command.

#### 4. Logging Replacement
* **Action:** Replace `LogFile.cs` with **Serilog**.
* **Pattern:**
    * `LogFlags.TzINFORMATION` -> `Log.Information()`
    * `LogFlags.TzEXCEPTION` -> `Log.Error()`
    * `LogFlags.TzSQL` -> `Log.Debug()` (Useful for debugging the dynamic ODBC queries).

### NuGet Package Requirements
| Package | Purpose |
| :--- | :--- |
| `Microsoft.Extensions.Hosting` | Worker Service scaffolding. |
| `Microsoft.Extensions.Hosting.WindowsServices` | Run as a Windows Service. |
| `Dapper` | Lightweight ORM for raw SQL execution. |
| `System.Data.Odbc` | Required for connecting to Honeywell EBI. |
| `Microsoft.Data.SqlClient` | Modern SQL Server client. |
| `Serilog.Extensions.Hosting` | Logging integration. |

---

## 5. Developer Notes & Quirks

1.  **ODBC Architecture Mismatch:** The legacy app targets `x86`. If you migrate to .NET 8 (which defaults to x64), the `System.Data.Odbc` call **will fail** if the installed Honeywell ODBC driver is 32-bit. You may need to force the .NET 8 project platform target to `x86`.
2.  **Date Handling:** The code uses `DateTime(2000, 1, 1)` as a magic "Start of Time". Preserve this default to avoid fetching data from the beginning of the epoch.
3.  **Immutable Schema:** Do not attempt to add Primary Keys or Indexes to the `History` tables to "fix" them. The Sync logic relies on specific `LEFT JOIN` patterns that assume the current schema structure.
4.  **Bitmask Logging:** The legacy `LogFile` checks `(flag & m_Mask) != 0`. Do not port this complexity; simply use `appsettings.json` "MinimumLevel" configuration to control verbosity.