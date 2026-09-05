using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing an authenticated user account and profile.
/// </summary>
public class User : Entity
{
    public string Username { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public Guid? AvatarMediaId { get; private set; }
    public string? BannerUrl { get; private set; }
    public Guid? BannerMediaId { get; private set; }
    public UserRole Role { get; private set; } = UserRole.User;
    public bool IsVerified { get; private set; } = false;
    public bool IsSuspended { get; private set; } = false;
    public DateTimeOffset? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }


    // Navigation properties
    public ICollection<Session> Sessions { get; private set; } = new List<Session>();
    public Media? AvatarMedia { get; private set; }
    public Media? BannerMedia { get; private set; }

    // EF Core parameterless constructor
    protected User() { }

    public static User Create(
        string username,
        string email,
        string passwordHash,
        string displayName,
        string? bio = null,
        string? avatarUrl = null,
        UserRole role = UserRole.User)
    {
        if (!IdentityNormalizers.IsValidUsername(username))
        {
            throw new ValidationException("Username", "Username must be 3-30 alphanumeric characters or underscores.");
        }

        if (!IdentityNormalizers.IsValidEmail(email))
        {
            throw new ValidationException("Email", "Email address is invalid.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ValidationException("Password", "Password hash cannot be empty.");
        }

        var trimmedDisplayName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedDisplayName) || trimmedDisplayName.Length > 50)
        {
            throw new ValidationException("DisplayName", "Display name must be between 1 and 50 characters.");
        }

        var trimmedBio = bio?.Trim();
        if (trimmedBio != null && trimmedBio.Length > 160)
        {
            throw new ValidationException("Bio", "Biography cannot exceed 160 characters.");
        }

        var user = new User
        {
            Id = Uuid7.New(),
            Username = username.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            DisplayName = trimmedDisplayName,
            Bio = string.IsNullOrEmpty(trimmedBio) ? null : trimmedBio,
            AvatarUrl = avatarUrl?.Trim(),
            Role = role,
            IsVerified = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        user.AddDomainEvent(new Events.UserRegisteredDomainEvent(user.Id, user.Username, user.Email, user.CreatedAt));
        return user;
    }

    public void UpdateProfile(string displayName, string? bio, string? avatarUrl = null, string? bannerUrl = null)
    {
        var trimmedDisplayName = displayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedDisplayName) || trimmedDisplayName.Length > 50)
        {
            throw new ValidationException("DisplayName", "Display name must be between 1 and 50 characters.");
        }

        var trimmedBio = bio?.Trim();
        if (trimmedBio != null && trimmedBio.Length > 160)
        {
            throw new ValidationException("Bio", "Biography cannot exceed 160 characters.");
        }

        DisplayName = trimmedDisplayName;
        Bio = string.IsNullOrEmpty(trimmedBio) ? null : trimmedBio;

        if (avatarUrl != null)
        {
            AvatarUrl = avatarUrl.Trim();
        }

        if (bannerUrl != null)
        {
            BannerUrl = bannerUrl.Trim();
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new Events.ProfileUpdatedDomainEvent(Id, DisplayName, Bio, UpdatedAt.Value));
    }

    public void UpdateAvatar(Media media)
    {
        if (media == null)
        {
            throw new ValidationException("Media", "Media cannot be null.");
        }

        if (media.OwnerUserId != Id)
        {
            throw new ForbiddenException("Cannot assign another user's media as your avatar.");
        }

        if (media.Status != MediaStatus.Ready)
        {
            throw new ValidationException("Media", $"Cannot set avatar with media in status '{media.Status}'. Media must be in 'Ready' status.");
        }

        if (!Media.AllowedImageMimeTypes.Contains(media.MimeType))
        {
            throw new ValidationException("Media", $"Unsupported avatar image format '{media.MimeType}'.");
        }

        AvatarMediaId = media.Id;
        AvatarUrl = $"/api/v1/media/{media.Id}";
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new Events.AvatarUpdatedDomainEvent(Id, media.Id, UpdatedAt.Value));
    }

    public void UpdateBanner(Media media)
    {
        if (media == null)
        {
            throw new ValidationException("Media", "Media cannot be null.");
        }

        if (media.OwnerUserId != Id)
        {
            throw new ForbiddenException("Cannot assign another user's media as your banner.");
        }

        if (media.Status != MediaStatus.Ready)
        {
            throw new ValidationException("Media", $"Cannot set banner with media in status '{media.Status}'. Media must be in 'Ready' status.");
        }

        if (!Media.AllowedImageMimeTypes.Contains(media.MimeType))
        {
            throw new ValidationException("Media", $"Unsupported banner image format '{media.MimeType}'.");
        }

        BannerMediaId = media.Id;
        BannerUrl = $"/api/v1/media/{media.Id}";
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new Events.BannerUpdatedDomainEvent(Id, media.Id, BannerUrl, UpdatedAt.Value));
    }

    public void SetBannerUrl(string? bannerUrl)
    {
        BannerMediaId = null;
        BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new Events.BannerUpdatedDomainEvent(Id, null, BannerUrl, UpdatedAt.Value));
    }

    public void Suspend(string reason)
    {
        if (IsSuspended)
        {
            return;
        }

        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? "Account suspended by moderator." : reason.Trim();
        IsSuspended = true;
        SuspendedAt = DateTimeOffset.UtcNow;
        SuspensionReason = trimmedReason;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new Events.UserSuspendedDomainEvent(Id, SuspensionReason, SuspendedAt.Value));
    }

    public void Reinstate()
    {
        if (!IsSuspended)
        {
            return;
        }

        IsSuspended = false;
        SuspendedAt = null;
        SuspensionReason = null;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new Events.UserReinstatedDomainEvent(Id, UpdatedAt.Value));
    }
}

