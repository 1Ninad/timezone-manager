// DeliveryRecordDisplay.cs
// The model the Records page grid binds to.
//
// After loading raw rows from the database (which store UTC timestamps), the Records page
// converts each UTC value to a formatted local-time string via the browser's JavaScript
// and stores the result in LocalTimestamp. The grid never sees raw UTC DateTime values —
// it always gets pre-formatted, viewer-local strings ready to display.

namespace timezone_manager.Models;

public class DeliveryRecordDisplay
{
    public string DeliveryNumber { get; set; } = "";
    public string Plant { get; set; } = "";
    public string Material { get; set; } = "";

    // Formatted in the viewer's local time by the browser's JS, e.g. "Jul 2, 2025, 02:30:00 PM".
    // This reflects whoever is currently viewing the page, not the original submitter's time.
    public string LocalTimestamp { get; set; } = "";

    // IANA timezone ID of who submitted the record, e.g. "Asia/Tokyo" or "America/Sao_Paulo".
    // Lets investigators see where an entry originated even if Plant is empty or removed.
    public string SubmitterTimezone { get; set; } = "";
}
