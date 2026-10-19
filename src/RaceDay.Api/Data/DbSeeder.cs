using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// Loads the same sample data as the Part 1 SQL script into an empty database, but with real
/// BCrypt hashes so the seeded accounts can actually log in. Every seeded account uses
/// the password <see cref="SeedPassword"/>.
/// </summary>
public static class DbSeeder
{
    public const string SeedPassword = "Password123!";

    public static void Seed(RaceDayDbContext db)
    {
        if (db.Users.Any())
        {
            return;
        }

        var hash = BCrypt.Net.BCrypt.HashPassword(SeedPassword);

        var naledi = new User { FullName = "Naledi Khumalo", Email = "naledi.khumalo@raceday.co.za", PasswordHash = hash, Role = UserRole.Organiser, PhoneNumber = "0821234501" };
        var sipho = new User { FullName = "Sipho Dlamini", Email = "sipho.dlamini@raceday.co.za", PasswordHash = hash, Role = UserRole.Organiser, PhoneNumber = "0821234502" };
        var amahle = new User { FullName = "Amahle Nkosi", Email = "amahle.nkosi@example.com", PasswordHash = hash, Role = UserRole.Participant, PhoneNumber = "0731234601" };
        var jason = new User { FullName = "Jason van Wyk", Email = "jason.vanwyk@example.com", PasswordHash = hash, Role = UserRole.Participant, PhoneNumber = "0731234602" };
        var palesa = new User { FullName = "Palesa Mokoena", Email = "palesa.mokoena@example.com", PasswordHash = hash, Role = UserRole.Participant, PhoneNumber = "0731234603" };
        var ryan = new User { FullName = "Ryan Govender", Email = "ryan.govender@example.com", PasswordHash = hash, Role = UserRole.Participant, PhoneNumber = "0731234604" };

        var jhb = new Event
        {
            Organiser = naledi, EventName = "Johannesburg City Marathon", EventDate = new DateOnly(2026, 10, 18),
            Location = "Johannesburg, Gauteng", DistanceKm = 21.10m, EventType = EventType.Run, Status = EventStatus.Completed,
            Description = "Annual road race through the Johannesburg CBD with 5km, 10km and 21km categories."
        };
        var dbn = new Event
        {
            Organiser = naledi, EventName = "Durban Beachfront Fun Run", EventDate = new DateOnly(2026, 11, 8),
            Location = "Durban, KwaZulu-Natal", DistanceKm = 8.00m, EventType = EventType.Walk,
            Description = "Family-friendly fun run along the Durban beachfront promenade."
        };
        var cpt = new Event
        {
            Organiser = sipho, EventName = "Cape Town Trail Challenge", EventDate = new DateOnly(2026, 12, 6),
            Location = "Cape Town, Western Cape", DistanceKm = 25.00m, EventType = EventType.Run,
            Description = "Off-road trail event on the slopes of Table Mountain."
        };

        var jhb5 = new Category { Event = jhb, CategoryName = "5km Fun Run", DistanceKm = 5.00m, EntryFee = 100.00m, MaxParticipants = 500 };
        var jhb10 = new Category { Event = jhb, CategoryName = "10km Road Race", DistanceKm = 10.00m, EntryFee = 180.00m, MaxParticipants = 400 };
        var jhb21 = new Category { Event = jhb, CategoryName = "21km Half Marathon", DistanceKm = 21.10m, EntryFee = 280.00m, MaxParticipants = 300 };
        var dbn3 = new Category { Event = dbn, CategoryName = "3km Fun Run", DistanceKm = 3.00m, EntryFee = 60.00m, MaxParticipants = 800 };
        var dbn8 = new Category { Event = dbn, CategoryName = "8km Beach Run", DistanceKm = 8.00m, EntryFee = 120.00m, MaxParticipants = 350 };
        var cptU20 = new Category { Event = cpt, CategoryName = "Under 20", CategoryType = CategoryType.Age, MinAge = 0, MaxAge = 19, DistanceKm = 12.00m, EntryFee = 150.00m, MaxParticipants = 100 };
        var cpt12 = new Category { Event = cpt, CategoryName = "12km Trail", DistanceKm = 12.00m, EntryFee = 220.00m, MaxParticipants = 200 };
        var cpt25 = new Category { Event = cpt, CategoryName = "25km Trail", DistanceKm = 25.00m, EntryFee = 350.00m, MaxParticipants = 150 };

        var e1 = new Enrolment { Participant = amahle, Category = jhb10, BibNumber = "JHB-1001" };
        var e2 = new Enrolment { Participant = jason, Category = jhb21, BibNumber = "JHB-1002" };
        var e3 = new Enrolment { Participant = palesa, Category = jhb5, BibNumber = "JHB-1003", Status = EnrolmentStatus.Pending };
        var e4 = new Enrolment { Participant = amahle, Category = dbn8, BibNumber = "DBN-2001" };
        var e5 = new Enrolment { Participant = ryan, Category = dbn3, BibNumber = "DBN-2002" };

        db.AddRange(naledi, sipho, amahle, jason, palesa, ryan);
        db.AddRange(jhb, dbn, cpt);
        db.AddRange(jhb5, jhb10, jhb21, dbn3, dbn8, cptU20, cpt12, cpt25);
        db.AddRange(e1, e2, e3, e4, e5);

        db.AddRange(
            new Payment { Enrolment = e1, Amount = 180.00m, PaymentMethod = PaymentMethod.Card, Status = PaymentStatus.Paid },
            new Payment { Enrolment = e2, Amount = 280.00m, PaymentMethod = PaymentMethod.EFT, Status = PaymentStatus.Paid },
            new Payment { Enrolment = e4, Amount = 120.00m, PaymentMethod = PaymentMethod.Card, Status = PaymentStatus.Paid },
            new Payment { Enrolment = e5, Amount = 60.00m, PaymentMethod = PaymentMethod.Cash, Status = PaymentStatus.Paid });

        // The Johannesburg marathon has been run, so its finishers already have results.
        db.AddRange(
            new Result { Enrolment = e1, FinishTimeSeconds = 3125, Position = 1, Status = ResultStatus.Finished },
            new Result { Enrolment = e2, FinishTimeSeconds = 6540, Position = 1, Status = ResultStatus.Finished });

        db.SaveChanges();
    }
}
