using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.OSM.Db;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsmSharp.IO.API;

namespace BikeDataProject.Integrations.OSM.Workers
{
    public class SynchronizeGpxWorker : BackgroundService
    {
        private readonly ILogger<SynchronizeGpxWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly OsmDbContext _db;
        private readonly DB.BikeDataDbContext _contributionsDb;

        public SynchronizeGpxWorker(ILogger<SynchronizeGpxWorker> logger, IConfiguration configuration,
            OsmDbContext db, DB.BikeDataDbContext contributionsDb)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
            _contributionsDb = contributionsDb;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("{worker} running at: {time}, triggered every {refreshTime}", 
                    nameof(QueryPublicTracksWorker), DateTimeOffset.Now, _configuration.GetValueOrDefault<int>("refresh-time-gpx", 1000));

                var doSync = _configuration.GetValueOrDefault("SYNC_GPX", true);
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
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "BikeDataProject");
                var clientFactory = new ClientsFactory(null, httpClient, _configuration["OSM_API"]);

                // get client credentials.
                var userName = await File.ReadAllTextAsync(_configuration["OSM_USER_ID"]);
                var userPass = await File.ReadAllTextAsync(_configuration["OSM_USER_PASS"]);
                var client = clientFactory.CreateBasicAuthClient(userName, userPass);
                
                // get unsynced track.
                var unSyncedTrack = await _db.GetUnSyncedPublicTrack();
                if (unSyncedTrack == null) return;
                
                // sync gpx track.
                try
                {
                    var track = await client.GetTraceData(unSyncedTrack.OsmTrackId);

                    using (var memoryStream = new MemoryStream())
                    using (var gzipStream = new GZipStream(memoryStream, CompressionLevel.Optimal))
                    {
                        await track.Stream.CopyToAsync(gzipStream);

                        await gzipStream.FlushAsync();
                        await gzipStream.DisposeAsync();
                        
                        unSyncedTrack.GpxFile = memoryStream.ToArray();
                    }
                    unSyncedTrack.GpxContentType = track.ContentType.MediaType;
                    unSyncedTrack.SyncState = TrackSyncStateEnum.GpxSynced;
                    
                    _db.Tracks.Update(unSyncedTrack);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation($"Synchronized GPX for public track: {unSyncedTrack.OsmTrackId}");
                }
                catch (Exception e)
                {
                    unSyncedTrack.SyncState = TrackSyncStateEnum.Error;
                    _db.Tracks.Update(unSyncedTrack);
                    await _db.SaveChangesAsync();
                    
                    _logger.LogWarning(e, $"Failed to get public track: {unSyncedTrack.OsmTrackId}");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unhandled exception while synchronizing GPX track.");
            }
        }
    }
}