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
}

