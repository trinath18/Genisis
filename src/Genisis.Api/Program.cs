using Genisis.Api.Maintenance;
using FluentValidation;
using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Common;
using Genisis.Api.Common.Errors;
using Genisis.Api.Data;
using Genisis.Api.Worksheet;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Centralized logging: console + rolling files, configured in the "Serilog" section of appsettings.json.
builder.Host.UseSerilog((context, services, logging) => logging.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());

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
builder.Services.AddScoped<Genisis.Api.Deletion.IDeletionService, Genisis.Api.Deletion.DeletionService>();
builder.Services.AddScoped<Genisis.Api.LeafletCard.ILeafletCardService, Genisis.Api.LeafletCard.LeafletCardService>();
builder.Services.AddScoped<Genisis.Api.Registration.ITempRegistrationService, Genisis.Api.Registration.TempRegistrationService>();
builder.Services.AddScoped<Genisis.Api.Adjustment.ITempAdjustmentService, Genisis.Api.Adjustment.TempAdjustmentService>();
builder.Services.AddScoped<Genisis.Api.Benefits.BenefitService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseRegistrationService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseDocumentsService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseAdjustmentService>();
builder.Services.AddSingleton<Genisis.Api.Case.CaseFilesService>();
builder.Services.AddCaseScreens();
builder.Services.AddWorksheetScreens();
builder.Services.AddMaintenanceScreens();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

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
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlDocs = Path.Combine(AppContext.BaseDirectory, "Genisis.Api.xml");
    if (File.Exists(xmlDocs)) options.IncludeXmlComments(xmlDocs);
    // Several screens have request / result types with the same class name (e.g. Membership and Case MmaProcedureRequest);
    // schema ids carry the namespace so they stay unique.
    options.CustomSchemaIds(Genisis.Api.Common.SwaggerSchemaIds.For);
    var bearer = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste the token from POST /api/auth/login.",
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnostics, http) => diagnostics.Set("User", http.User.Identity?.Name ?? "anonymous"));
app.UseExceptionHandler();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
