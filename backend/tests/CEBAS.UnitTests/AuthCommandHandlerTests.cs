using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using CEBAS.Api.Features.Auth.Login;
using CEBAS.Api.Features.Auth.Logout;
using CEBAS.Api.Features.Auth.Register;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.UnitTests;

public class AuthCommandHandlerTests
{
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ISessionTokenService _tokenService = Substitute.For<ISessionTokenService>();

    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Register_WithValidData_ShouldPersistUser_AndReturnCurrentUserResponse()
    {
        using var dbContext = CreateDbContext();
        _passwordHasher.Hash("Password123!").Returns("hashed_pwd_123");

        var handler = new RegisterCommandHandler(dbContext, _passwordHasher, NullLogger<RegisterCommandHandler>.Instance);
        var command = new RegisterCommand("newuser", "NEWUSER@EXAMPLE.COM", "Password123!", "New User");

        var response = await handler.Handle(command, CancellationToken.None);

        response.Should().NotBeNull();
        response.Username.Should().Be("newuser");
        response.Email.Should().Be("newuser@example.com");
        response.DisplayName.Should().Be("New User");

        var persisted = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == "newuser");
        persisted.Should().NotBeNull();
        persisted!.PasswordHash.Should().Be("hashed_pwd_123");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_CaseInsensitive_ShouldThrowConflictException()
    {
        using var dbContext = CreateDbContext();
        var existing = User.Create("existinguser", "existing@example.com", "hash", "Existing");
        await dbContext.Users.AddAsync(existing);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterCommandHandler(dbContext, _passwordHasher, NullLogger<RegisterCommandHandler>.Instance);
        var command = new RegisterCommand("ExistingUser", "unique@example.com", "Password123!");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already taken*");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_CaseInsensitive_ShouldThrowConflictException()
    {
        using var dbContext = CreateDbContext();
        var existing = User.Create("user1", "test@example.com", "hash", "User 1");
        await dbContext.Users.AddAsync(existing);
        await dbContext.SaveChangesAsync();

        var handler = new RegisterCommandHandler(dbContext, _passwordHasher, NullLogger<RegisterCommandHandler>.Instance);
        var command = new RegisterCommand("user2", "TEST@EXAMPLE.COM", "Password123!");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldCreateSession_AndReturnLoginResult()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("johndoe", "john@example.com", "valid_hash", "John Doe");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        _passwordHasher.Verify("Password123!", "valid_hash").Returns(true);
        _tokenService.GenerateRawToken().Returns("raw_random_token_64");
        _tokenService.ComputeTokenHash("raw_random_token_64").Returns("sha256_hash_64");

        var handler = new LoginCommandHandler(dbContext, _passwordHasher, _tokenService, NullLogger<LoginCommandHandler>.Instance);
        var command = new LoginCommand("johndoe", "Password123!", "Mozilla/5.0", "127.0.0.1");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.RawSessionToken.Should().Be("raw_random_token_64");
        result.User.Username.Should().Be("johndoe");

        var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.TokenHash == "sha256_hash_64");
        session.Should().NotBeNull();
        session!.UserId.Should().Be(user.Id);
        session.UserAgent.Should().Be("Mozilla/5.0");
        session.IpAddress.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldThrowUnauthorizedException()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("johndoe", "john@example.com", "valid_hash", "John Doe");
        await dbContext.Users.AddAsync(user);
        await dbContext.SaveChangesAsync();

        _passwordHasher.Verify("WrongPassword!", "valid_hash").Returns(false);

        var handler = new LoginCommandHandler(dbContext, _passwordHasher, _tokenService, NullLogger<LoginCommandHandler>.Instance);
        var command = new LoginCommand("johndoe", "WrongPassword!");

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>().WithMessage("Invalid username/email or password.");
    }

    [Fact]
    public async Task Logout_WithValidToken_ShouldRevokeSession()
    {
        using var dbContext = CreateDbContext();
        var user = User.Create("johndoe", "john@example.com", "hash", "John");
        var session = Session.Create(user.Id, "hashed_token_abc", DateTimeOffset.UtcNow.AddDays(7));
        await dbContext.Users.AddAsync(user);
        await dbContext.Sessions.AddAsync(session);
        await dbContext.SaveChangesAsync();

        _tokenService.ComputeTokenHash("raw_token_abc").Returns("hashed_token_abc");

        var handler = new LogoutCommandHandler(dbContext, _tokenService, NullLogger<LogoutCommandHandler>.Instance);
        var result = await handler.Handle(new LogoutCommand("raw_token_abc"), CancellationToken.None);

        result.Should().BeTrue();
        var updated = await dbContext.Sessions.FindAsync(session.Id);
        updated!.RevokedAt.Should().NotBeNull();
    }
}
