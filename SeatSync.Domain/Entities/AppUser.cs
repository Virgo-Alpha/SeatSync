using SeatSync.Domain.Enums;

namespace SeatSync.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AppUser() { }

    public AppUser(Guid id, string email, string displayName, string password, UserRole role)
    {
        if (id == Guid.Empty) throw new ArgumentException("User id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password is required.", nameof(password));

        Id = id;
        Email = email.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
        Password = password;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public bool VerifyPassword(string password) =>
        string.Equals(Password, password, StringComparison.Ordinal);
}
