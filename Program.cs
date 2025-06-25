using FitnessTracker.API.Data;
using FitnessTracker.API.Services;
using FitnessTracker.API.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger configuration
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fitness Tracker API",
        Version = "v2.1.0",
        Description = "API for fitness tracking with LW Coin system and referrals"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=fitness.db"));

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IFoodIntakeService, FoodIntakeService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<ICoinService, CoinService>();
builder.Services.AddScoped<ISkinService, SkinService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IMissionService, MissionService>();
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ILwCoinService, LwCoinService>();

// Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IFoodIntakeRepository, FoodIntakeRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<ICoinRepository, CoinRepository>();
builder.Services.AddScoped<ISkinRepository, SkinRepository>();
builder.Services.AddScoped<IReferralRepository, ReferralRepository>();
builder.Services.AddScoped<IMissionRepository, MissionRepository>();
builder.Services.AddScoped<ILwCoinRepository, LwCoinRepository>();

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-key-that-is-at-least-32-characters-long";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Database initialization
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        Console.WriteLine("✅ Database created successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Database error: {ex.Message}");
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fitness Tracker API v2.1.0");
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
    c.EnableFilter();
});

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ✨ ДОБАВЛЯЕМ КОРНЕВЫЕ ENDPOINTS
app.MapGet("/", () => new {
    message = "🪙 Fitness Tracker API with LW Coin System & Referrals!",
    version = "2.1.0",
    timestamp = DateTime.UtcNow,
    features = new
    {
        lwCoins = "LW Coin payment system",
        referrals = "Advanced referral system with leaderboard",
        premiumSubscription = "Unlimited usage for $8.99/month",
        freeFeatures = new[] { "Exercise tracking", "Progress archive" },
        paidFeatures = new[] { "Photo scanning", "Voice input", "Text analysis" }
    },
    endpoints = new
    {
        swagger = "/swagger",
        health = "/health",
        lwCoinBalance = "/api/lw-coin/balance",
        referralStats = "/api/referral/stats",
        pricing = "/api/lw-coin/pricing"
    }
});

app.MapGet("/health", () => new {
    status = "healthy",
    timestamp = DateTime.UtcNow,
    uptime = Environment.TickCount64,
    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
    features = new
    {
        lwCoinSystem = "active",
        referralSystem = "active",
        premiumSubscriptions = "active",
        leaderboard = "active"
    }
});

Console.WriteLine("🚀 Fitness Tracker API with LW Coin System & Referrals starting...");
Console.WriteLine($"🪙 LW Coin System: ACTIVE");
Console.WriteLine($"🎯 Referral System: ACTIVE with Leaderboard");
Console.WriteLine($"💎 Premium Subscriptions: $8.99/month");
Console.WriteLine($"🎁 Referral Rewards: 150 LW Coins");
Console.WriteLine($"📊 Swagger HTTP: http://localhost:60170/swagger");
Console.WriteLine($"📊 Swagger HTTPS: https://localhost:60169/swagger");
Console.WriteLine($"🌐 API HTTP: http://localhost:60170");
Console.WriteLine($"🌐 API HTTPS: https://localhost:60169");
Console.WriteLine($"❤️ Health: http://localhost:60170/health");

app.Run();