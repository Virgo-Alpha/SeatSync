namespace SeatSync.Web.Models.Api;

public sealed record SeatApiModel(
    Guid Id,
    string Section,
    string Row,
    string Number,
    decimal? X,
    decimal? Y,
    int State);
