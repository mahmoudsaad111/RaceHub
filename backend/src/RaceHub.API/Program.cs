using RaceHub.API.Hubs;
using RaceHub.API.Middleware;
using RaceHub.Application;
using RaceHub.Domain.Entities;
using RaceHub.Infrastructure;
using RaceHub.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "FrontendCorsPolicy";

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<RaceHub.API.Messaging.RewardNotificationRelayService>();
builder.Services.AddHostedService<RaceHub.API.Messaging.AchievementNotificationRelayService>();
// Typed client for the RabbitMQ management HTTP API (queue-depths
// diagnostics endpoint) — SocketsHttpHandler is safe to share across
// requests, so one client registration serves every call.
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// See RaceHubUserIdProvider's doc comment — replaces SignalR's default
// ClaimTypes.NameIdentifier-based user matching (which TokenService never
// populates) with the "userId" claim every controller already relies on.
// Fixes friend presence (FriendOnline/FriendOffline) silently not reaching
// anyone.
builder.Services.AddSingleton<IUserIdProvider, RaceHubUserIdProvider>();

// Angular dev server + containerized frontend both need explicit CORS
// since the API and SPA are served from different origins.
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Checks for and applies any pending EF Core migrations on startup, so a
// fresh container (e.g. `docker compose up` against an empty SQL Server
// volume) ends up with an up-to-date schema without a manual
// `dotnet ef database update` step. Logs what it finds either way, so a
// "nothing to do" boot is just as visible in the logs as an actual migration
// run. Requires migrations to already exist in the project (see backend/
// README section on generating the initial migration) — if the Migrations
// folder is empty, GetPendingMigrations() will be empty too and this is a
// silent no-op, which is expected for a from-scratch DbContext.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();

    if (pendingMigrations.Count > 0)
    {
        logger.LogInformation(
            "Applying {Count} pending migration(s): {Migrations}",
            pendingMigrations.Count,
            string.Join(", ", pendingMigrations));

        await dbContext.Database.MigrateAsync();

        logger.LogInformation("Migrations applied successfully.");
    }
    else
    {
        logger.LogInformation("No pending migrations — database schema is up to date.");
    }

    // Single source of truth for the car catalog. The AnyAsync() gate below
    // only fires on a totally empty table; the per-name sync pass after it
    // is what keeps an existing database current — inserting cars added
    // later and re-syncing stats/prices — without ever touching ownership
    // rows (UserCars reference these cars by id, and ids are stable because
    // the sync matches on name).
    //
    // Economy design: every account starts with the five free starters
    // (Price 0 = usable by everyone without a UserCar row — see Car.Price
    // docs). Paid cars are strictly better as price rises — the average of
    // the five stats grows monotonically with price (entry ~74, sport
    // ~78-83, performance ~84-86, super ~86-89, hyper ~90-93) while each
    // car keeps a personality: speed-biased, handling-biased, nitro-heavy,
    // etc. Prices are tuned against the reward curve (~100-250 coins per
    // race): entry cars are a few races, hypercars a long-term grind.
    var carCatalog = new (string Name, int TopSpeed, int Acceleration, int Handling, int Braking, int NitroCapacity, int Price)[]
    {
        // — Free starters (all Price 0): five distinct personalities at the
        // same ~70 overall level. Balanced / speed / handling / accel /
        // nitro flavors.
        ("Speedster", 72, 70, 72, 70, 66, 0),
        ("Lightning", 79, 74, 66, 64, 69, 0),
        ("Phantom", 68, 68, 78, 76, 64, 0),
        ("Shadow", 74, 78, 68, 66, 64, 0),
        ("Thunder", 77, 73, 63, 62, 74, 0),

        // — Entry tier: hot hatches and light coupes, overall ~74-76.
        ("Mini JCW GP", 73, 76, 77, 73, 70, 600),
        ("Hyundai i30 N Performance", 75, 74, 77, 74, 70, 750),
        ("VW Golf GTI Clubsport", 76, 75, 76, 73, 70, 900),
        ("Toyota GR86", 73, 72, 84, 78, 68, 1000),
        ("Honda Civic Type R", 78, 77, 79, 74, 71, 1300),

        // — Sport tier: driver's coupes and sports sedans, overall ~78-83.
        ("Vortex", 86, 82, 74, 72, 76, 2000),
        ("Zenith", 84, 78, 82, 80, 72, 3500),
        ("BMW M2", 84, 86, 80, 76, 78, 4200),
        ("Nissan Z Performance", 86, 85, 81, 76, 78, 4400),
        ("Toyota GR Supra", 87, 85, 80, 76, 80, 4800),
        ("Audi RS3 Sportback", 84, 88, 80, 79, 78, 5000),
        ("Lexus RC F Track Edition", 86, 84, 82, 80, 77, 5400),
        ("Mercedes-AMG C63 S", 86, 88, 79, 78, 80, 5800),
        ("BMW M4 Competition", 88, 89, 80, 78, 82, 6800),

        // — Performance tier: track weapons, overall ~84-86.
        ("Chevrolet Corvette C8 Stingray", 90, 89, 81, 79, 82, 8500),
        ("Porsche Cayman GT4 RS", 88, 86, 88, 84, 80, 9500),
        ("Nissan GT-R Nismo", 91, 92, 82, 80, 84, 10500),
        ("Aston Martin Vantage", 92, 89, 86, 83, 80, 13500),
        ("Mercedes-AMG GT R", 92, 90, 83, 81, 84, 14000),

        // — Supercar tier: the six-figure exotics, overall ~86-89.
        ("Audi R8 V10 Performance", 93, 91, 82, 80, 84, 16000),
        ("McLaren 570S", 93, 90, 86, 83, 84, 18000),
        ("Porsche 911 GT3 RS", 94, 90, 89, 86, 84, 19500),
        ("Ferrari Roma", 94, 93, 86, 83, 88, 21000),
        ("Lamborghini Huracan EVO", 95, 94, 86, 83, 88, 23000),

        // — Hypercar tier: end-game grinds, overall ~90-93, best stats in
        // the game.
        ("McLaren 765LT", 96, 94, 86, 84, 89, 38000),
        ("Lamborghini Aventador SVJ", 98, 96, 84, 82, 90, 42000),
        ("Porsche 918 Spyder", 96, 95, 88, 85, 89, 46000),
        ("Ferrari SF90 Stradale", 97, 96, 87, 85, 91, 48000),
        ("Bugatti Chiron", 100, 98, 84, 82, 96, 75000),
        ("Koenigsegg Jesko", 100, 99, 86, 84, 98, 90000),
    };

    if (!await dbContext.Cars.AnyAsync())
    {
        logger.LogInformation("Seeding initial car catalog ({Count} cars).", carCatalog.Length);

        dbContext.Cars.AddRange(carCatalog.Select(c =>
            new Car(c.Name, c.TopSpeed, c.Acceleration, c.Handling, c.Braking, c.NitroCapacity, c.Price)));

        await dbContext.SaveChangesAsync();

        logger.LogInformation("Car catalog seeded successfully.");
    }
    else
    {
        // Per-name sync so cars added to the catalog later still land in an
        // existing database, and stat/price retunes are re-applied. Owned
        // UserCar rows are keyed by id and never touched — players keep
        // every car they bought, it just may drive differently after a
        // rebalance.
        var existingCars = await dbContext.Cars.ToDictionaryAsync(c => c.Name);

        var newCars = carCatalog
            .Where(c => !existingCars.ContainsKey(c.Name))
            .Select(c => new Car(c.Name, c.TopSpeed, c.Acceleration, c.Handling, c.Braking, c.NitroCapacity, c.Price))
            .ToList();

        if (newCars.Count > 0)
        {
            logger.LogInformation("Seeding {Count} new car(s): {Cars}", newCars.Count, string.Join(", ", newCars.Select(c => c.Name)));

            dbContext.Cars.AddRange(newCars);
            await dbContext.SaveChangesAsync();

            logger.LogInformation("Car catalog synced successfully.");
        }

        var updatedCars = 0;
        foreach (var entry in carCatalog)
        {
            if (existingCars.TryGetValue(entry.Name, out var car) &&
                (car.Price != entry.Price ||
                 car.TopSpeed != entry.TopSpeed ||
                 car.Acceleration != entry.Acceleration ||
                 car.Handling != entry.Handling ||
                 car.Braking != entry.Braking ||
                 car.NitroCapacity != entry.NitroCapacity))
            {
                car.SetStats(entry.TopSpeed, entry.Acceleration, entry.Handling, entry.Braking, entry.NitroCapacity);
                car.SetPrice(entry.Price);
                updatedCars++;
            }
        }

        if (updatedCars > 0)
        {
            logger.LogInformation("Syncing stats/prices for {Count} existing car(s).", updatedCars);
            await dbContext.SaveChangesAsync();
        }
    }

    // Per-name existence check (not AnyAsync()) so a new track added later
    // — like Beach Track — still gets inserted into an existing database
    // instead of being silently skipped forever because *some* tracks
    // already existed.
    var existingTrackNames = await dbContext.Tracks.Select(t => t.Name).ToListAsync();

    var trackCatalog = new (string Name, string Description, int TotalLaps, int Difficulty)[]
    {
        ("Classic Track", "A welcoming circuit with broad sweeping curves — the perfect place to learn the racing line.", 3, 1),
        ("Desert Track", "Long straights through the dunes snap into brutal high-speed corners — only the brave floor it here.", 3, 2),
        ("Beach Track", "Fast, flowing coastal esses — sustained lateral g-force tests your nerve and your line.", 3, 2),
        ("City Track", "Tight urban chicanes and quick direction changes — one mistake and you're in the wall.", 4, 3),
        ("Forest Track", "The ultimate technical challenge: narrow, twisty, and merciless — only masters survive here.", 4, 4),
    };

    var newTracks = trackCatalog
        .Where(t => !existingTrackNames.Contains(t.Name))
        .Select(t => new Track(t.Name, t.Description, totalLaps: t.TotalLaps, difficulty: t.Difficulty))
        .ToList();

    if (newTracks.Count > 0)
    {
        logger.LogInformation("Seeding {Count} new track(s): {Tracks}", newTracks.Count, string.Join(", ", newTracks.Select(t => t.Name)));

        dbContext.Tracks.AddRange(newTracks);

        await dbContext.SaveChangesAsync();

        logger.LogInformation("Track catalog synced successfully.");
    }

    // Separate guard from the track-catalog seed above: an existing DB
    // volume from before checkpoint geometry existed would have tracks but
    // no checkpoints. This re-syncs checkpoints for every named track on
    // every startup (delete + re-insert) rather than a one-time "only if
    // empty" guard, specifically so tuning track size/shape (radius,
    // checkpoint count, width) takes effect on the next `docker compose up`
    // without requiring a full DB wipe.
    {
        logger.LogInformation("Syncing track checkpoint geometry.");

        // Canvas coordinate space is 3000x1700 (see RaceComponent) with the
        // arena centered at (1500, 850). Each track is a closed loop built
        // from a base ellipse plus a sum of angular "wobble" harmonics
        // (frequency, amplitude, phase, power) that perturb the radius as
        // you go around. Integer frequencies guarantee the loop still
        // closes smoothly (periodic over the same 0-2π sweep). Power
        // reshapes each harmonic's wave: 1.0 is a plain, round sine bend;
        // values under 1.0 (via sign(sin)*|sin|^power) sharpen it toward a
        // squarer wave — faster radius transitions that read as actual
        // corners rather than gentle bends.
        //
        // See the geometryByTrackName array below for the current tuning
        // pass and why these numbers are what they are — kept there
        // instead of duplicated here to avoid the two comments drifting
        // out of sync with each other the way they previously drifted out
        // of sync with the actual canvas size.
        //
        // Width doubles as that track's road half-width for fence
        // rendering/collision on the client. Checkpoint 0 doubles as the
        // start/finish line.
        var geometryByTrackName = new (string TrackName, int Count, double RadiusX, double RadiusY, decimal Width, (double Frequency, double Amplitude, double Phase, double Power)[] Wobble)[]
        {
            // Canvas is 3000x1700, centered at (1500, 850) — see
            // GenerateOvalCheckpoints below and RaceComponent's
            // CANVAS_WIDTH/CANVAS_HEIGHT, which this must always match.
            // (Found this drifted stale twice over: this comment previously
            // claimed 2450x1400 while a *different* comment on
            // GenerateOvalCheckpoints itself claimed 2200x1300 — neither
            // matched the real 3000x1700 canvas or each other. The actual
            // GenerateOvalCheckpoints center constants were still hardcoded
            // to the 2450x1400-era (1225, 700), which is the real bug this
            // fixes: with the canvas already having grown twice without a
            // matching pass here, every track was centered off and sized
            // for a canvas ~20% smaller than the one it's actually drawn
            // on, leaving real headroom unused — directly why tracks felt
            // short of what the space allowed.
            //
            // "Longer" pass: every radius scaled up by *exactly* the ratio
            // the canvas grew by since these were last tuned (x1.2245,
            // y1.2143, from 2450x1400 -> 3000x1700) rather than re-derived
            // from a fresh margin/fold-over budget calculation — a uniform
            // scale-up of geometry that already rendered safely (no
            // clipping, no fence self-intersection) preserves every one of
            // those safety properties exactly, since fold-over depends only
            // on amplitude sum (unchanged by this scale-up) and edge
            // clearance scales by the same ratio as the canvas itself.
            // Checkpoint counts scaled up by the same ~1.2x so polyline
            // resolution through the corners doesn't get coarser as the
            // physical track grows.
            //
            // "Harder" pass: road WIDTH deliberately did *not* scale up
            // with the radii — same absolute width on a physically bigger,
            // faster track means proportionally less margin for error, not
            // more. On top of that, every wobble amplitude went up ~15-20%
            // (power nudged down to match, for a sharper transition into
            // each bend) while staying comfortably under the ~0.4 self-fold
            // ceiling — Forest, the highest, sits at 0.33.
            //   - Classic: still the beginner circuit (lowest amplitude,
            //     widest road relative to its size) but now a real
            //     double-bend, not a near-oval.
            //   - Desert: widest/fastest track, square harmonics for long
            //     straights snapping into hard ~90° corners.
            //   - Beach: sustained mid-frequency esses at 3+5.
            //   - City: freq 5+7 low-power flicks — tight chicanes.
            //   - Forest: narrowest road, three sharpened incommensurate
            //     harmonics (3/5/7) — the technical maze, minimal straight-
            //     line rest.
            ("Classic Track", 38, 1322, 590, 280, new[] { (2.0, 0.14, 0.3, 0.78) }),
            ("Desert Track", 44, 918, 389, 340, new[] { (2.0, 0.14, 0.5, 0.58), (4.0, 0.08, 1.2, 0.72) }),
            ("Beach Track", 48, 894, 407, 300, new[] { (3.0, 0.14, 0.3, 0.78), (5.0, 0.08, 2.0, 0.75) }),
            ("City Track", 52, 881, 401, 270, new[] { (5.0, 0.12, 0.6, 0.62), (7.0, 0.09, 3.1, 0.68) }),
            ("Forest Track", 58, 869, 389, 250, new[] { (3.0, 0.15, 0.1, 0.68), (5.0, 0.10, 2.4, 0.65), (7.0, 0.08, 5.0, 0.65) }),
        };

        var tracksByName = await dbContext.Tracks.ToDictionaryAsync(t => t.Name, cancellationToken: default);

        foreach (var (trackName, count, radiusX, radiusY, width, wobble) in geometryByTrackName)
        {
            if (!tracksByName.TryGetValue(trackName, out var track))
            {
                continue;
            }

            var existing = await dbContext.TrackCheckpoints
                .Where(c => c.TrackId == track.Id)
                .ToListAsync();

            if (existing.Count > 0)
            {
                dbContext.TrackCheckpoints.RemoveRange(existing);
            }

            dbContext.TrackCheckpoints.AddRange(GenerateOvalCheckpoints(track.Id, count, radiusX, radiusY, width, wobble));
        }

        await dbContext.SaveChangesAsync();

        logger.LogInformation("Track checkpoint geometry synced successfully.");
    }
}

// Parametric ellipse centered at (1225, 700) — canvas coordinate space is
// 2450x1400 (see RaceComponent's CANVAS_WIDTH/HEIGHT) — perturbed by a sum
// of angular "wobble" harmonics so the path bends in and out rather than
// staying a smooth oval; checkpoint 0 sits at the top (12 o'clock) and
// sequence increases clockwise, matching how RaceComponent walks the
// checkpoint list to draw the track path and advance cars along it. Each
// harmonic is (frequency, amplitude, phase, power):
//   - frequency: integer, keeps the wobble periodic over one full 0-2π
//     lap so the loop still closes without a seam.
//   - amplitude: fraction of the base radius (keep the sum comfortably
//     under ~0.4 or bends can get sharp enough to fold the road over itself).
//   - phase: rotates where in the lap this harmonic's bends land, mainly
//     so different tracks' wobbles don't all peak at the same angle.
//   - power: reshapes the wave via sign(sin)*|sin|^power. 1.0 is a plain
//     sine (smooth, round bend). Under 1.0 sharpens it toward a squarer
//     wave — faster transitions, reads as a sharper corner.
// An empty harmonics array reproduces the original plain oval exactly.
static List<TrackCheckpoint> GenerateOvalCheckpoints(
    Guid trackId, int count, double radiusX, double radiusY, decimal width,
    (double Frequency, double Amplitude, double Phase, double Power)[]? wobble = null)
{
    const double centerX = 1225;
    const double centerY = 700;

    var checkpoints = new List<TrackCheckpoint>();

    for (var i = 0; i < count; i++)
    {
        var angle = (i / (double)count * 2 * Math.PI) - (Math.PI / 2);

        var wobbleFactor = 1.0;
        if (wobble is not null)
        {
            foreach (var (frequency, amplitude, phase, power) in wobble)
            {
                var raw = Math.Sin(frequency * angle + phase);
                var shaped = Math.Sign(raw) * Math.Pow(Math.Abs(raw), power);
                wobbleFactor += amplitude * shaped;
            }
        }

        var x = centerX + (radiusX * wobbleFactor * Math.Cos(angle));
        var y = centerY + (radiusY * wobbleFactor * Math.Sin(angle));

        checkpoints.Add(new TrackCheckpoint(trackId, i, (decimal)x, (decimal)y, width));
    }

    return checkpoints;
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// First in the pipeline so it can catch anything thrown downstream,
// including FluentValidation failures from the MediatR pipeline.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapHub<RaceHub.API.Hubs.RaceHub>("/hubs/race");

app.MapHub<RaceHub.API.Hubs.ChatHub>("/hubs/chat");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous();

app.Run();
