using Genisis.Api.Auth;
using Genisis.Api.Common.Errors;
using Genisis.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<DbConnections>();
builder.Services.AddScoped<Genisis.Api.Registration.RegistrationService>();
builder.Services.AddScoped<Genisis.Api.Plans.PlanService>();
builder.Services.AddScoped<Genisis.Api.Plans.AnnualLimitService>();
builder.Services.AddScoped<Genisis.Api.Plans.PremiumService>();
builder.Services.AddScoped<Genisis.Api.Plans.CodeListService>();
builder.Services.AddScoped<Genisis.Api.Plans.SpecialNoteService>();
builder.Services.AddScoped<Genisis.Api.Plans.BenefitLimitService>();
builder.Services.AddScoped<Genisis.Api.Plans.MgmtFeeService>();
builder.Services.AddScoped<Genisis.Api.Plans.McoFeeService>();
builder.Services.AddScoped<Genisis.Api.Benefits.IBenefitService, Genisis.Api.Benefits.BenefitService>();
builder.Services.AddScoped<Genisis.Api.Mma.IMmaService, Genisis.Api.Mma.MmaService>();
builder.Services.AddScoped<Genisis.Api.Adjustment.IAdjustmentService, Genisis.Api.Adjustment.AdjustmentService>();
builder.Services.AddScoped<Genisis.Api.Adjustment.IAdjustmentQueryService, Genisis.Api.Adjustment.AdjustmentQueryService>();
builder.Services.Configure<Genisis.Api.Membership.DocumentOptions>(builder.Configuration.GetSection(Genisis.Api.Membership.DocumentOptions.SectionName));
builder.Services.AddSingleton<Genisis.Api.Membership.IPolicyWordingStore, Genisis.Api.Membership.PolicyWordingStore>();
builder.Services.AddScoped<Genisis.Api.Membership.IEnquiryTabsService, Genisis.Api.Membership.EnquiryTabsService>();
builder.Services.AddScoped<Genisis.Api.Bordx.BordxAsciiExporter>();
builder.Services.AddScoped<Genisis.Api.Bordx.IBordxService, Genisis.Api.Bordx.BordxService>();
builder.Services.AddScoped<Genisis.Api.Tracking.ITrackingRepository, Genisis.Api.Tracking.TrackingRepository>();
builder.Services.AddScoped<Genisis.Api.Tracking.ITrackingService, Genisis.Api.Tracking.TrackingService>();
builder.Services.AddScoped<Genisis.Api.Reinstatement.IReinstatementService, Genisis.Api.Reinstatement.ReinstatementService>();
builder.Services.AddScoped<Genisis.Api.McoPending.IMcoPendingService, Genisis.Api.McoPending.McoPendingService>();
builder.Services.AddScoped<Genisis.Api.Pluto.PlutoAsciiExporter>();
builder.Services.AddScoped<Genisis.Api.Pluto.IPlutoService, Genisis.Api.Pluto.PlutoService>();
builder.Services.AddScoped<Genisis.Api.CardControl.ICardControlService, Genisis.Api.CardControl.CardControlService>();
builder.Services.AddScoped<Genisis.Api.Benefits.BenefitService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseRegistrationService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseDocumentsService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseAdjustmentService>();
builder.Services.AddSingleton<Genisis.Api.Case.CaseFilesService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TokenService>((options, tokens) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = tokens.Issuer,
            ValidAudience = tokens.Issuer,
            IssuerSigningKey = tokens.SigningKey,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
        };
    });
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Genisis.Api")
        .LogError(error, "Unhandled error processing {Method} {Path}", context.Request.Method, context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new
    {
        message = error is Microsoft.Data.SqlClient.SqlException { Number: StoredProcedures.MissingProcedure }
            ? "The database is missing the stored procedures this version needs. Run sql/genisis-main.sql and sql/genisis-maintenance.sql (see sql/README.md)."
            : "Server error. Check the API log for details.",
    });
}));

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
