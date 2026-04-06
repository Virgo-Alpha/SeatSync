namespace SeatSync.Web.Models;

public sealed class SeatViewModel
{
    public required string Section { get; init; }

    public required string Row { get; init; }

    public required string Number { get; init; }

    public SeatVisualState State { get; set; }

    public string SeatId => $"{Section}-{Row}-{Number}";

    public bool IsInteractive => State is SeatVisualState.Available or SeatVisualState.Selected;
}
