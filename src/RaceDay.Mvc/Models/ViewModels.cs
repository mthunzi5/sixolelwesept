namespace RaceDay.Mvc.Models;

// Page models: the data a view needs, gathered from one or more API calls.

public class HomeViewModel
{
    public EventFilter Filter { get; set; } = new();
    public List<EventDto> Events { get; set; } = new();
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();

    /// <summary>The logged-in participant's active enrolment in this event, if any.</summary>
    public EnrolmentDto? MyEnrolment { get; set; }

    public bool IsOwner { get; set; }
}

public class LeaderboardViewModel
{
    public EventDto Event { get; set; } = new();
    public List<ResultDto> Results { get; set; } = new();
}

public class OrganiserDashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public List<EventDto> Upcoming => Events.Where(e => e.EventDate >= DateOnly.FromDateTime(DateTime.Today) && e.Status != EventStatus.Cancelled).OrderBy(e => e.EventDate).ToList();
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = new();
    public CategoryForm Form { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public int? CategoryId { get; set; }
}

public class ResultsEntryViewModel
{
    public EventDto Event { get; set; } = new();
    public List<EnrolmentDto> Entrants { get; set; } = new();
    public bool EventHasHappened => Event.EventDate <= DateOnly.FromDateTime(DateTime.Today);
}

public class ProfileViewModel
{
    public UserDto User { get; set; } = new();
    public ProfileForm Profile { get; set; } = new();
    public ChangePasswordForm Password { get; set; } = new();
}
