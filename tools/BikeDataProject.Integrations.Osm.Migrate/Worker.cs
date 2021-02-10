using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.OSM.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BikeDataProject.Integrations.Osm.Migrate
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("{worker} running at: {time}, triggered every {refreshTime}", 
                    nameof(Worker), DateTimeOffset.Now, _configuration.GetValueOrDefault<int>("refresh-time-gpx", 1000));

                var doSync = _configuration.GetValueOrDefault("QUERY_TRACKS", true);
                if (!doSync)
                {
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time-gpx", 1000), stoppingToken);
                    continue;
                }

                await this.RunAsync(stoppingToken);
                
                await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time-gpx", 1000), stoppingToken);
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            try {
                var sourceDbConnectionString = await File.ReadAllTextAsync(_configuration["SOURCE_DB"]);
                var sourceDbOptionsBuilder = new DbContextOptionsBuilder<OsmDbContext>();
                sourceDbOptionsBuilder.UseNpgsql(sourceDbConnectionString);
                var sourceDb = new OsmDbContext(sourceDbOptionsBuilder.Options);
                
                var targetDbConnectionString = await File.ReadAllTextAsync(_configuration["TARGET_DB"]);
                var targetDbOptionsBuilder = new DbContextOptionsBuilder<OsmDbContext>();
                targetDbOptionsBuilder.UseNpgsql(targetDbConnectionString);
                var targetDb = new OsmDbContext(targetDbOptionsBuilder.Options);

                var earliestTrack = await targetDb.GetEarliestTrack();
                if (earliestTrack == null) return;

                var earlierTracks = await sourceDb.GetTracksBefore(earliestTrack.OsmTrackId);
                
                // migrate all users.
                var users = new Dictionary<long, User>();
                foreach (var earlierTrack in earlierTracks) {
                    if (earlierTrack.User == null) continue;
                    
                    if (users.TryGetValue(earlierTrack.User.Id, out var user)) continue;

                    user = await targetDb.GetForOsmUser(earlierTrack.User.OsmUser);
                    if (user == null) {
                        // user doesn't exist, create it.
                        user = new User()
                        {
                            OsmUser = earlierTrack.User.OsmUser
                        };
                        
                        await targetDb.Users.AddAsync(user);
                        await targetDb.SaveChangesAsync();
                    }
                    
                    users[earlierTrack.User.Id] = user;
                }

                foreach (var earlierTrack in earlierTracks) {
                    int? userId = null;
                    if (earlierTrack.User != null) {
                        if (!users.TryGetValue(earlierTrack.User.Id, out var user)) {
                            throw new Exception("User exists but was not migrated.");
                        }

                        userId = user.Id;
                    }

                    await targetDb.Tracks.AddAsync(new Track()
                    {
                        Tags = earlierTrack.Tags,
                        UserId = userId,
                        GpxFile = earlierTrack.GpxFile,
                        IsCyclist = earlierTrack.IsCyclist,
                        OsmState = earlierTrack.OsmState,
                        SyncState = earlierTrack.SyncState,
                        GpxContentType = earlierTrack.GpxContentType,
                        GpxFileName = earlierTrack.GpxFileName,
                        OsmTimeStamp = earlierTrack.OsmTimeStamp,
                        OsmTrackId = earlierTrack.OsmTrackId,
                        SyncTimeStamp = earlierTrack.SyncTimeStamp,
                        BikeDataProjectId = earlierTrack.BikeDataProjectId
                    });
                }
                
                await targetDb.SaveChangesAsync();
                
                _logger.LogInformation("Migrated tracks: [{trackFrom},{trackTo}]",
                    earlierTracks[0].OsmTrackId, earlierTracks[^1].OsmTrackId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception exporting GPX tracks");
                throw;
            }
        }
    }
}