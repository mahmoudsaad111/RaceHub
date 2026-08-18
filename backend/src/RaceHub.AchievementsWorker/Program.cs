using Microsoft.EntityFrameworkCore;
using RaceHub.AchievementsWorker;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Infrastructure.Messaging;
using RaceHub.Infrastructure.Persistence;
using RaceHub.Infrastructure.Persistence.Repositories;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();
builder.Services.AddScoped<IRaceHistoryRepository, RaceHistoryRepository>();
builder.Services.AddScoped<IUserAchievementRepository, UserAchievementRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<ITrackRepository, TrackRepository>();
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddHostedService<AchievementsConsumer>();

await builder.Build().RunAsync();
