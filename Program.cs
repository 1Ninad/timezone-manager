// Program.cs
// Application startup — registers all services and configures the HTTP pipeline.
// This is the first file that runs when the app starts.

using Microsoft.EntityFrameworkCore;
using timezone_manager.Components;
using timezone_manager.Models;
using timezone_manager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Telerik (Kendo UI) Blazor services.
// Required for TelerikGrid, TelerikTextBox, TelerikButton, etc. to work.
builder.Services.AddTelerikBlazor();

// Register the EF Core DbContext factory for Blazor Server.
// We use a factory (not a plain DbContext) because Blazor Server can have many users
// connected simultaneously. The factory creates a fresh DbContext per operation,
// which prevents users from accidentally sharing database state.
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register DeliveryService so pages can inject it with @inject DeliveryService.
// Scoped means one instance per user connection — appropriate for Blazor Server.
builder.Services.AddScoped<DeliveryService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
