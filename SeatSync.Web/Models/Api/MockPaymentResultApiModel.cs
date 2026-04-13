namespace SeatSync.Web.Models.Api;

public sealed record MockPaymentResultApiModel(
    bool IsSuccess,
    string? State,
    string? Message
);
