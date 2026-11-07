namespace InternshipPortal.Web.Models.ViewModels;

public class JobFilterViewModel
{
    public string? Keyword { get; set; }

    public string? Major { get; set; }

    public string? Location { get; set; }

    public IEnumerable<JobPost> Jobs { get; set; } = Enumerable.Empty<JobPost>();

    public List<string> AvailableMajors { get; set; } = new();

    public List<string> AvailableLocations { get; set; } = new();
}

