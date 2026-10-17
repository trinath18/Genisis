using FluentValidation;
using Genisis.Api.Auth;
using Genisis.Api.Case;
using Genisis.Api.Common;
using Genisis.Api.Data;
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
builder.Services.AddScoped<Genisis.Api.Benefits.BenefitService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseRegistrationService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseDocumentsService>();
builder.Services.AddScoped<Genisis.Api.Case.CaseAdjustmentService>();
builder.Services.AddSingleton<Genisis.Api.Case.CaseFilesService>();
builder.Services.AddCaseScreens();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

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

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlDocs = Path.Combine(AppContext.BaseDirectory, "Genisis.Api.xml");
    if (File.Exists(xmlDocs)) options.IncludeXmlComments(xmlDocs);
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
