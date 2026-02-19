// Add Seat model with properties: 
// Id (Guid)
// EventId 
//     Section, Row, Number
// Optional: X, Y for map positioning

namespace SeatSync.Domain.Entities;

public sealed class Seat
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    
    public Guid EventId { get; private set; }
    public string Section { get; private set; } = default!;
    public string Row { get; private set; } = default!;
    public string Number { get; private set; } = default!;
    
    // Optional map coordinates
    public decimal? X { get; private set; }
    public decimal? Y { get; private set; }
    
    private Seat() { }

    public Seat(
        Guid eventId,
        string section,
        string row,
        string number,
        decimal? x = null,
        decimal? y = null
    )
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty", nameof(eventId));
        if (string.IsNullOrEmpty(section))
            throw new ArgumentException("Section cannot be empty", nameof(section));
        if (string.IsNullOrEmpty(row))
            throw new ArgumentException("Row cannot be empty", nameof(row));
        if (string.IsNullOrEmpty(number))
            throw new ArgumentException("Number cannot be empty", nameof(number));
        
        EventId = eventId;
        Section = section.Trim();
        Row = row.Trim();
        Number = number.Trim();
        X = x;
        Y = y;
    }

    public void SetPosition(decimal? x, decimal? y)
    {
        X = x;
        Y = y;
    }


}