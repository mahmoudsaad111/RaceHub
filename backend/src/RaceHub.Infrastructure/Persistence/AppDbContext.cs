using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure.Messaging;

namespace RaceHub.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<TrackCheckpoint> TrackCheckpoints => Set<TrackCheckpoint>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<RacePlayer> RacePlayers => Set<RacePlayer>();
    public DbSet<Lap> Laps => Set<Lap>();
    public DbSet<RaceResult> RaceResults => Set<RaceResult>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<PlayerStatistics> PlayerStatistics => Set<PlayerStatistics>();
    public DbSet<RaceHistoryEntry> RaceHistoryEntries => Set<RaceHistoryEntry>();
    public DbSet<UserCar> UserCars => Set<UserCar>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}