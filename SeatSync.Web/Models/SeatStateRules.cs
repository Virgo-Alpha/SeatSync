namespace SeatSync.Web.Models;

public static class SeatStateRules
{
    public static bool TryToggle(SeatViewModel seat)
    {
        if (!seat.IsInteractive)
        {
            return false;
        }

        seat.State = seat.State == SeatVisualState.Available
            ? SeatVisualState.Selected
            : SeatVisualState.Available;

        return true;
    }

    public static string ToCssClass(SeatVisualState state) => state switch
    {
        SeatVisualState.Available => "seat--available",
        SeatVisualState.Selected => "seat--selected",
        SeatVisualState.Held => "seat--held",
        SeatVisualState.Booked => "seat--booked",
        _ => "seat--available"
    };
}
