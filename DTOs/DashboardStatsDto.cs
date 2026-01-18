namespace CivicService.DTOs;

public class DashboardStatsDto
{
    public int TotalRequests { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = [];
    public Dictionary<string, int> ByCategory { get; set; } = [];
    public List<DailyCountDto> RequestsOverTime { get; set; } = [];
    public double AverageResolutionHours { get; set; }
    public List<NeighborhoodCountDto> TopNeighborhoods { get; set; } = [];
}

public class DailyCountDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AddressCountDto
{
    public string Address { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class NeighborhoodCountDto
{
    public string Neighborhood { get; set; } = string.Empty;
    public int Count { get; set; }
}
