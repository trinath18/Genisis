using Genisis.Api.Auth;
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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
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
