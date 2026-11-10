using System.Globalization;
using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Helpers;

/// <summary>Formatting helpers shared by every view, so dates, money, times and badges look the same everywhere.</summary>
public static class Display
{
    public static string Date(DateOnly date) => date.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateOnly? date) => date is DateOnly d ? Date(d) : "-";

    public static string ShortDate(DateOnly date) => date.ToString("d MMM", CultureInfo.InvariantCulture);

    public static string Money(decimal amount) => amount == 0 ? "Free" : "R " + amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string Km(decimal km) => km.ToString("0.##", CultureInfo.InvariantCulture) + " km";

    public static string FinishTime(int? seconds)
    {
        if (seconds is not int s) return "-";
        var t = TimeSpan.FromSeconds(s);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }

    /// <summary>1 → 1st, 2 → 2nd, 3 → 3rd, 11 → 11th, 22 → 22nd, 47 → 47th.</summary>
    public static string Ordinal(int n)
    {
        var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        return n.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    /// <summary>"47th of 312 finishers", or the DNF/DSQ status.</summary>
    public static string Placing(ResultDto r) => r.Status switch
    {
        ResultStatus.Finished when r.Position is int p => $"{Ordinal(p)} of {r.FinishersInCategory} finisher{(r.FinishersInCategory == 1 ? "" : "s")}",
        ResultStatus.DNF => "Did not finish",
        ResultStatus.DSQ => "Disqualified",
        _ => "-"
    };

    /// <summary>
    /// Image URLs from the API point at /api/images/...; the browser loads them through this site's
    /// /media/... route, which fetches them from the API. The browser never talks to Azure or the API directly.
    /// </summary>
    public static string? ImageSrc(string? apiImageUrl) =>
        string.IsNullOrWhiteSpace(apiImageUrl) ? null : apiImageUrl.Replace("/api/images/", "/media/", StringComparison.Ordinal);

    public static string EventTypeIcon(EventType type) => type switch
    {
        EventType.Run => "bi-lightning-charge-fill",
        EventType.Walk => "bi-person-walking",
        EventType.Cycle => "bi-bicycle",
        _ => "bi-flag-fill"
    };

    public static string Initials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
        };
    }

    // Colour-coded status badges. Each maps to a .rd-badge-* class in site.css.
    public static string BadgeClass(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Confirmed => "rd-badge-success",
        EnrolmentStatus.Pending => "rd-badge-warning",
        _ => "rd-badge-muted"
    };

    public static string BadgeClass(EventStatus status) => status switch
    {
        EventStatus.Scheduled => "rd-badge-info",
        EventStatus.Ongoing => "rd-badge-brand",
        EventStatus.Completed => "rd-badge-dark",
        _ => "rd-badge-muted"
    };

    public static string BadgeClass(ResultStatus status) => status switch
    {
        ResultStatus.Finished => "rd-badge-success",
        ResultStatus.DNF => "rd-badge-warning",
        _ => "rd-badge-danger"
    };

    public static string BadgeClass(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid => "rd-badge-success",
        PaymentStatus.Pending => "rd-badge-warning",
        _ => "rd-badge-muted"
    };
}
