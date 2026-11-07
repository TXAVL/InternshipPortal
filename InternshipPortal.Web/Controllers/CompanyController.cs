using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Data;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Controllers;

public class CompanyController : Controller
{
    private readonly ApplicationDbContext _context;

    public CompanyController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Company
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.Companies
            .Include(c => c.JobPosts)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.CompanyName.ToLower().Contains(term) ||
                                     (c.Address != null && c.Address.ToLower().Contains(term)) ||
                                     (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(term)));
        }

        var companies = await query
            .OrderByDescending(c => c.Id)
            .ToListAsync();

        ViewBag.SearchTerm = search;
        return View(companies);
    }

    // GET: /Company/Detail/5
    public async Task<IActionResult> Detail(int id)
    {
        var company = await _context.Companies
            .Include(c => c.JobPosts)
                .ThenInclude(j => j.Applications)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (company == null)
        {
            return NotFound();
        }

        return View(company);
    }

    // GET: /Company/Create
    public IActionResult Create()
    {
        return View(new Company());
    }

    // POST: /Company/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Company company)
    {
        ModelState.Remove("JobPosts");

        if (ModelState.IsValid)
        {
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Thêm doanh nghiệp '{company.CompanyName}' thành công!";
            return RedirectToAction(nameof(Detail), new { id = company.Id });
        }

        return View(company);
    }

    // GET: /Company/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
        {
            return NotFound();
        }

        return View(company);
    }

    // POST: /Company/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Company company)
    {
        if (id != company.Id)
        {
            return BadRequest();
        }

        ModelState.Remove("JobPosts");

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(company);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Cập nhật doanh nghiệp '{company.CompanyName}' thành công!";
                return RedirectToAction(nameof(Detail), new { id = company.Id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Companies.AnyAsync(c => c.Id == id))
                {
                    return NotFound();
                }
                throw;
            }
        }

        return View(company);
    }

    // POST: /Company/ToggleApproval/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleApproval(int id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
        {
            return NotFound();
        }

        company.IsApproved = !company.IsApproved;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = company.IsApproved
            ? $"Đã phê duyệt hợp tác với doanh nghiệp '{company.CompanyName}'!"
            : $"Đã tạm ngừng phê duyệt doanh nghiệp '{company.CompanyName}'!";

        return RedirectToAction(nameof(Index));
    }
}
