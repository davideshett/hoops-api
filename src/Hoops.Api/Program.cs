using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hoops.Api.Auth;
using Hoops.Api.Health;
using Hoops.Api.BackgroundServices;
using Hoops.Api.Http;
using Hoops.Api.Logging;
using Hoops.Api.Observability;
using Hoops.Infrastructure;
using Hoops.Infrastructure.Persistence;
using Hoops.Modules.Competitions;
using Hoops.Modules.GameRecording;
using Hoops.Modules.Identity;
using Hoops.Modules.Registry;
using Hoops.Modules.Statistics;
using Hoops.Modules.Identity.Application.Abstractions;
using Hoops.SharedKernel.Abstractions;
using Hoops.SharedKernel.Identifiers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Both signing secrets must come from a secret store in real environments. For local development
// only, supply stable in-memory defaults so `dotnet run` works — in memory, never in a committed
// file, because a value in appsettings.json binds in every environment and would turn a forgotten
// secret into a silently insecure production boot rather than a failed one.
if (builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(builder.Configuration["Registry:NinPepper"]))
    {
        builder.Configuration["Registry:NinPepper"] = "dev-only-insecure-nin-pepper-change-me";
    }

    if (string.IsNullOrWhiteSpace(builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"]))
    {
        builder.Configuration[$"{JwtOptions.SectionName}:SigningKey"] = JwtOptions.DevelopmentPlaceholderKey;
    }
}

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.With(new SensitiveDataEnricher()) // redacts nin/ninHmac/guardianPhone/photoObjectKey
        .WriteTo.Console();

    // Test hosts set CaptureLogs=true to assert a plaintext NIN never reaches any log line.
    if (context.Configuration.GetValue<bool>("CaptureLogs"))
    {
        loggerConfiguration.WriteTo.Sink(new CapturingSink());
    }
});

// ── Options ────────────────────────────────────────────────────────────────
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and at least 32 bytes. Set the Jwt__SigningKey environment "
        + "variable from your secret store.")
    // Length alone cannot tell a real secret from the development placeholder, which is long enough to
    // pass and is public. Outside Development it is refused by name.
    .Validate(o => isDevelopment || o.SigningKey != JwtOptions.DevelopmentPlaceholderKey,
        "Jwt:SigningKey is the development placeholder, which is published in source. Supply a real "
        + "secret via Jwt__SigningKey.")
    .ValidateOnStart();

// Resolve AuthOptions from the bound JwtOptions so it reflects the final configuration (including
// test/host overrides), never an eagerly-captured snapshot.
builder.Services.AddSingleton(sp =>
    new AuthOptions(TimeSpan.FromDays(sp.GetRequiredService<IOptions<JwtOptions>>().Value.RefreshTokenDays)));

// ── MVC + JSON + validation ────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new StronglyTypedIdJsonConverterFactory()));

builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = "One or more validation errors occurred.",
        };
        problem.Extensions["code"] = "VALIDATION_ERROR";
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" },
        };
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// ── Application + infrastructure ───────────────────────────────────────────
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddIdentityModule();
builder.Services.AddCompetitionsModule();
builder.Services.AddRegistryModule();
builder.Services.AddGameRecordingModule();
builder.Services.AddStatisticsModule();

// Drains the outbox so finalisation-triggered recomputes survive a crash (§9.3).
builder.Services.AddSingleton<OutboxDrainer>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxDrainer>());

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
builder.Services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();

// ── Auth ───────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Configure the bearer scheme from the bound JwtOptions (via DI) so token validation uses exactly the
// same key/issuer/audience the token generator signs with — even under host/test configuration overrides.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        var options = jwt.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = options.Issuer,
            ValidAudience = options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        bearer.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ProblemJson.WriteAsync(
                    context.HttpContext, StatusCodes.Status401Unauthorized,
                    "UNAUTHENTICATED", "Unauthorized", "Authentication is required.");
            },
            OnForbidden = context => ProblemJson.WriteAsync(
                context.HttpContext, StatusCodes.Status403Forbidden,
                "FORBIDDEN", "Forbidden", "You do not have access to this resource."),
        };
    });

builder.Services.AddHoopsAuthorization();

// ── Errors, health, docs ───────────────────────────────────────────────────
builder.Services.AddHoopsObservability(builder.Configuration, "hoops-api");
builder.Services.AddHoopsRateLimiting(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadyHealthCheck>("database", tags: ["ready"]);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hoops API",
        Version = "v1",
        Description = "Nigerian Basketball Platform API.",
    });

    foreach (var xml in Directory.GetFiles(AppContext.BaseDirectory, "Hoops.*.xml"))
    {
        options.IncludeXmlComments(xml, includeControllerXmlComments: true);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token (without the 'Bearer ' prefix).",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document, null), new List<string>() },
    });
});

var app = builder.Build();

// Migrate on startup ONLY in development, so `docker compose up -d && dotnet run` is enough to be
// usable locally. In every other environment migrations are applied as SQL ahead of the deploy (see
// docs/deployment.md): with more than one instance running, startup migration is a race.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hoops API v1");
        options.DocumentTitle = "Hoops API";
    });
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

/// <summary>Exposed so the integration-test host (<c>WebApplicationFactory</c>) can boot the app.</summary>
public partial class Program;
