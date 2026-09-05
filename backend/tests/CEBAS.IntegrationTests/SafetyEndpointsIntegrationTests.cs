using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Application.Contracts.Reports;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class SafetyEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SafetyEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClientWithCookies()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.50.{Random.Shared.Next(1, 250)}.{Random.Shared.Next(1, 250)}");
        return client;
    }

    private async Task<(HttpClient client, Guid userId, string username)> RegisterAndLoginUserAsync(string username, string email)
    {
        var client = CreateClientWithCookies();
        var registerRequest = new RegisterRequest(username, email, "Password123!", $"Display {username}");
        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResponse = await client.GetAsync("/api/v1/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<JsonElement>>(_jsonOptions);
        var userId = meBody!.Data.GetProperty("id").GetGuid();

        return (client, userId, username);
    }

    private async Task<HttpClient> LoginAsModeratorAsync()
    {
        var client = CreateClientWithCookies();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("moderator", "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return client;
    }

    private async Task<Guid> CreatePostAsync(HttpClient authorClient, string content = "Safety test post")
    {
        var req = new CreatePostRequest(content, null);
        var res = await authorClient.PostAsJsonAsync("/api/v1/posts", req);
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        return body!.Data!.Id;
    }

    [Fact]
    public async Task CreateReport_WithoutAuth_ShouldReturn401Unauthorized()
    {
        var client = CreateClientWithCookies();
        var req = new CreateReportRequest(Guid.NewGuid(), null, "SPAM", "Unauthenticated report");
        var res = await client.PostAsJsonAsync("/api/v1/reports", req);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateReport_PostReport_ShouldSucceed_AndPreventDuplicates()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"author_{suffix}", $"author_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"rep_{suffix}", $"rep_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Potentially offensive content");

        // 1. Submit valid report
        var reportReq = new CreateReportRequest(postId, null, "HARASSMENT", "Offensive message targeting others");
        var reportRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        reportRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await reportRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        body.Should().NotBeNull();
        body!.Data!.TargetPostId.Should().Be(postId);
        body.Data.Category.Should().Be("HARASSMENT");
        body.Data.Status.Should().Be("PENDING");

        // 2. Duplicate report by same user should return 409 Conflict
        var dupRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        dupRes.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateReport_SelfReportingPost_ShouldReturn400BadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"self_{suffix}", $"self_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "My own post");

        var reportReq = new CreateReportRequest(postId, null, "SPAM", "Reporting myself");
        var reportRes = await authorClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        reportRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateReport_SelfReportingAccount_ShouldReturn400BadRequest()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (userClient, userId, _) = await RegisterAndLoginUserAsync($"selfu_{suffix}", $"selfu_{suffix}@test.com");

        var reportReq = new CreateReportRequest(null, userId, "SPAM", "Reporting own account");
        var reportRes = await userClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        reportRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminReports_NonStaffUser_ShouldReturn403Forbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (normalClient, _, _) = await RegisterAndLoginUserAsync($"norm_{suffix}", $"norm_{suffix}@test.com");

        var res = await normalClient.GetAsync("/api/v1/admin/reports");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminReports_ModeratorUser_ShouldReturn200OK_AndHydrateContext()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, _) = await RegisterAndLoginUserAsync($"auth_{suffix}", $"auth_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"rep_{suffix}", $"rep_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Content that needs moderation");

        // Submit report
        var reportReq = new CreateReportRequest(postId, null, "INAPPROPRIATE_CONTENT", "Check this post");
        var repRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        repRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Moderator checks queue
        var modClient = await LoginAsModeratorAsync();
        var queueRes = await modClient.GetAsync("/api/v1/admin/reports?status=PENDING");
        queueRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var queueBody = await queueRes.Content.ReadFromJsonAsync<ApiResponse<PagedReportsResult>>(_jsonOptions);
        queueBody.Should().NotBeNull();
        queueBody!.Data!.Items.Should().Contain(r => r.TargetPost != null && r.TargetPost.Id == postId);
    }

    [Fact]
    public async Task ModerationAction_HidePost_ShouldHidePostAndResolveReport()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"badpost_{suffix}", $"badpost_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"modrep_{suffix}", $"modrep_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Violating post to be hidden");

        // Report post
        var reportReq = new CreateReportRequest(postId, null, "SPAM", "Spam advertisement");
        var repRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        var repBody = await repRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        var reportId = repBody!.Data!.Id;

        // Moderator executes HIDE_POST
        var modClient = await LoginAsModeratorAsync();
        var actionReq = new ModerationActionRequest("HIDE_POST", "Violates spam policy");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/action", actionReq);
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var actionBody = await actionRes.Content.ReadFromJsonAsync<ApiResponse<ModerationActionResponse>>(_jsonOptions);
        actionBody!.Data!.Status.Should().Be("RESOLVED");

        // Verify hidden post returns 404 on direct lookup
        var postLookup = await reporterClient.GetAsync($"/api/v1/posts/{postId}");
        postLookup.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ModerationAction_SuspendUser_ShouldSuspendUserAndBlockPosting()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (badUserClient, badUserId, _) = await RegisterAndLoginUserAsync($"abusive_{suffix}", $"abusive_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"whistle_{suffix}", $"whistle_{suffix}@test.com");

        // Report user
        var reportReq = new CreateReportRequest(null, badUserId, "HARASSMENT", "Severe harassment campaign");
        var repRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        var repBody = await repRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        var reportId = repBody!.Data!.Id;

        // Moderator executes SUSPEND_USER
        var modClient = await LoginAsModeratorAsync();
        var actionReq = new ModerationActionRequest("SUSPEND_USER", "Terms of service violation");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/action", actionReq);
        var actionBodyStr = await actionRes.Content.ReadAsStringAsync();
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK, actionBodyStr);

        // Suspended user's active session is immediately revoked -> 401 Unauthorized
        var createPostReq = new CreatePostRequest("I want to post still", null);
        var postRes = await badUserClient.PostAsJsonAsync("/api/v1/posts", createPostReq);
        postRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Suspended user attempts to log in again -> 403 Forbidden
        var loginRes = await badUserClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest($"abusive_{suffix}", "Password123!"));
        loginRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ModerationAction_DismissReport_ShouldSetStatusDismissed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"innocent_{suffix}", $"innocent_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"false_{suffix}", $"false_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Innocent content");

        // Report post
        var reportReq = new CreateReportRequest(postId, null, "SPAM", "False claim");
        var repRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        var repBody = await repRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        var reportId = repBody!.Data!.Id;

        // Moderator executes DISMISS
        var modClient = await LoginAsModeratorAsync();
        var actionReq = new ModerationActionRequest("DISMISS", "No violation found");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/action", actionReq);
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var actionBody = await actionRes.Content.ReadFromJsonAsync<ApiResponse<ModerationActionResponse>>(_jsonOptions);
        actionBody!.Data!.Status.Should().Be("DISMISSED");

        // Post should still be accessible
        var postLookup = await reporterClient.GetAsync($"/api/v1/posts/{postId}");
        postLookup.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HidePost_ShouldExcludePostFromAuthorProfileAndTabs()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, authorUsername) = await RegisterAndLoginUserAsync($"hideprof_{suffix}", $"hideprof_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"rep_h_{suffix}", $"rep_h_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Spam post that should disappear from profile");

        // Report post
        var reportReq = new CreateReportRequest(postId, null, "SPAM", "Spam post");
        var repRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        var repBody = await repRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        var reportId = repBody!.Data!.Id;

        // Moderator hides post
        var modClient = await LoginAsModeratorAsync();
        var actionReq = new ModerationActionRequest("HIDE_POST", "Violating post");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/action", actionReq);
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify post is excluded from author profile posts tab
        var authorPostsRes = await reporterClient.GetAsync($"/api/v1/users/{authorUsername}/posts");
        authorPostsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var authorPosts = await authorPostsRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        authorPosts!.Data!.Items.Should().NotContain(p => p.Id == postId);
    }

    [Fact]
    public async Task SuspendUser_ProfileShouldReturn404_AndUnsuspendShouldRestoreAccountAndPosts()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, authorUsername) = await RegisterAndLoginUserAsync($"susprof_{suffix}", $"susprof_{suffix}@test.com");
        var (reporterClient, _, _) = await RegisterAndLoginUserAsync($"rep_s_{suffix}", $"rep_s_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Post before suspension");

        // Report author
        var reportReq = new CreateReportRequest(null, authorId, "HARASSMENT", "Severe harassment");
        var repRes = await reporterClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        var repBody = await repRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        var reportId = repBody!.Data!.Id;

        // Moderator suspends author
        var modClient = await LoginAsModeratorAsync();
        var actionReq = new ModerationActionRequest("SUSPEND_USER", "Terms of service violation");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{reportId}/action", actionReq);
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 1. Direct profile access returns 404 User Not Found
        var profileRes = await reporterClient.GetAsync($"/api/v1/users/{authorUsername}");
        profileRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. Profile posts tab returns 404 User Not Found
        var profilePostsRes = await reporterClient.GetAsync($"/api/v1/users/{authorUsername}/posts");
        profilePostsRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Admin lists suspended users -> should contain author
        var suspendedListRes = await modClient.GetAsync("/api/v1/admin/users/suspended");
        suspendedListRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var suspendedList = await suspendedListRes.Content.ReadFromJsonAsync<ApiResponse<PagedSuspendedUsersResult>>(_jsonOptions);
        suspendedList!.Data!.Items.Should().Contain(u => u.Id == authorId);

        // 4. Admin unsuspends user
        var unsuspendReq = new UnsuspendUserRequest("Appealed and approved");
        var unsuspendRes = await modClient.PostAsJsonAsync($"/api/v1/admin/users/{authorId}/unsuspend", unsuspendReq);
        unsuspendRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. Direct profile access now returns 200 OK
        var restoredProfileRes = await reporterClient.GetAsync($"/api/v1/users/{authorUsername}");
        restoredProfileRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 6. Profile posts tab returns 200 OK and contains the post
        var restoredPostsRes = await reporterClient.GetAsync($"/api/v1/users/{authorUsername}/posts");
        restoredPostsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var restoredPosts = await restoredPostsRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<PostResponse>>>(_jsonOptions);
        restoredPosts!.Data!.Items.Should().Contain(p => p.Id == postId);
    }

    [Fact]
    public async Task PostBySuspendedAuthor_WithReplies_ShouldReturnMaskedPost_AndPreserveReplyTree()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, authorId, authorUsername) = await RegisterAndLoginUserAsync($"auth_tree_{suffix}", $"auth_tree_{suffix}@test.com");
        var (replierClient, _, replierUsername) = await RegisterAndLoginUserAsync($"rep_tree_{suffix}", $"rep_tree_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Root post from author who will be suspended");

        // Replier replies to Author's post
        var replyReq = new CreateReplyRequest("Important reply from innocent user", null);
        var replyRes = await replierClient.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", replyReq);
        replyRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Suspend author
        var modClient = await LoginAsModeratorAsync();
        var reportReq = new CreateReportRequest(null, authorId, "SPAM", "Spam user");
        var repRes = await replierClient.PostAsJsonAsync("/api/v1/reports", reportReq);
        var repBody = await repRes.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);

        var actionReq = new ModerationActionRequest("SUSPEND_USER", "Spamming");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{repBody!.Data!.Id}/action", actionReq);
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Lookup post detail: post is masked because it has replies
        var postRes = await replierClient.GetAsync($"/api/v1/posts/{postId}");
        postRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        postBody!.Data!.Content.Should().Be("[Post is deleted]");
        postBody.Data.IsDeleted.Should().BeTrue();

        // Lookup replies: reply tree is intact and contains replier's content
        var repliesRes = await replierClient.GetAsync($"/api/v1/posts/{postId}/replies");
        repliesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var repliesBody = await repliesRes.Content.ReadFromJsonAsync<ApiResponse<HierarchicalRepliesResult>>(_jsonOptions);
        repliesBody!.Data!.Items.Should().Contain(r => r.Content == "Important reply from innocent user" && r.Author != null && r.Author.Username == replierUsername);
    }

    [Fact]
    public async Task AdminReports_MultipleReportsForSamePost_ShouldGroupIntoSingleStack_AndActionShouldResolveAll()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (authorClient, _, _) = await RegisterAndLoginUserAsync($"stack_auth_{suffix}", $"stack_auth_{suffix}@test.com");
        var (rep1Client, _, _) = await RegisterAndLoginUserAsync($"stack_rep1_{suffix}", $"stack_rep1_{suffix}@test.com");
        var (rep2Client, _, _) = await RegisterAndLoginUserAsync($"stack_rep2_{suffix}", $"stack_rep2_{suffix}@test.com");

        var postId = await CreatePostAsync(authorClient, "Content reported by multiple users");

        // Reporter 1 reports post
        var rep1Req = new CreateReportRequest(postId, null, "SPAM", "Reporter 1 claims spam");
        var rep1Res = await rep1Client.PostAsJsonAsync("/api/v1/reports", rep1Req);
        rep1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var rep1Body = await rep1Res.Content.ReadFromJsonAsync<ApiResponse<ReportResponse>>(_jsonOptions);
        var report1Id = rep1Body!.Data!.Id;

        // Reporter 2 reports same post
        var rep2Req = new CreateReportRequest(postId, null, "HARASSMENT", "Reporter 2 claims harassment");
        var rep2Res = await rep2Client.PostAsJsonAsync("/api/v1/reports", rep2Req);
        rep2Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // Moderator checks queue
        var modClient = await LoginAsModeratorAsync();
        var queueRes = await modClient.GetAsync("/api/v1/admin/reports?status=PENDING");
        queueRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var queueBody = await queueRes.Content.ReadFromJsonAsync<ApiResponse<PagedReportsResult>>(_jsonOptions);
        queueBody.Should().NotBeNull();

        // Must find exactly 1 stacked item for this postId
        var stackedItem = queueBody!.Data!.Items.FirstOrDefault(r => r.TargetPostId == postId);
        stackedItem.Should().NotBeNull();
        stackedItem!.ReportCount.Should().Be(2);
        stackedItem.Categories.Should().Contain("SPAM");
        stackedItem.Categories.Should().Contain("HARASSMENT");
        // Must NOT overwrite the card with the new report's data
        stackedItem.ReporterUserId.Should().Be(rep1Body!.Data!.ReporterUserId);
        stackedItem.Reason.Should().Be("Reporter 1 claims spam");
        stackedItem.Category.Should().Be("SPAM");
        // Must show all reports in details
        stackedItem.Reports.Should().HaveCount(2);
        stackedItem.Reports![0].ReporterUserId.Should().Be(rep1Body.Data.ReporterUserId);

        // Moderator executes HIDE_POST on report1Id
        var actionReq = new ModerationActionRequest("HIDE_POST", "Post violates terms");
        var actionRes = await modClient.PostAsJsonAsync($"/api/v1/admin/reports/{report1Id}/action", actionReq);
        actionRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify post is hidden
        var postRes = await rep1Client.GetAsync($"/api/v1/posts/{postId}");
        postRes.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Verify stack is no longer pending
        var refreshQueueRes = await modClient.GetAsync("/api/v1/admin/reports?status=PENDING");
        var refreshQueueBody = await refreshQueueRes.Content.ReadFromJsonAsync<ApiResponse<PagedReportsResult>>(_jsonOptions);
        refreshQueueBody!.Data!.Items.Should().NotContain(r => r.TargetPostId == postId);
    }
}
