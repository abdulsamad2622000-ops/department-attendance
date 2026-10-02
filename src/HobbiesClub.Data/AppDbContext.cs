using HobbiesClub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<MemberClub> MemberClubs => Set<MemberClub>();
    public DbSet<BiometricTemplate> BiometricTemplates => Set<BiometricTemplate>();
    public DbSet<AttendanceLog> AttendanceLogs => Set<AttendanceLog>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Club>(e =>
        {
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Club { Id = 1, Name = "AI Club", IsActive = true, CreatedAt = new DateTime(2026, 1, 1) },
                new Club { Id = 2, Name = "Hobbies Club", IsActive = true, CreatedAt = new DateTime(2026, 1, 1) });
        });

        b.Entity<Member>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.Phone).IsRequired().HasMaxLength(20);
            e.HasIndex(x => x.Phone).IsUnique();
            e.HasIndex(x => x.MemberCode).IsUnique();
        });

        b.Entity<MemberClub>(e =>
        {
            e.HasKey(x => new { x.MemberId, x.ClubId });
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<BiometricTemplate>(e =>
        {
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.MemberId);
        });

        b.Entity<AttendanceLog>(e =>
        {
            e.HasOne(x => x.Member).WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.MemberId, x.ClubId, x.Timestamp });
            e.HasIndex(x => new { x.ClubId, x.Timestamp });
        });

        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.Username).IsRequired().HasMaxLength(50);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.PasswordSalt).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
        });

        b.Entity<BackupHistory>(e =>
        {
            e.HasIndex(x => x.CreatedAt);
        });

        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Timestamp);
        });
    }
}
