using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Genisis.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Genisis.Api.Tests;

/// <summary>The whole API pipeline in memory: routing, authentication, menu rights, request validation and the exception handler.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AllRights = "111111111111111111111111111111";
    public const string NoRights = "000000000000000000000000000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Auth:JwtKey", "integration-test-signing-key-0123456789-abcdefghijklmnop");
        if (TestDb.MainDb is { } main)
        {
            builder.UseSetting("ConnectionStrings:MainDb", main);
            builder.UseSetting("ConnectionStrings:Maintenance", TestDb.Maintenance ?? main);
            builder.UseSetting("ConnectionStrings:WorkSheet", TestDb.WorkSheet ?? main);
        }
    }

    /// <summary>A client carrying a valid token with the given USRAccess flags.</summary>
    public HttpClient ClientWith(string? access)
    {
        var client = CreateClient();
        if (access is not null)
            client.DefaultRequestHeaders.Authorization = new("Bearer", Services.GetRequiredService<TokenService>().Create("TESTER", "Tester", access).Token);
        return client;
    }
}

/// <summary>Integration tests of the nine Case screens' endpoints.</summary>
public class ApiIntegrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly (HttpMethod Method, string Url)[] Routes =
    [
        (HttpMethod.Get, "/api/case/payors"),
        (HttpMethod.Get, "/api/case/panel-hospitals/codes"),
        (HttpMethod.Get, "/api/case/panel-hospitals?code=x"),
        (HttpMethod.Get, "/api/case/non-disclosure/X"),
        (HttpMethod.Put, "/api/case/non-disclosure/X"),
        (HttpMethod.Get, "/api/case/pay-claim/X"),
        (HttpMethod.Put, "/api/case/pay-claim/X"),
        (HttpMethod.Get, "/api/case/mma-cpt/procedures"),
        (HttpMethod.Post, "/api/case/mma-cpt/procedures"),
        (HttpMethod.Put, "/api/case/mma-cpt/procedures/1"),
        (HttpMethod.Delete, "/api/case/mma-cpt/procedures/1"),
        (HttpMethod.Get, "/api/case/mma-cpt/fees"),
        (HttpMethod.Post, "/api/case/mma-cpt/fees"),
        (HttpMethod.Get, "/api/case/audit-review/X"),
        (HttpMethod.Put, "/api/case/audit-review/X"),
        (HttpMethod.Get, "/api/case/transfer/X"),
        (HttpMethod.Get, "/api/case/transfer/member?memberNo=a&coverId=b"),
        (HttpMethod.Post, "/api/case/transfer/X"),
        (HttpMethod.Get, "/api/case/listing/search?wksNoFrom=a"),
        (HttpMethod.Post, "/api/case/listing/documents"),
        (HttpMethod.Post, "/api/case/listing/bordx"),
        (HttpMethod.Post, "/api/case/claim-ascii/generate"),
        (HttpMethod.Get, "/api/case/claim-ascii/files/x"),
        (HttpMethod.Post, "/api/case/case-ascii/generate"),
        (HttpMethod.Get, "/api/case/case-ascii/files/x"),
    ];

    private static HttpRequestMessage Request((HttpMethod Method, string Url) route, string? json = "{}")
    {
        var request = new HttpRequestMessage(route.Method, route.Url);
        if (route.Method != HttpMethod.Get && route.Method != HttpMethod.Delete && json is not null)
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    private static async Task<string?> Message(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();

    [Fact]
    public async Task Every_screen_endpoint_requires_a_login()
    {
        using var client = factory.ClientWith(null);
        foreach (var route in Routes)
            Assert.True(HttpStatusCode.Unauthorized == (await client.SendAsync(Request(route))).StatusCode, $"{route.Method} {route.Url}");
    }

    [Fact]
    public async Task Every_screen_endpoint_requires_its_menu_right()
    {
        using var client = factory.ClientWith(ApiFactory.NoRights);
        foreach (var route in Routes)
            Assert.True(HttpStatusCode.Forbidden == (await client.SendAsync(Request(route))).StatusCode, $"{route.Method} {route.Url}");
    }

    [Fact]
    public async Task Case_adjustment_right_alone_opens_transfer_pay_claim_audit_review_and_non_disclosure()
    {
        using var client = factory.ClientWith("000000000001000000000000000000");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/case/pay-claim/X", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/case/panel-hospitals/codes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/case/claim-ascii/generate", new { })).StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/api/case/non-disclosure/X", """{"preCategory":"9"}""", "Select a valid Pre-Existing Illness category.")]
    [InlineData("PUT", "/api/case/pay-claim/X", "{}", "Please enter Pay&Claim Date")]
    [InlineData("POST", "/api/case/mma-cpt/procedures", "{}", "Please Enter Main Body Part")]
    [InlineData("POST", "/api/case/mma-cpt/fees", "{}", "Please enter a Surgeon's or Anaesthetist's category.")]
    [InlineData("PUT", "/api/case/audit-review/X", "{}", "Please Enter Discount Type!")]
    [InlineData("POST", "/api/case/transfer/X", "{}", "Please enter the Member No. to transfer to.")]
    [InlineData("GET", "/api/case/transfer/member", null, "Enter the Member No. and the Cover ID.")]
    [InlineData("GET", "/api/case/panel-hospitals", null, "Please Select Hospital Panel Code!")]
    [InlineData("GET", "/api/case/listing/search", null, "Please Enter Worksheet No")]
    [InlineData("POST", "/api/case/listing/bordx", "{}", "Please select at least one record to be updated!")]
    [InlineData("POST", "/api/case/claim-ascii/generate", "{}", "Please enter the Payor code.")]
    [InlineData("POST", "/api/case/case-ascii/generate", """{"payor":"AC"}""", "Please enter Case Reg. Date from.")]
    public async Task Invalid_requests_are_answered_with_the_first_message_before_any_work_is_done(string method, string url, string? json, string message)
    {
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.SendAsync(Request((new HttpMethod(method), url), json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(message, await Message(response));
    }

    [Fact]
    public async Task A_document_check_for_too_many_cases_is_refused()
    {
        using var client = factory.ClientWith(ApiFactory.AllRights);
        var response = await client.PostAsJsonAsync("/api/case/listing/documents", new { cases = Enumerable.Range(0, 1001).Select(i => $"AC{i}") });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Check at most 1000 cases at a time.", await Message(response));
    }

    [Fact]
    public async Task Without_a_document_store_the_document_check_says_it_is_unavailable()
    {
        await using var unconfigured = factory.WithWebHostBuilder(b => b.UseSetting("Documents:RootPath", ""));
        using var client = unconfigured.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", unconfigured.Services.GetRequiredService<TokenService>().Create("TESTER", "Tester", ApiFactory.AllRights).Token);
        var response = await client.PostAsJsonAsync("/api/case/listing/documents", new { cases = new[] { "AC1" } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("available").GetBoolean());
        Assert.False(body.GetProperty("cases").GetProperty("AC1").GetBoolean());
    }

    // ---- against the development databases (GENISIS_TEST_MAINDB) ----

    [Fact]
    public async Task Lookups_and_unknown_records_are_answered_by_the_exception_handler_over_http()
    {
        if (TestDb.MainDb is null) return;
        await SqlScripts.EnsureDeployedAsync(TestDb.MainDb);
        using var client = factory.ClientWith(ApiFactory.AllRights);

        var payors = await client.GetFromJsonAsync<JsonElement>("/api/case/payors");
        Assert.True(payors.GetArrayLength() > 0);
        var codes = await client.GetFromJsonAsync<JsonElement>("/api/case/panel-hospitals/codes");
        Assert.True(codes.GetArrayLength() > 0);

        var unknown = await client.GetAsync("/api/case/pay-claim/NO%20SUCH%20CASE");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("Record not found!", await Message(unknown));
        var nonDisclosure = await client.GetAsync("/api/case/non-disclosure/NO%20SUCH%20CASE");
        Assert.Equal((HttpStatusCode.NotFound, "No record found!"), (nonDisclosure.StatusCode, await Message(nonDisclosure)));
        var transfer = await client.PostAsJsonAsync("/api/case/transfer/NO%20SUCH%20CASE", new { memberNo = "M1", coverId = "00" });
        Assert.Equal(HttpStatusCode.NotFound, transfer.StatusCode);
        var member = await client.GetAsync("/api/case/transfer/member?memberNo=NO%20SUCH&coverId=00");
        Assert.Equal((HttpStatusCode.NotFound, "Member not found."), (member.StatusCode, await Message(member)));
        var audit = await client.GetAsync("/api/case/audit-review/NO%20SUCH%20CASE");
        Assert.Equal(HttpStatusCode.NotFound, audit.StatusCode);
        var claimFile = await client.GetAsync("/api/case/claim-ascii/files/nothing");
        Assert.Equal(HttpStatusCode.NotFound, claimFile.StatusCode);
        var none = await client.PostAsJsonAsync("/api/case/claim-ascii/generate",
            new { payor = "NO SUCH PAYOR", dateFrom = "2021-07-01", dateTo = "2021-07-02", exportStatus = "N", worksheetStatus = "0" });
        Assert.Equal((HttpStatusCode.NotFound, "No record found!"), (none.StatusCode, await Message(none)));
    }
}
