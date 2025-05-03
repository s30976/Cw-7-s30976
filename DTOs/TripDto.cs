namespace Cw_7_s30976.DTOs;

public class TripDto
{
    public int IdTrip { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int MaxPeople { get; set; }
    public List<string> Countries { get; set; } = new();
}