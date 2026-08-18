using Microsoft.EntityFrameworkCore;
using RaceHub.Application.Interfaces.Persistence;
using RaceHub.Infrastructure.Messaging;
using RaceHub.Infrastructure.Persistence;
using RaceHub.Infrastructure.Persistence.Repositories;
using RaceHub.StatisticsWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();
builder.Services.AddScoped<IRaceHistoryRepository, RaceHistoryRepository>();
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddHostedService<StatisticsConsumer>();

await builder.Build().RunAsync();