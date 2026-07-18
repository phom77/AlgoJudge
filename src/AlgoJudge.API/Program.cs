using AlgoJudge.API.Configuration;
using AlgoJudge.API.ErrorHandling;
using AlgoJudge.API.Health;
using AlgoJudge.API.Middleware;
using AlgoJudge.API.Security;
using AlgoJudge.Application.Interfaces;
using AlgoJudge.Application.Mappings;
using AlgoJudge.Application.Services;
using AlgoJudge.Infrastructure.Data;
using AlgoJudge.Infrastructure.Health;
using AlgoJudge.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(options =>
    {
        options.ColorBehavior = LoggerColorBehavior.Enabled;
        options.IncludeScopes = false;
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
        options.UseUtcTimestamp = true;
    });
}
else
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        options.UseUtcTimestamp = true;
    });
}

var connectionString = PostgreSqlHealthCheck.ValidateConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection"),
    "API");

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();

var rateLimitingOptions = builder.Configuration
    .GetSection(RateLimitingOptions.SectionName)
    .Get<RateLimitingOptions>() ?? new RateLimitingOptions();
rateLimitingOptions.Validate();

var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(rateLimitingOptions);
builder.Services.AddSingleton(databaseOptions);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails is ApiProblemDetails or ApiValidationProblemDetails)
        {
            context.ProblemDetails.Extensions.Remove("traceId");
            return;
        }

        context.ProblemDetails.Extensions["code"] =
            ApiErrorContract.GetCode(context.ProblemDetails.Type);
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrWhiteSpace(context.Token))
                {
                    var cookieManager = context.HttpContext.RequestServices
                        .GetRequiredService<AuthCookieManager>();
                    context.Token = cookieManager.ReadAccessToken(context.Request);
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await ProblemDetailsResponse.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Authentication is required.",
                    ApiErrorContract.AuthenticationType,
                    "Provide a valid bearer token to access this resource.");
            },
            OnForbidden = context => ProblemDetailsResponse.WriteAsync(
                context.HttpContext,
                StatusCodes.Status403Forbidden,
                "Access is forbidden.",
                ApiErrorContract.ForbiddenType,
                "The authenticated user cannot access this resource.")
        };
    });

builder.Services.AddAuthorization();
DataProtectionConfiguration.AddConfiguredDataProtection(builder);
builder.Services.AddAntiforgery(options =>
{
    var useSecureCookies = !builder.Environment.IsDevelopment();
    options.HeaderName = AuthCookieManager.AntiforgeryHeaderName;
    options.Cookie.Name = useSecureCookies
        ? AuthCookieManager.AntiforgeryCookieName
        : AuthCookieManager.DevelopmentAntiforgeryCookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = useSecureCookies
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.None;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
});
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var result = new BadRequestObjectResult(
                ApiErrorContract.CreateValidation(context));
            result.ContentTypes.Add("application/problem+json");
            return result;
        };
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "AlgoJudge API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Stable backend contract for the AlgoJudge problem catalogue and submission workflow.";
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["CookieSession"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = AuthCookieManager.AccessCookieName,
            Description = "Secure HttpOnly access-token cookie issued by the authentication endpoints."
        };
        document.Components.SecuritySchemes["RefreshCookie"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = AuthCookieManager.RefreshCookieName,
            Description = "Secure HttpOnly refresh-token cookie restricted to authentication endpoints."
        };
        document.Components.SecuritySchemes["AntiforgeryHeader"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = AuthCookieManager.AntiforgeryHeaderName,
            Description = "Angular antiforgery header paired with the cookies from GET /api/auth/csrf."
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var cookieRequirement = new OpenApiSecurityRequirement();
        var requiresBearer = metadata.OfType<IAuthorizeData>().Any() &&
            !metadata.OfType<IAllowAnonymous>().Any();
        if (requiresBearer)
        {
            cookieRequirement[
                new OpenApiSecuritySchemeReference("CookieSession", context.Document)] = [];
        }

        var controller = context.Description.ActionDescriptor.RouteValues["controller"];
        var action = context.Description.ActionDescriptor.RouteValues["action"];
        if (string.Equals(controller, "Auth", StringComparison.Ordinal) &&
            action is "Refresh" or "Revoke")
        {
            cookieRequirement[
                new OpenApiSecuritySchemeReference("RefreshCookie", context.Document)] = [];
        }

        var httpMethod = context.Description.HttpMethod ?? string.Empty;
        var isUnsafe = !HttpMethods.IsGet(httpMethod) &&
            !HttpMethods.IsHead(httpMethod) &&
            !HttpMethods.IsOptions(httpMethod) &&
            !HttpMethods.IsTrace(httpMethod);
        if (isUnsafe)
        {
            cookieRequirement[
                new OpenApiSecuritySchemeReference("AntiforgeryHeader", context.Document)] = [];
        }

        if (cookieRequirement.Count > 0)
            operation.Security = [cookieRequirement];
        else if (string.Equals(controller, "Problems", StringComparison.Ordinal))
        {
            var optionalCookie = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("CookieSession", context.Document)] = []
            };
            operation.Security = [new OpenApiSecurityRequirement(), optionalCookie];
        }

        return Task.CompletedTask;
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds),
                QueueLimit = rateLimitingOptions.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    });
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        await ProblemDetailsResponse.WriteAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "Too many requests.",
            ApiErrorContract.RateLimitType,
            "The request rate limit has been exceeded. Retry later.");
    };
});

builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("The API process is running."),
        tags: ["live", "ready"])
    .AddCheck(
        "postgresql",
        new PostgreSqlHealthCheck(connectionString),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProblemRepository, ProblemRepository>();
builder.Services.AddScoped<IProblemService, ProblemService>();
builder.Services.AddScoped<ISubmissionRepository, SubmissionRepository>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IRunRepository, RunRepository>();
builder.Services.AddScoped<IRunService, RunService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<AuthCookieManager>();

var app = builder.Build();

if (databaseOptions.MigrateOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapOpenApi("/openapi/{documentName}.json")
    .DisableRateLimiting();
app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    })
    .DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteAsync
    })
    .DisableRateLimiting();
app.MapControllers();

await app.RunAsync();

public partial class Program;
