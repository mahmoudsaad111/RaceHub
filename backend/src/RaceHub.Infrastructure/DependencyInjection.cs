using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RaceHub.Application.Interfaces;
using RaceHub.Application.Interfaces.Authentication;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Authentication;
using RaceHub.Infrastructure.Messaging;
using RaceHub.Infrastructure.Persistence;
using RaceHub.Infrastructure.Persistence.Repositories;
using RaceHub.Infrastructure.Realtime;

namespace RaceHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentityCore<User>(options =>
        {
            options.User.RequireUniqueEmail = true;

            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "JWT configuration is missing.");

        // Google sign-in here is ID-token based, not the ASP.NET Core
        // external-OAuth redirect/cookie flow: the Angular client uses
        // Google Identity Services to get an ID token directly from Google,
        // then POSTs it to POST /api/auth/google, where GoogleAuthService
        // verifies it. That fits an API + SPA architecture better than a
        // server-side redirect flow, which needs a cookie scheme we don't
        // otherwise use since everything else is JWT bearer auth.
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme =
                JwtBearerDefaults.AuthenticationScheme;

            options.DefaultChallengeScheme =
                JwtBearerDefaults.AuthenticationScheme;
        })
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

                ClockSkew = TimeSpan.Zero
            };

            // Important for SignalR
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];

                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/hubs/race") || path.StartsWithSegments("/hubs/chat")))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        services.AddHttpContextAccessor();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
        services.AddScoped<ICarRepository, CarRepository>();
        services.AddScoped<IRaceResultRepository, RaceResultRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IRaceRepository, RaceRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<ITrackRepository, TrackRepository>();
        services.AddScoped<IPlayerStatisticsRepository, PlayerStatisticsRepository>();
        services.AddScoped<IRaceHistoryRepository, RaceHistoryRepository>();
        services.AddScoped<IUserCarRepository, UserCarRepository>();
        services.AddScoped<IUserRewardRepository, UserRewardRepository>();
        services.AddScoped<IUserAchievementRepository, UserAchievementRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPresenceTracker, InMemoryPresenceTracker>();
        services.AddRabbitMqMessaging(configuration);
        services.AddHostedService<RabbitMqTopologyHostedService>();
        services.AddHostedService<OutboxPublisherService>();
        services.AddSignalR();
        // RewardNotificationRelayService is registered in RaceHub.API's
        // Program.cs, not here — it needs IHubContext<RaceHub>, which is an
        // API-layer type. Infrastructure doesn't (and shouldn't) reference
        // the API project, so it can't be wired up from this method.

        return services;
    }
}
