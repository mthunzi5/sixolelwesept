namespace RaceDay.Api.Models;

// Every enum is stored in SQL Server as its string name (see RaceDayDbContext),
// so the columns hold exactly the values allowed by the Part 1 CHECK constraints.

/// <summary>The two account types in RaceDay.</summary>
public enum UserRole
{
    Organiser,
    Participant
}

/// <summary>Kind of event being held.</summary>
public enum EventType
{
    Run,
    Walk,
    Cycle
}

/// <summary>Lifecycle state of an event.</summary>
public enum EventStatus
{
    Scheduled,
    Ongoing,
    Completed,
    Cancelled
}

/// <summary>Whether a category splits entrants by distance (e.g. 10km) or by age group (e.g. Under 20).</summary>
public enum CategoryType
{
    Distance,
    Age
}

/// <summary>State of a participant's entry into a category.</summary>
public enum EnrolmentStatus
{
    Pending,
    Confirmed,
    Withdrawn
}

/// <summary>Outcome recorded for a participant after the race.</summary>
public enum ResultStatus
{
    Finished,
    DNF,
    DSQ
}

/// <summary>How an entry fee was paid.</summary>
public enum PaymentMethod
{
    Card,
    EFT,
    Cash
}

/// <summary>State of an entry fee payment.</summary>
public enum PaymentStatus
{
    Pending,
    Paid,
    Refunded
}
