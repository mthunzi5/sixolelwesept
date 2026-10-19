using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core Code-First context. The Fluent API below reproduces docs/raceday_schema.sql:
/// same table and column names, column types, unique keys, CHECK constraints and defaults.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // No cascading deletes anywhere, same as the Part 1 script: removing a parent row
        // that still has children is blocked, and the controllers return 409 Conflict instead.

        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            e.HasKey(u => u.UserId);
            e.Property(u => u.UserId).HasColumnName("UserID");
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(u => u.Email).IsUnique().HasDatabaseName("UQ_Users_Email");
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasColumnType("varchar(20)");
            e.Property(u => u.PhoneNumber).HasMaxLength(20);
            e.Property(u => u.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.ToTable("Events", t =>
            {
                t.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Scheduled', 'Ongoing', 'Completed', 'Cancelled')");
                t.HasCheckConstraint("CK_Events_EventType", "[EventType] IN ('Run', 'Walk', 'Cycle')");
                t.HasCheckConstraint("CK_Events_Distance", "[DistanceKm] > 0");
            });
            e.HasKey(ev => ev.EventId);
            e.Property(ev => ev.EventId).HasColumnName("EventID");
            e.Property(ev => ev.OrganiserId).HasColumnName("OrganiserID");
            e.Property(ev => ev.EventName).HasMaxLength(150).IsRequired();
            e.Property(ev => ev.EventDate).HasColumnType("date");
            e.Property(ev => ev.Location).HasMaxLength(150).IsRequired();
            e.Property(ev => ev.Description).HasMaxLength(500);
            e.Property(ev => ev.DistanceKm).HasColumnType("decimal(5,2)");
            e.Property(ev => ev.EventType).HasConversion<string>().HasColumnType("varchar(10)");
            e.Property(ev => ev.Status).HasConversion<string>().HasColumnType("varchar(20)");
            e.Property(ev => ev.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");

            e.HasOne(ev => ev.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(ev => ev.OrganiserId)
                .HasConstraintName("FK_Events_Organiser")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories", t =>
            {
                t.HasCheckConstraint("CK_Categories_Distance", "[DistanceKm] > 0");
                t.HasCheckConstraint("CK_Categories_MaxParticipants", "[MaxParticipants] > 0");
                t.HasCheckConstraint("CK_Categories_CategoryType", "[CategoryType] IN ('Distance', 'Age')");
            });
            e.HasKey(c => c.CategoryId);
            e.Property(c => c.CategoryId).HasColumnName("CategoryID");
            e.Property(c => c.EventId).HasColumnName("EventID");
            e.Property(c => c.CategoryName).HasColumnType("varchar(50)").IsRequired();
            e.Property(c => c.CategoryType).HasConversion<string>().HasColumnType("varchar(10)");
            e.Property(c => c.DistanceKm).HasColumnType("decimal(5,2)");
            e.Property(c => c.EntryFee).HasColumnType("decimal(8,2)");
            e.HasIndex(c => new { c.EventId, c.CategoryName }).IsUnique().HasDatabaseName("UQ_Categories_EventName");

            e.HasOne(c => c.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(c => c.EventId)
                .HasConstraintName("FK_Categories_Event")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolments", t => t.HasCheckConstraint("CK_Enrolments_Status", "[Status] IN ('Pending', 'Confirmed', 'Withdrawn')"));
            e.HasKey(en => en.EnrolmentId);
            e.Property(en => en.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(en => en.ParticipantId).HasColumnName("ParticipantID");
            e.Property(en => en.CategoryId).HasColumnName("CategoryID");
            e.Property(en => en.BibNumber).HasColumnType("varchar(10)").IsRequired();
            e.HasIndex(en => en.BibNumber).IsUnique().HasDatabaseName("UQ_Enrolments_Bib");
            e.HasIndex(en => new { en.ParticipantId, en.CategoryId }).IsUnique().HasDatabaseName("UQ_Enrolments_Participant_Category");
            e.Property(en => en.EnrolmentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(en => en.Status).HasConversion<string>().HasColumnType("varchar(20)");

            e.HasOne(en => en.Participant).WithMany(u => u.Enrolments)
                .HasForeignKey(en => en.ParticipantId)
                .HasConstraintName("FK_Enrolments_Participant")
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(en => en.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(en => en.CategoryId)
                .HasConstraintName("FK_Enrolments_Category")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Result>(e =>
        {
            e.ToTable("Results", t => t.HasCheckConstraint("CK_Results_Status", "[Status] IN ('Finished', 'DNF', 'DSQ')"));
            e.HasKey(r => r.ResultId);
            e.Property(r => r.ResultId).HasColumnName("ResultID");
            e.Property(r => r.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(r => r.Status).HasConversion<string>().HasColumnType("varchar(20)");
            e.HasIndex(r => r.EnrolmentId).IsUnique().HasDatabaseName("UQ_Results_Enrolment");

            e.HasOne(r => r.Enrolment).WithOne(en => en.Result)
                .HasForeignKey<Result>(r => r.EnrolmentId)
                .HasConstraintName("FK_Results_Enrolment")
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments", t =>
            {
                t.HasCheckConstraint("CK_Payments_Method", "[PaymentMethod] IN ('Card', 'EFT', 'Cash')");
                t.HasCheckConstraint("CK_Payments_Status", "[Status] IN ('Pending', 'Paid', 'Refunded')");
            });
            e.HasKey(p => p.PaymentId);
            e.Property(p => p.PaymentId).HasColumnName("PaymentID");
            e.Property(p => p.EnrolmentId).HasColumnName("EnrolmentID");
            e.Property(p => p.Amount).HasColumnType("decimal(8,2)");
            e.Property(p => p.PaymentMethod).HasConversion<string>().HasColumnType("varchar(30)");
            e.Property(p => p.PaymentDate).HasColumnType("datetime").HasDefaultValueSql("GETDATE()");
            e.Property(p => p.Status).HasConversion<string>().HasColumnType("varchar(20)");
            e.HasIndex(p => p.EnrolmentId).IsUnique().HasDatabaseName("UQ_Payments_Enrolment");

            e.HasOne(p => p.Enrolment).WithOne(en => en.Payment)
                .HasForeignKey<Payment>(p => p.EnrolmentId)
                .HasConstraintName("FK_Payments_Enrolment")
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
