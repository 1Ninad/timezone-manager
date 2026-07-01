# Project Component Breakdown — Timezone Manager

## Components (in build order)

1. **SQL Server database schema** — SQL script defining the table that stores DeliveryNumber, Plant, Material, and a UTC timestamp column.

2. **EF Core DbContext & entity model** — Database-first scaffold generating the C# entity class and DbContext wired to the SQL Server table.

3. **Data access service** — C# service class with methods to insert a new record and retrieve all records from the database.

4. **JavaScript timezone interop** — JS functions exposed to Blazor via IJSRuntime to read the user's browser timezone identifier and current local datetime.

5. **Input form page** — Blazor page with Kendo UI form fields for Delivery Number, Plant, and Material; auto-captures local time via JS interop and saves the record as UTC.

6. **Records display page** — Blazor page with a Kendo UI grid listing all saved records, converting stored UTC timestamps to the current viewer's local time via JS interop.

7. **App layout & Tetra Pak theme** — Updated MainLayout, NavMenu, and global CSS applying Tetra Pak brand colors and replacing the default template styling.

---