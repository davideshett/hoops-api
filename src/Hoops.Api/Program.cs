using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hoops.Api.Auth;
using Hoops.Api.Health;
using Hoops.Api.Http;
using Hoops.Infrastructure;
using Hoops.Infrastructure.Persistence;
using Hoops.Modules.Identity;
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

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

// ── Options ────────────────────────────────────────────────────────────────
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey) && Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
        "Jwt:SigningKey must be configured and at least 32 bytes.")
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

// Apply migrations on startup so `docker compose up -d && dotnet run` is enough to be usable.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.Run();

/// <summary>Exposed so the integration-test host (<c>WebApplicationFactory</c>) can boot the app.</summary>
public partial class Program;
