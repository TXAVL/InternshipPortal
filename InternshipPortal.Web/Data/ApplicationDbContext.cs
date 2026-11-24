using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<JobPost> JobPosts { get; set; } = null!;
    public DbSet<StudentProfile> StudentProfiles { get; set; } = null!;
    public DbSet<Application> Applications { get; set; } = null!;
    public DbSet<WeeklyReport> WeeklyReports { get; set; } = null!;
    public DbSet<UserAccount> UserAccounts { get; set; } = null!;
    public DbSet<Evaluation> Evaluations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Evaluation>(entity =>
        {
            // Mỗi đơn thực tập chỉ có một bản đánh giá cuối kỳ
            entity.HasOne(e => e.Application)
                  .WithOne(a => a.Evaluation)
                  .HasForeignKey<Evaluation>(e => e.ApplicationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ApplicationId).IsUnique();
        });

        modelBuilder.Entity<WeeklyReport>(entity =>
        {
            entity.HasOne(r => r.Application)
                  .WithMany(a => a.WeeklyReports)
                  .HasForeignKey(r => r.ApplicationId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Mỗi đơn trúng tuyển chỉ có 1 báo cáo cho mỗi tuần
            entity.HasIndex(r => new { r.ApplicationId, r.WeekNumber }).IsUnique();
        });
    }
}

