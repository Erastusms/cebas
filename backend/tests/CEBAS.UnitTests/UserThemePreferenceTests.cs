using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using CEBAS.Api.Features.Users.GetCurrentUser;
using CEBAS.Api.Features.Users.UpdateProfile;
using CEBAS.Application.Contracts.Users;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.UnitTests;

public class UserThemePreferenceTests
{
    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public void NewUser_ShouldDefaultThemePreference_ToSystem()
    {
        var user = User.Create("alice", "alice@example.com", "hash123", "Alice");
        user.ThemePreference.Should().Be(ThemePreference.SYSTEM);
    }

    [Fact]
    public async Task GetCurrentUser_ShouldReturnThemePreference()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("bob", "bob@example.com", "hash123", "Bob");
        user.UpdateThemePreference(ThemePreference.DARK);
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new GetCurrentUserQueryHandler(dbContext);
        var result = await handler.Handle(new GetCurrentUserQuery(user.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result.ThemePreference.Should().Be("DARK");
    }

    [Theory]
    [InlineData("LIGHT", ThemePreference.LIGHT)]
    [InlineData("DARK", ThemePreference.DARK)]
    [InlineData("SYSTEM", ThemePreference.SYSTEM)]
    [InlineData("light", ThemePreference.LIGHT)]
    [InlineData("dark", ThemePreference.DARK)]
    [InlineData("system", ThemePreference.SYSTEM)]
    public async Task UpdateProfile_WithValidThemePreference_ShouldUpdateAndPersist(string themeInput, ThemePreference expected)
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("carol", "carol@example.com", "hash123", "Carol");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(dbContext, NullLogger<UpdateProfileCommandHandler>.Instance);
        var command = new UpdateProfileCommand(
            UserId: user.Id,
            DisplayName: null,
            Bio: null,
            BannerUrl: null,
            ThemePreference: themeInput
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.ThemePreference.Should().Be(expected.ToString().ToUpperInvariant());
        var persisted = await dbContext.Users.FindAsync(user.Id);
        persisted!.ThemePreference.Should().Be(expected);
    }

    [Fact]
    public async Task UpdateProfile_UpdatingSameValue_ShouldBeIdempotent()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("david", "david@example.com", "hash123", "David");
        user.UpdateThemePreference(ThemePreference.DARK);
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var initialUpdatedAt = user.UpdatedAt;

        var handler = new UpdateProfileCommandHandler(dbContext, NullLogger<UpdateProfileCommandHandler>.Instance);
        var command = new UpdateProfileCommand(
            UserId: user.Id,
            DisplayName: null,
            Bio: null,
            BannerUrl: null,
            ThemePreference: "DARK"
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.ThemePreference.Should().Be("DARK");

        var persisted = await dbContext.Users.FindAsync(user.Id);
        persisted!.ThemePreference.Should().Be(ThemePreference.DARK);
        persisted.UpdatedAt.Should().Be(initialUpdatedAt);
    }

    [Fact]
    public async Task UpdateProfile_WithOnlyTheme_ShouldPreserveExistingDisplayNameAndBio()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("eve", "eve@example.com", "hash123", "Eve Original", "Original Bio");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(dbContext, NullLogger<UpdateProfileCommandHandler>.Instance);
        var command = new UpdateProfileCommand(
            UserId: user.Id,
            DisplayName: null,
            Bio: null,
            BannerUrl: null,
            ThemePreference: "DARK"
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.DisplayName.Should().Be("Eve Original");
        result.Bio.Should().Be("Original Bio");
        result.ThemePreference.Should().Be("DARK");

        var persisted = await dbContext.Users.FindAsync(user.Id);
        persisted!.DisplayName.Should().Be("Eve Original");
        persisted.Bio.Should().Be("Original Bio");
        persisted.ThemePreference.Should().Be(ThemePreference.DARK);
    }

    [Fact]
    public async Task UpdateProfile_ExistingProfileUpdateFunctionality_RemainsIntact()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("frank", "frank@example.com", "hash123", "Frank Old", "Bio Old");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateProfileCommandHandler(dbContext, NullLogger<UpdateProfileCommandHandler>.Instance);
        var command = new UpdateProfileCommand(
            UserId: user.Id,
            DisplayName: "Frank New",
            Bio: "Bio New"
        );

        var result = await handler.Handle(command, CancellationToken.None);
        result.DisplayName.Should().Be("Frank New");
        result.Bio.Should().Be("Bio New");
        result.ThemePreference.Should().Be("SYSTEM");

        var persisted = await dbContext.Users.FindAsync(user.Id);
        persisted!.DisplayName.Should().Be("Frank New");
        persisted.Bio.Should().Be("Bio New");
        persisted.ThemePreference.Should().Be(ThemePreference.SYSTEM);
    }

    [Theory]
    [InlineData("light-mode")]
    [InlineData("darkmode")]
    [InlineData("foo")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("")]
    public void UpdateProfileCommandValidator_InvalidThemeValues_ShouldFailValidation(string invalidTheme)
    {
        var validator = new UpdateProfileCommandValidator();
        var command = new UpdateProfileCommand(
            UserId: Guid.NewGuid(),
            ThemePreference: invalidTheme
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ThemePreference" && e.ErrorMessage.Contains("Theme preference must be one of"));
    }

    [Theory]
    [InlineData("LIGHT")]
    [InlineData("DARK")]
    [InlineData("SYSTEM")]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("system")]
    public void UpdateProfileCommandValidator_ValidThemeValues_ShouldPassValidation(string validTheme)
    {
        var validator = new UpdateProfileCommandValidator();
        var command = new UpdateProfileCommand(
            UserId: Guid.NewGuid(),
            ThemePreference: validTheme
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("light-mode")]
    [InlineData("darkmode")]
    [InlineData("foo")]
    [InlineData("TRUE")]
    [InlineData("1")]
    public void UpdateProfileRequestValidator_InvalidThemeValues_ShouldFailValidation(string invalidTheme)
    {
        var validator = new UpdateProfileRequestValidator();
        var request = new UpdateProfileRequest(ThemePreference: invalidTheme);

        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ThemePreference");
    }

    [Fact]
    public void UpdateProfileRequestValidator_EmptyRequest_ShouldFailValidation()
    {
        var validator = new UpdateProfileRequestValidator();
        var request = new UpdateProfileRequest();

        var result = validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("At least one profile field must be provided"));
    }
}
