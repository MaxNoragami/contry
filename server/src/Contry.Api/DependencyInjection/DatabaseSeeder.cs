using Contry.Application.Ranked;
using Contry.Domain.Ranked;
using Contry.Domain.Users;
using Contry.Infrastructure.Configuration;
using Contry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Contry.Api.DependencyInjection;

public static class DatabaseSeeder
{
    private const string SeedPassword = "Test1234!";

    private static readonly (string Username, string Email)[] FakePlayers =
    [
        ("alice_w", "alice@test.dev"),
        ("bob_builder", "bob@test.dev"),
        ("charlie_geo", "charlie@test.dev"),
        ("diana_maps", "diana@test.dev"),
        ("echo_quiz", "echo@test.dev"),
        ("frank_atlas", "frank@test.dev"),
        ("grace_world", "grace@test.dev"),
        ("hank_globe", "hank@test.dev"),
    ];

    private static readonly string[] CountryIds =
    [
        "FR", "DE", "BR", "JP", "AU", "CA", "IN", "MX",
        "IT", "ES", "GB", "AR", "EG", "ZA", "KR", "NG",
        "RU", "CN", "US", "TR", "CO", "SE", "NO", "PL"
    ];

    private static readonly string[] DefaultClueIds =
    [
        "hemisphere", "continent", "temperature_avg_c", "population", "coordinates"
    ];

    public static async Task EnsureAdminUserAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContryDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ContryDbContext>>();
        var adminOptions = scope.ServiceProvider.GetRequiredService<IOptions<AdminBootstrapOptions>>().Value;

        var existingAdmin = await dbContext.Users
            .AnyAsync(user => user.NormalizedUsername == adminOptions.Username.ToUpperInvariant(), CancellationToken.None);

        if (!existingAdmin)
        {
            dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = adminOptions.Username,
                NormalizedUsername = adminOptions.Username.ToUpperInvariant(),
                Email = adminOptions.Email,
                NormalizedEmail = adminOptions.Email.ToUpperInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminOptions.Password),
                Role = UserRole.Admin,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });

            await dbContext.SaveChangesAsync();
            logger.LogInformation("Created bootstrap admin user '{Username}'.", adminOptions.Username);
        }
    }

    public static async Task SeedDevelopmentDataAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContryDbContext>();
        var rankedDatasetProvider = scope.ServiceProvider.GetRequiredService<IRankedDatasetProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ContryDbContext>>();

        // Only seed fake players if no users exist yet beyond the bootstrap admin.
        var userCount = await dbContext.Users.CountAsync();
        if (userCount > 1)
        {
            logger.LogInformation("Database already seeded ({UserCount} users), skipping fake player seeding.", userCount);
            return;
        }

        logger.LogInformation("Seeding development database with fake players...");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(SeedPassword);
        var random = new Random(42); // Deterministic for reproducibility
        var now = DateTimeOffset.UtcNow;

        // 1. Create fake users
        var users = new List<User>();
        foreach (var (username, email) in FakePlayers)
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = passwordHash,
                Role = UserRole.User,
                CreatedAtUtc = now.AddDays(-random.Next(30, 90)),
            };
            users.Add(user);
            dbContext.Users.Add(user);
        }

        // 2. Create past challenges (last 14 days)
        var challenges = new List<RankedChallenge>();
        for (int dayOffset = 14; dayOffset >= 0; dayOffset--)
        {
            var challengeDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-dayOffset));
            var challengeDefinition = await rankedDatasetProvider.GetChallengeDefinitionAsync(challengeDate, CancellationToken.None);

            var challenge = new RankedChallenge
            {
                Id = Guid.NewGuid(),
                ChallengeDateUtc = challengeDate,
                TargetCountryId = challengeDefinition.TargetCountryId,
                ClueSetJson = System.Text.Json.JsonSerializer.Serialize(challengeDefinition.Clues),
                CreatedAtUtc = now.AddDays(-dayOffset),
            };
            challenges.Add(challenge);
            dbContext.RankedChallenges.Add(challenge);
        }

        // 3. Create sessions, guesses, and stats for each user
        foreach (var user in users)
        {
            var playedCount = 0;
            var wonCount = 0;
            var totalGuessesOnWins = 0;
            int? fastestWin = null;
            int? slowestWin = null;
            var currentStreak = 0;
            var bestStreak = 0;
            var distribution = new Dictionary<string, int>();
            var discoveredCountries = new HashSet<string>();

            // Each user plays a random subset of past challenges
            var challengesToPlay = challenges
                .Where(_ => random.NextDouble() > 0.2) // ~80% participation
                .ToList();

            foreach (var challenge in challengesToPlay)
            {
                var won = random.NextDouble() > 0.15; // ~85% win rate
                var guessCount = won ? random.Next(1, 8) : random.Next(3, 10);

                var session = new RankedSession
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    RankedChallengeId = challenge.Id,
                    Status = won ? RankedSessionStatus.Won : RankedSessionStatus.Lost,
                    GuessCount = guessCount,
                    StartedAtUtc = challenge.CreatedAtUtc.AddHours(random.Next(1, 18)),
                    CompletedAtUtc = challenge.CreatedAtUtc.AddHours(random.Next(1, 18)).AddMinutes(random.Next(1, 15)),
                };

                // Create guesses for the session - ensure no duplicate countries per session
                var sessionGuessedCountries = new HashSet<string>();
                for (int attempt = 1; attempt <= guessCount; attempt++)
                {
                    var isCorrect = won && attempt == guessCount;
                    string guessCountryId;

                    if (isCorrect)
                    {
                        guessCountryId = challenge.TargetCountryId;
                    }
                    else
                    {
                        // Pick a random country not yet guessed in this session and not the target
                        do
                        {
                            guessCountryId = CountryIds[random.Next(CountryIds.Length)];
                        } while (sessionGuessedCountries.Contains(guessCountryId) || guessCountryId == challenge.TargetCountryId);
                    }
                    sessionGuessedCountries.Add(guessCountryId);

                    var guess = new RankedGuess
                    {
                        Id = Guid.NewGuid(),
                        RankedSessionId = session.Id,
                        AttemptNumber = attempt,
                        GuessCountryId = guessCountryId,
                        GuessCountryName = guessCountryId, // Simplified for seed
                        ResultsJson = "[]",
                        CreatedAtUtc = session.StartedAtUtc.AddSeconds(attempt * 30),
                    };
                    dbContext.Set<RankedGuess>().Add(guess);
                }

                dbContext.Set<RankedSession>().Add(session);

                playedCount++;
                if (won)
                {
                    wonCount++;
                    totalGuessesOnWins += guessCount;
                    fastestWin = fastestWin == null ? guessCount : Math.Min(fastestWin.Value, guessCount);
                    slowestWin = slowestWin == null ? guessCount : Math.Max(slowestWin.Value, guessCount);
                    currentStreak++;
                    bestStreak = Math.Max(bestStreak, currentStreak);
                    discoveredCountries.Add(challenge.TargetCountryId);

                    var bucket = guessCount >= 10 ? "10+" : guessCount.ToString();
                    distribution[bucket] = distribution.GetValueOrDefault(bucket, 0) + 1;
                }
                else
                {
                    currentStreak = 0;
                }
            }

            // 4. Create RankedUserStats
            var stats = new RankedUserStats
            {
                UserId = user.Id,
                PlayedCount = playedCount,
                WonCount = wonCount,
                TotalGuessesOnWins = totalGuessesOnWins,
                FastestWinGuessCount = fastestWin,
                SlowestWinGuessCount = slowestWin,
                CurrentStreak = currentStreak,
                BestStreak = bestStreak,
                GuessDistributionJson = System.Text.Json.JsonSerializer.Serialize(distribution),
            };
            dbContext.Set<RankedUserStats>().Add(stats);

            // 5. Create CountryDiscoveryStats
            foreach (var countryId in discoveredCountries)
            {
                var discoveryCount = random.Next(1, 4);
                dbContext.Set<RankedCountryDiscoveryStat>().Add(new RankedCountryDiscoveryStat
                {
                    UserId = user.Id,
                    CountryId = countryId,
                    Discovered = true,
                    BestAttempts = random.Next(1, 6),
                    SolvedCount = discoveryCount,
                    LastSolvedAtUtc = now.AddDays(-random.Next(0, 14)),
                });
            }

            // 6. Create ClueUsageStats
            foreach (var clueId in DefaultClueIds)
            {
                dbContext.Set<RankedClueUsageStat>().Add(new RankedClueUsageStat
                {
                    UserId = user.Id,
                    ClueId = clueId,
                    UsageCount = playedCount + random.Next(-2, 3),
                });
            }
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {UserCount} fake players with ranked data.", users.Count);
    }
}
