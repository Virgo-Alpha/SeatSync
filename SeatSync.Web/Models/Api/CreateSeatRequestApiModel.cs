namespace SeatSync.Web.Models.Api;

public sealed record CreateSeatRequestApiModel(
    string Section,
    string Row,
    string Number,
    decimal X,
    decimal Y);
