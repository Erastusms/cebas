using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Auth;
using CEBAS.Application.Contracts.Media;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Application.Contracts.Users;

namespace CEBAS.IntegrationTests;

[Collection("IntegrationTests")]
public class PostEndpointsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PostEndpointsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        return _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true
        });
    }

    private async Task<(HttpClient client, CurrentUserResponse user)> RegisterAndLoginUserAsync(string username, string email)
    {
        var client = CreateAuthenticatedClient();
        var registerRequest = new RegisterRequest(username, email, "Password123!", $"Display {username}");
        var regResponse = await client.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        regResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(username, "Password123!"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var meResponse = await client.GetAsync("/api/v1/users/me");
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<ApiResponse<CurrentUserResponse>>(_jsonOptions);

        return (client, meBody!.Data!);
    }

    private async Task<Guid> UploadAndConfirmMediaAsync(HttpClient client, string fileName = "sample.png")
    {
        var uploadReq = new CreateMediaUploadRequest(fileName, "image/png", 1024 * 50);
        var uploadRes = await client.PostAsJsonAsync("/api/v1/media/upload-url", uploadReq);
        uploadRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadBody = await uploadRes.Content.ReadFromJsonAsync<ApiResponse<CreateMediaUploadResponse>>(_jsonOptions);
        var mediaId = uploadBody!.Data!.MediaId;

        // Direct binary upload with valid PNG header bytes
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x01];
        var binaryContent = new ByteArrayContent(pngBytes);
        binaryContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        var putRes = await client.PutAsync(uploadBody.Data.UploadUrl, binaryContent);
        putRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Confirm
        var confirmRes = await client.PostAsync($"/api/v1/media/{mediaId}/confirm", null);
        confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        return mediaId;
    }

    [Fact]
    public async Task CreatePost_WithoutAuth_ShouldReturn401Unauthorized()
    {
        var client = CreateAuthenticatedClient();
        var request = new CreatePostRequest("Unauthenticated post", null);
        var response = await client.PostAsJsonAsync("/api/v1/posts", request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ScenarioA_TextPost_Create_GetDetail_And_Delete()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"post_a_{suffix}", $"post_a_{suffix}@test.com");

        // 1. Create text post
        var createRequest = new CreatePostRequest("Hello CEBAS from Scenario A! 🚀", null);
        var createRes = await client.PostAsJsonAsync("/api/v1/posts", createRequest);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        createBody!.Data.Should().NotBeNull();
        createBody.Data!.Content.Should().Be("Hello CEBAS from Scenario A! 🚀");
        createBody.Data.MediaCount.Should().Be(0);
        createBody.Data.Author.Username.Should().Be(user.Username);
        var postId = createBody.Data.Id;

        // 2. Get post detail
        var getRes = await client.GetAsync($"/api/v1/posts/{postId}");
        if (!getRes.IsSuccessStatusCode)
        {
            var errStr = await getRes.Content.ReadAsStringAsync();
            throw new Exception($"GetPost failed with status {getRes.StatusCode}: {errStr}");
        }
        getRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        getBody!.Data!.Id.Should().Be(postId);
        getBody.Data.Content.Should().Be("Hello CEBAS from Scenario A! 🚀");

        // 3. Delete post as author
        var deleteRes = await client.DeleteAsync($"/api/v1/posts/{postId}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Verify post is now 404 Not Found
        var getAfterDelete = await client.GetAsync($"/api/v1/posts/{postId}");
        getAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ScenarioB_FourImages_PostCreation_ShouldCommitAtomically()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"media_b_{suffix}", $"media_b_{suffix}@test.com");

        // Upload and confirm 4 media assets via S3 direct upload emulation
        var m1 = await UploadAndConfirmMediaAsync(client, "photo1.png");
        var m2 = await UploadAndConfirmMediaAsync(client, "photo2.png");
        var m3 = await UploadAndConfirmMediaAsync(client, "photo3.png");
        var m4 = await UploadAndConfirmMediaAsync(client, "photo4.png");

        var mediaIds = new List<Guid> { m1, m2, m3, m4 };
        var createRequest = new CreatePostRequest("Post with 4 images attached!", mediaIds);

        var createRes = await client.PostAsJsonAsync("/api/v1/posts", createRequest);
        createRes.StatusCode.Should().Be(HttpStatusCode.Created);

        var createBody = await createRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        createBody!.Data!.MediaCount.Should().Be(4);
        createBody.Data.Media.Should().HaveCount(4);
        createBody.Data.Media[0].Position.Should().Be(0);
        createBody.Data.Media[1].Position.Should().Be(1);
        createBody.Data.Media[2].Position.Should().Be(2);
        createBody.Data.Media[3].Position.Should().Be(3);
    }

    [Fact]
    public async Task ScenarioC_NestedConversation_ShouldReturnHierarchicalReplies()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"user_c1_{suffix}", $"user_c1_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"user_c2_{suffix}", $"user_c2_{suffix}@test.com");

        // 1. Create main post by User A
        var postRes = await clientA.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Main conversation topic", null));
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        var postId = postBody!.Data!.Id;

        // 2. User B creates direct Reply A
        var replyARes = await clientB.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply A", null));
        replyARes.StatusCode.Should().Be(HttpStatusCode.Created);
        var replyABody = await replyARes.Content.ReadFromJsonAsync<ApiResponse<ReplyResponse>>(_jsonOptions);
        var replyAId = replyABody!.Data!.Id;

        // 3. User A replies to Reply A (Reply A.1)
        var replyA1Res = await clientA.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply A.1", replyAId));
        replyA1Res.StatusCode.Should().Be(HttpStatusCode.Created);
        var replyA1Body = await replyA1Res.Content.ReadFromJsonAsync<ApiResponse<ReplyResponse>>(_jsonOptions);
        var replyA1Id = replyA1Body!.Data!.Id;

        // 4. User B replies to Reply A.1 (Reply A.1.1)
        var replyA11Res = await clientB.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply A.1.1", replyA1Id));
        replyA11Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // 5. User A creates direct Reply B
        var replyBRes = await clientA.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply B", null));
        replyBRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // 6. Retrieve thread replies hierarchy
        var getRepliesRes = await clientA.GetAsync($"/api/v1/posts/{postId}/replies");
        getRepliesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var repliesBody = await getRepliesRes.Content.ReadFromJsonAsync<ApiResponse<HierarchicalRepliesResult>>(_jsonOptions);

        repliesBody!.Data!.Items.Should().HaveCount(4);
        // Order: Reply A (depth 0) -> Reply A.1 (depth 1) -> Reply A.1.1 (depth 2) -> Reply B (depth 0)
        repliesBody.Data.Items[0].Content.Should().Be("Reply A");
        repliesBody.Data.Items[0].Depth.Should().Be(0);

        repliesBody.Data.Items[1].Content.Should().Be("Reply A.1");
        repliesBody.Data.Items[1].Depth.Should().Be(1);

        repliesBody.Data.Items[2].Content.Should().Be("Reply A.1.1");
        repliesBody.Data.Items[2].Depth.Should().Be(2);

        repliesBody.Data.Items[3].Content.Should().Be("Reply B");
        repliesBody.Data.Items[3].Depth.Should().Be(0);
    }

    [Fact]
    public async Task ScenarioD_DeleteParentReply_PreservesChildRepliesHierarchy()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (client, user) = await RegisterAndLoginUserAsync($"user_d_{suffix}", $"user_d_{suffix}@test.com");

        // 1. Create Post
        var postRes = await client.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post for Scenario D", null));
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        var postId = postBody!.Data!.Id;

        // 2. Create Reply A and child Reply A.1
        var replyARes = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply A", null));
        var replyABody = await replyARes.Content.ReadFromJsonAsync<ApiResponse<ReplyResponse>>(_jsonOptions);
        var replyAId = replyABody!.Data!.Id;

        var replyA1Res = await client.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply A.1 child", replyAId));
        replyA1Res.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Delete Reply A
        var deleteReplyRes = await client.DeleteAsync($"/api/v1/replies/{replyAId}");
        deleteReplyRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Retrieve replies: Reply A is soft-deleted placeholder, Reply A.1 remains intact
        var repliesRes = await client.GetAsync($"/api/v1/posts/{postId}/replies");
        repliesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var repliesBody = await repliesRes.Content.ReadFromJsonAsync<ApiResponse<HierarchicalRepliesResult>>(_jsonOptions);

        repliesBody!.Data!.Items.Should().HaveCount(2);
        repliesBody.Data.Items[0].IsDeleted.Should().BeTrue();
        repliesBody.Data.Items[0].Content.Should().Contain("deleted by the author");
        repliesBody.Data.Items[1].Content.Should().Be("Reply A.1 child");
        repliesBody.Data.Items[1].Depth.Should().Be(1);
    }

    [Fact]
    public async Task DeletePost_ByNonOwner_ShouldReturn403Forbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, _) = await RegisterAndLoginUserAsync($"owner_{suffix}", $"owner_{suffix}@test.com");
        var (clientB, _) = await RegisterAndLoginUserAsync($"intruder_{suffix}", $"intruder_{suffix}@test.com");

        var postRes = await clientA.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Owner post", null));
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);

        var deleteRes = await clientB.DeleteAsync($"/api/v1/posts/{postBody!.Data!.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteReply_ByNonOwner_ShouldReturn403Forbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, _) = await RegisterAndLoginUserAsync($"rep_a_{suffix}", $"rep_a_{suffix}@test.com");
        var (clientB, _) = await RegisterAndLoginUserAsync($"rep_b_{suffix}", $"rep_b_{suffix}@test.com");

        var postRes = await clientA.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post", null));
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);

        var replyRes = await clientA.PostAsJsonAsync($"/api/v1/posts/{postBody!.Data!.Id}/replies", new CreateReplyRequest("Alice reply", null));
        var replyBody = await replyRes.Content.ReadFromJsonAsync<ApiResponse<ReplyResponse>>(_jsonOptions);

        var deleteRes = await clientB.DeleteAsync($"/api/v1/replies/{replyBody!.Data!.Id}");
        deleteRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserReplies_ShouldReturnOnlyUserAuthoredReplies()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var (clientA, userA) = await RegisterAndLoginUserAsync($"poster_{suffix}", $"poster_{suffix}@test.com");
        var (clientB, userB) = await RegisterAndLoginUserAsync($"replier_{suffix}", $"replier_{suffix}@test.com");

        // User A creates a post
        var postRes = await clientA.PostAsJsonAsync("/api/v1/posts", new CreatePostRequest("Post from User A", null));
        var postBody = await postRes.Content.ReadFromJsonAsync<ApiResponse<PostResponse>>(_jsonOptions);
        var postId = postBody!.Data!.Id;

        // User B replies to User A's post
        var replyRes = await clientB.PostAsJsonAsync($"/api/v1/posts/{postId}/replies", new CreateReplyRequest("Reply from User B", null));
        replyRes.StatusCode.Should().Be(HttpStatusCode.Created);

        // Fetch replies for User B
        var userBRepliesRes = await clientB.GetAsync($"/api/v1/users/{userB.Username}/replies");
        userBRepliesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var userBRepliesBody = await userBRepliesRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<UserReplyResponse>>>(_jsonOptions);

        userBRepliesBody!.Data!.Items.Should().HaveCount(1);
        userBRepliesBody.Data.Items[0].Content.Should().Be("Reply from User B");
        userBRepliesBody.Data.Items[0].ReplyingToUsername.Should().Be(userA.Username);
        userBRepliesBody.Data.Items[0].Author.Username.Should().Be(userB.Username);

        // Fetch replies for User A (should be empty since User A only posted, didn't reply)
        var userARepliesRes = await clientA.GetAsync($"/api/v1/users/{userA.Username}/replies");
        userARepliesRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var userARepliesBody = await userARepliesRes.Content.ReadFromJsonAsync<ApiResponse<CursorPagedResult<UserReplyResponse>>>(_jsonOptions);
        userARepliesBody!.Data!.Items.Should().BeEmpty();
    }
}
