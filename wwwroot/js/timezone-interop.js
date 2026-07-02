// timezone-interop.js
// JavaScript functions that Blazor calls to get timezone and time information from the user's browser.
//
// Blazor Server runs your C# code on the server, not in the browser. The server has no idea
// what time it is for the user in Stockholm or São Paulo. These JS functions are the only way
// to ask the browser for that information. Blazor calls them via IJSRuntime (see NewEntry.razor
// and Records.razor).
//
// All functions are grouped under window.timezoneInterop to keep the global namespace clean.

window.timezoneInterop = {

    // Returns the current moment as a UTC ISO 8601 string, e.g. "2025-07-02T10:30:00.000Z".
    // JavaScript's Date always represents a universal moment in time. .toISOString() always
    // formats it as UTC regardless of the user's local timezone — no conversion needed on the C# side.
    getCurrentUtcIso: () => new Date().toISOString(),

    // Returns the user's IANA timezone identifier from their browser's OS settings.
    // Examples: "America/Sao_Paulo", "Asia/Tokyo", "Europe/Stockholm".
    // IANA IDs are DST-aware — "America/New_York" automatically covers EST and EDT.
    getIanaTimezoneId: () => Intl.DateTimeFormat().resolvedOptions().timeZone,

    // Converts an array of UTC ISO strings to formatted local-time strings in one call.
    // Doing this in a single JS call (instead of one call per row) avoids the overhead of
    // many round-trips over the Blazor Server SignalR connection.
    // The browser uses its own locale and timezone settings to format each value.
    convertUtcArrayToLocal: (utcStrings) =>
        utcStrings.map(s => new Date(s).toLocaleString('default', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit',
            hour12: true
        }))
};
