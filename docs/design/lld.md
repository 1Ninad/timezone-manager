# Low-Level Design — Timezone Manager

> Written for a first-time intern. Assumes basic understanding of C# and web concepts, but explains .NET-specific things as they come up.

---

## Component 1 — SQL Server Database Schema

**What this is:** The actual table in the database that stores each record.

One table is all we need. Two decisions: the data type for the timestamp column, and whether to store the submitter's timezone.

### Approaches

| Option | Type | What it means |
|---|---|---|
| A | `datetime` | Old SQL Server type. Precision ~3ms. Microsoft says "don't use this for new projects." |
| B | `datetime2(7)` | Modern type. Precision up to 100 nanoseconds. Range: year 0001–9999. |
| C | `datetimeoffset(7)` | Like datetime2, but also stores the UTC offset (e.g. +05:30). |

**Why not A:** Outdated. Microsoft themselves say to use `datetime2` for new code.

**Why not C:** We are *always* storing UTC in this column — the offset would always be `+00:00`. That's redundant. Wastes space (10 bytes vs 8), and adds visual noise in queries.

**Recommendation: `datetime2(7)`** — precise, modern, and since we always store UTC we don't need to carry an offset alongside it.

### Why store the submitter's timezone at all?

Two reasons:
1. **Plant may be removed or left blank in future.** Right now Plant implicitly tells you where an entry came from. If it ever goes away, you lose that location context entirely. Timezone fills that gap.
2. **Investigations need to reconstruct local time at the source.** UTC tells you *when* something happened universally. But support teams often need to know *"what was the local time at the plant when this was entered?"* — that question can only be answered if you stored the submitter's timezone.

Captured automatically from the browser on submit — the user never types it.

### What to store — IANA ID, not country/region

Store the submitter's **IANA timezone ID** (e.g. `"America/Sao_Paulo"`, `"Asia/Tokyo"`), not a country name or region label.

Why IANA ID and not country:
- Country is too coarse — Brazil has 4 timezones. "Brazil" tells you nothing precise.
- IANA ID is what the browser gives you natively via JS. No extra mapping needed.
- It's the industry standard format used by every timezone library.

This column is captured automatically from the browser on submit — the user never types it.

### Table definition

```sql
CREATE TABLE DeliveryRecords (
    Id                  INT           IDENTITY(1,1)  PRIMARY KEY,
    DeliveryNumber      BIGINT        NOT NULL,
    Plant               NVARCHAR(50)  NOT NULL,
    Material            NVARCHAR(100) NOT NULL,
    TimestampUtc        datetime2(7)  NOT NULL,
    SubmitterTimezone   NVARCHAR(100) NOT NULL
);
```

`IDENTITY(1,1)` means SQL Server auto-assigns an incrementing ID — we never need to set it manually.

`SubmitterTimezone` stores the IANA timezone ID of whoever submitted the record (e.g. `"Europe/Stockholm"`). Useful for investigations if Plant is ever removed or if you need to reconstruct "what local time was it for the person who entered this."

---

## Component 2 — EF Core DbContext & Entity Model

**What this is:** The C# code that represents the database table and lets you talk to it from code.

EF Core (Entity Framework Core) is an ORM — it maps C# objects to database rows so you don't have to write raw SQL. You just work with C# classes and EF handles the SQL behind the scenes.

### Approaches

| Option | Name | How it works |
|---|---|---|
| A | **Database First** | Create the table in SQL Server first, then run a command to auto-generate the C# class. |
| B | **Code First** | Write the C# class first, then EF generates migrations and creates the table in the DB. |

**Why not B:** Code First is great for greenfield projects where C# is the source of truth. But CLAUDE.md specifies Database First — the DB schema is the source of truth here. Also, if the database already exists independently (which is common at large companies like Tetra Pak), Database First is the correct approach.

**Recommendation: Database First (Approach A)** — matches the project spec. You create the table in SQL Server, then run one terminal command and EF generates everything else.

### What the scaffold command looks like

```bash
dotnet ef dbcontext scaffold "Server=...;Database=...;..." \
  Microsoft.EntityFrameworkCore.SqlServer \
  --output-dir Models \
  --context AppDbContext
```

This generates two files automatically:
- `Models/DeliveryRecord.cs` — the C# class matching the table
- `Models/AppDbContext.cs` — the "connection manager" between C# and the DB

### What goes in `Program.cs`

Register the DbContext factory (not just DbContext — explained in Component 3):
```csharp
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

And add the connection string to `appsettings.json`:
```json
"ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=TimezoneManager;Trusted_Connection=True;"
}
```

---

## Component 3 — Data Access Service

**What this is:** A C# class that does the actual database operations (save a record, get all records). Components call this instead of hitting the database directly.

### Approaches

| Option | Approach | Description |
|---|---|---|
| A | Inject DbContext directly into Razor pages | Simplest to set up. Mixes data logic and UI logic in the same file. |
| B | Separate service class (`DeliveryService`) | Clean separation. UI pages call the service; service talks to DB. |
| C | Repository + Service pattern | Adds an `IDeliveryRepository` interface layer. Useful if you want to swap DB providers or mock for testing. |

**Why not A:** Works, but messy. If you want to change how data is fetched later, you'd have to hunt through every page.

**Why not C:** Overkill for a single-table internal tool. The extra abstraction layer adds files and complexity with no real benefit at this scale.

**Recommendation: Service class (Approach B)** — clean, simple, and the right size for this tool.

### Important Blazor Server caveat

In Blazor Server, your app can have many users connected at the same time (it's a real-time server app). If you register DbContext with `AddDbContext`, multiple users might accidentally share one connection, causing errors.

The fix: use `IDbContextFactory<AppDbContext>` instead of injecting `AppDbContext` directly. The factory creates a fresh DbContext for each operation. This is the standard Blazor Server pattern.

### What `DeliveryService` looks like

```csharp
public class DeliveryService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public DeliveryService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public async Task SaveRecordAsync(long deliveryNumber, string plant, string material, DateTime timestampUtc, string submitterTimezone)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.DeliveryRecords.Add(new DeliveryRecord {
            DeliveryNumber = deliveryNumber,
            Plant = plant,
            Material = material,
            TimestampUtc = timestampUtc,
            SubmitterTimezone = submitterTimezone
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<DeliveryRecord>> GetAllRecordsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.DeliveryRecords.OrderByDescending(r => r.TimestampUtc).ToListAsync();
    }
}
```

Register in `Program.cs`:
```csharp
builder.Services.AddScoped<DeliveryService>();
```

---

## Component 4 — JavaScript Timezone Interop

**What this is:** The JavaScript code that tells the C# side "what time is it in the user's browser right now" and "convert this UTC time to the user's local time for display."

**This is the most critical component in the whole project.** Get this wrong and every timestamp is wrong.

### Why JavaScript is unavoidable here

Blazor Server runs your C# code on the **server**, not in the user's browser. Think of it like this: the server is sitting in a data center, and the user is in Stockholm, Singapore, or São Paulo. The server has absolutely no idea what timezone the browser is in. The only way to find out is to ask the browser — and the only language the browser speaks is JavaScript.

So the pattern is:
1. C# calls a JavaScript function via `IJSRuntime` (a built-in Blazor service)
2. JavaScript runs in the user's browser, gets the timezone/time info
3. JavaScript returns the result to C#

---

### For INPUT — capturing "what time is it right now for this user"

You might think: "I need to get the user's timezone, then convert their local time to UTC in C#." That would work, but there's a much simpler way.

**Option A: `new Date().toISOString()` — direct UTC from JS**

JavaScript's `Date` object always represents the current moment in time. When you call `.toISOString()` on it, JavaScript always returns UTC — it handles the local-to-UTC conversion internally, automatically.

```javascript
window.getCurrentUtcIso = () => new Date().toISOString();
// Returns: "2024-01-15T10:30:00.000Z"  ← always UTC, regardless of user's timezone
```

In C#, parse it:
```csharp
var utcString = await JS.InvokeAsync<string>("getCurrentUtcIso");
var utcDateTime = DateTime.Parse(utcString, null, DateTimeStyles.RoundtripKind);
// utcDateTime.Kind == DateTimeKind.Utc ✓
```

Tradeoff: Extremely simple. No timezone name needed. Works correctly for DST automatically.

**Option B: Get IANA timezone name, capture local datetime, convert in C#**

```javascript
window.getTimezoneId = () => Intl.DateTimeFormat().resolvedOptions().timeZone;
// Returns: "America/New_York" or "Asia/Kolkata"
```

Then in C#, use `TimeZoneConverter` NuGet package to map the IANA name to a .NET `TimeZoneInfo` object, then convert local → UTC.

Tradeoff: More code. .NET's built-in timezone system uses Windows names ("Eastern Standard Time"), not IANA names ("America/New_York") — so you need an extra NuGet package (`TimeZoneConverter`) just to bridge that gap. Same end result as Option A but significantly more complex.

**Recommendation for INPUT: Option A** — `new Date().toISOString()` called on form submit. Capture UTC directly from JavaScript. No timezone name, no conversion package, no complexity.

---

### For OUTPUT — showing UTC timestamps in the viewer's local time

The records are stored as UTC. When the display page loads, we need to convert each UTC value to the viewer's local time.

> **Common question: "Does it show Brazil time or Japan time?"**
> It shows **the current viewer's time** — whoever has the page open right now.
> Here's why this can't go wrong: we never store the submitter's timezone in the DB. Once a Brazil user's `2pm Brazil time` is saved as `5pm UTC`, the Brazil timezone is gone. When a Japan user opens the records page, the JS call (`new Date(utcString).toLocaleString()`) runs **in Japan's browser**, so it returns Japan local time. If the same Japan user moves to Brazil and opens the same page, they'd see Brazil local time — because the browser changed. The conversion always happens in whoever's browser is open at that moment. This is the intended behavior per the spec: every viewer sees timestamps in their own local time.

**Option A: Call JS once per row in the grid**

Each row in the grid calls `IJSRuntime` to convert its UTC value to local time.

Tradeoff: If you have 100 records, that's 100 separate JavaScript calls. JavaScript ↔ C# calls have overhead (they go through a SignalR connection). This will be noticeably slow. **Avoid.**

**Option B: Get IANA timezone once in C#, convert all records server-side**

Call JS once to get `Intl.DateTimeFormat().resolvedOptions().timeZone`. Then use `TimeZoneConverter` NuGet to convert all UTC timestamps to local `DateTimeOffset` in C#. Pass the formatted strings to the grid.

Tradeoff: One JS call. But requires `TimeZoneConverter` package. Also, formatting (e.g., "Jan 15, 2024 at 3:30 PM") needs to be done in C#, which is more verbose.

**Option C: Pass all UTC timestamps as an array to JS in one call**

Call JS once, pass all UTC timestamps, get back all formatted local strings.

```javascript
window.convertUtcArrayToLocal = (utcStrings) => {
    return utcStrings.map(s => new Date(s).toLocaleString());
};
// Returns: ["1/15/2024, 3:30:00 PM", "1/14/2024, 9:00:00 AM", ...]
```

Tradeoff: One JS call. Browser automatically handles DST, locale, and formatting. No extra NuGet package. `toLocaleString()` uses the user's own browser locale settings for the format.

**Recommendation for OUTPUT: Option C** — batch call. One JS round-trip, browser handles all timezone logic natively.

---

### Final JS interop design

One file: `wwwroot/js/timezone-interop.js`

```javascript
window.timezoneInterop = {

    // Called on form submit — returns current moment as UTC ISO string
    getCurrentUtcIso: () => new Date().toISOString(),

    // Called on form submit — returns submitter's IANA timezone ID (e.g. "America/Sao_Paulo")
    getIanaTimezoneId: () => Intl.DateTimeFormat().resolvedOptions().timeZone,

    // Called on records page load — converts array of UTC strings to viewer's local time strings
    convertUtcArrayToLocal: (utcStrings) =>
        utcStrings.map(s => new Date(s).toLocaleString('default', {
            year: 'numeric', month: 'short', day: 'numeric',
            hour: '2-digit', minute: '2-digit', second: '2-digit'
        }))
};
```

Add to `App.razor` just before `</body>`:
```html
<script src="js/timezone-interop.js"></script>
```

In any Blazor component that needs it:
```csharp
@inject IJSRuntime JS

// On form submit — get both values together before saving:
var utcIso      = await JS.InvokeAsync<string>("timezoneInterop.getCurrentUtcIso");
var timezoneId  = await JS.InvokeAsync<string>("timezoneInterop.getIanaTimezoneId");

// On records page load:
var localTimes  = await JS.InvokeAsync<string[]>("timezoneInterop.convertUtcArrayToLocal", utcStrings);
```

> `getIanaTimezoneId` reads the browser's OS-level timezone setting. It returns the IANA name, not an offset — so it's DST-aware automatically (e.g. `"America/New_York"` knows when to be EST vs EDT).

---

## Component 5 — Input Form Page

**What this is:** The page where a user enters Delivery Number, Plant, and Material. The timestamp is captured automatically on submit.

### When to capture the timestamp

| Option | When | Tradeoff |
|---|---|---|
| A | On page load (`OnInitializedAsync`) | Simple code. But if user spends 5 minutes filling the form, the timestamp is 5 minutes old. Not accurate. |
| B | On submit (button click handler) | Captures the exact moment the user clicks Save. Accurate. |

**Recommendation: On submit (Option B)** — the timestamp should represent when the user *submitted* the entry, not when they *opened* the page.

### Form model

Create a separate C# model for the form (not the DB entity — keeping them separate avoids accidental exposure of DB internals):

```csharp
// Models/DeliveryFormModel.cs
public class DeliveryFormModel
{
    [Required(ErrorMessage = "Delivery Number is required")]
    [Range(1, long.MaxValue, ErrorMessage = "Must be a positive number")]
    public long DeliveryNumber { get; set; }

    [Required(ErrorMessage = "Plant is required")]
    [MaxLength(50)]
    public string Plant { get; set; } = "";

    [Required(ErrorMessage = "Material is required")]
    [MaxLength(100)]
    public string Material { get; set; } = "";
}
```

### Kendo UI components to use

| Field | Kendo Component | Why |
|---|---|---|
| Delivery Number | `TelerikNumericTextBox<long>` | Built-in numeric keyboard on mobile, no letters allowed |
| Plant | `TelerikTextBox` | Free text. (If a fixed plant list exists, swap to `TelerikDropDownList` — easy to change later) |
| Material | `TelerikTextBox` | Free text |
| Submit button | `TelerikButton` | Styled consistently with the rest of the Kendo UI |
| Success/error alerts | `TelerikNotification` | Toast-style alerts, dismissable |
| Form wrapper | `TelerikForm` | Handles validation display automatically |

### Submit handler flow

```
User clicks Save
  → C# calls JS: timezoneInterop.getCurrentUtcIso()   → "2024-01-15T10:30:00.000Z"
  → C# calls JS: timezoneInterop.getIanaTimezoneId()  → "America/Sao_Paulo"
  → C# parses UTC string to DateTime
  → C# calls DeliveryService.SaveRecordAsync(..., timestampUtc, submitterTimezone)
  → On success: reset form fields, show green notification
  → On error: show red notification with message
```

Both JS calls happen on submit, before the save, so both values reflect the same moment.

### Page route

```csharp
@page "/new-entry"
```

---

## Component 6 — Records Display Page

**What this is:** The page that lists all saved records. Shows each record with the timestamp in the viewer's own local time.

### Grid component choice

| Option | Component | Notes |
|---|---|---|
| A | `TelerikGrid<T>` | Built-in sorting, filtering, pagination. Professional. Right choice. |
| B | `TelerikListView` | For card layouts. Not suitable for tabular data. |
| C | Plain HTML `<table>` | No built-in features. Would need custom sorting/paging. |

**Recommendation: `TelerikGrid<T>`** — this is exactly what Kendo UI grids are designed for.

### Data loading

| Option | Approach | When to use |
|---|---|---|
| A | Load all records on init | Simple. Works for hundreds to a few thousand records. |
| B | Server-side paging with `OnRead` | Better for tens of thousands of records. More code. |

**Recommendation: Option A** — for an internal support tool at Tetra Pak, the dataset will be manageable. Load all records in `OnInitializedAsync`. Add server-side paging only if performance becomes a problem later.

### UTC → Local time conversion flow

Since we're using batch JS conversion (from Component 4's recommendation):

```
OnInitializedAsync:
  1. Load all DeliveryRecord rows from DB via service
  2. Extract TimestampUtc values → convert to ISO strings array
  3. Call JS: timezoneInterop.convertUtcArrayToLocal(utcStrings)
  4. JS returns: array of formatted local time strings
  5. Zip records + local time strings → list of DeliveryRecordDisplay
  6. Bind DeliveryRecordDisplay list to TelerikGrid
```

Create a display model (not the DB entity):

```csharp
// Models/DeliveryRecordDisplay.cs
public class DeliveryRecordDisplay
{
    public long DeliveryNumber { get; set; }
    public string Plant { get; set; } = "";
    public string Material { get; set; } = "";
    public string LocalTimestamp { get; set; } = "";      // viewer's local time, formatted by JS
    public string SubmitterTimezone { get; set; } = "";   // IANA ID of who entered it, e.g. "Asia/Tokyo"
}
```

### Grid configuration

- Columns: Delivery Number | Plant | Material | Date & Time (viewer local) | Entered From (timezone)
- Sorting: enabled on all columns (user can sort by time to see chronological order)
- Filtering: enabled (support team can filter by Plant, Delivery Number, or timezone while investigating)
- Pagination: 20 rows per page

The `SubmitterTimezone` column (e.g. `"America/Sao_Paulo"`) lets investigators immediately see where an entry originated, even if Plant is empty or removed in future.

### Page route

```csharp
@page "/records"
```

---

## Component 7 — App Layout & Tetra Pak Theme

**What this is:** The navigation shell and visual styling that wraps every page. Makes the tool look like a Tetra Pak internal product.

### Kendo UI theming approaches

| Option | Approach | Complexity |
|---|---|---|
| A | CSS variable overrides on top of a base Kendo theme | Low. Add ~20 lines of CSS. |
| B | Kendo ThemeBuilder (online tool) | Medium. Generate a custom CSS file with your brand colors. |
| C | SASS compilation | High. Requires Node.js, npm, SASS build pipeline. Full control but heavy setup. |

**Recommendation: Option A** — Reference Kendo's Default theme (already available via their NuGet/CDN), then write a small CSS file that overrides the color variables. No toolchain, no build step, easy to tweak.

Kendo UI ships CSS custom properties (CSS variables) like `--kendo-color-primary`, `--kendo-color-base`, etc. Override them in `app.css`:

```css
:root {
    --kendo-color-primary: #023F88;        /* Tetra Pak Blue */
    --kendo-color-primary-hover: #00BDF2;  /* Tetra Pak Cyan on hover */
}
```

Plus standard styling for `navbar`, sidebar/top-row background, heading colors, button colors.

### Changes needed to existing files

| File | What changes |
|---|---|
| `App.razor` | Add Kendo stylesheet, add Kendo JS script, add timezone-interop.js |
| `MainLayout.razor` | Restyle page layout (sidebar background → Tetra Pak Blue) |
| `NavMenu.razor` | Replace Counter/Weather/Home links with "New Entry" and "View Records" |
| `app.css` | Add Tetra Pak color overrides, clean up template defaults |

### NuGet packages to add

| Package | Why |
|---|---|
| `Telerik.UI.for.Blazor` | Kendo UI components (TelerikGrid, TelerikForm, etc.) |

Register in `Program.cs`:
```csharp
builder.Services.AddTelerikBlazor();
```

Add to `_Imports.razor`:
```csharp
@using Telerik.Blazor
@using Telerik.Blazor.Components
```
