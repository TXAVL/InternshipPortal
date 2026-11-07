using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;

namespace InternshipPortal.Web.Controllers;

public class JobController : Controller
{
    private readonly ApplicationDbContext _context;

    public JobController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Job or /
    public async Task<IActionResult> Index()
    {
        var jobs = await _context.JobPosts
            .Include(j => j.Company)
            .ToListAsync();

        return View(jobs);
    }

    // GET: /Job/Detail/5
    public async Task<IActionResult> Detail(int id)
    {
        var job = await _context.JobPosts
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (job == null)
        {
            return NotFound();
        }

        return View(job);
    }
}

