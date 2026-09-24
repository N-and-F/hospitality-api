using System.Text;
using Hospitality.Api.Auth;
using Hospitality.Api.Common;
using Hospitality.Api.Data;
using Hospitality.Api.Endpoints;
using Hospitality.Api.Felogan;
using Hospitality.Api.Identity;
using Hospitality.Api.Ledger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

EnvFile.LoadIfPresent();

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["JWT_SIGNING_KEY"];
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    {
        throw new InvalidOperationException("JWT_SIGNING_KEY must be at least 32 bytes.");
    }
}

jwtKey ??= "dev-only-signing-key-32-bytes-min!";

var databaseUrl = builder.Configuration["DATABASE_URL"];
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(databaseUrl))
    {
        options.UseNpgsql(PostgresUrl.ToConnectionString(databaseUrl));
        return;
    }

    if (builder.Environment.IsDevelopment())
    {
        options.UseInMemoryDatabase("HospitalityDev");
        return;
    }

    throw new InvalidOperationException("DATABASE_URL is required.");
});

builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = false;
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
        options.Password.RequiredLength = 1;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration>((options, config) =>
    {
        var key = config["JWT_SIGNING_KEY"] ?? jwtKey;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            RoleClaimType = "role",
            NameClaimType = "unique_name"
        };
        options.Events = JwtBearerErrorEvents.Create();
    });

builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<WalletSetService>();
builder.Services.AddScoped<FeloganService>();
builder.Services.AddHostedService<IdentitySeedHostedService>();
builder.Services.AddHostedService<WalletSetSeedHostedService>();
builder.Services.AddHostedService<FeloganSeedHostedService>();

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "docs";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Hospitality API v1");
});
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
var v1 = app.MapGroup("/v1");
v1.MapAuthEndpoints();
v1.MapCurrentUserEndpoints();
v1.MapStaffEndpoints();
v1.MapWalletSetEndpoints();
v1.MapLedgerEndpoints();
v1.MapFeloganEndpoints();

app.Run();

public partial class Program;
