using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BikeDataProject.Integrations.OSM.Db;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OsmSharp.API;
using OsmSharp.IO.API;

namespace BikeDataProject.Integrations.OSM.Workers
{
    public class QueryPublicTracksWorker : BackgroundService
    {
        private readonly ILogger<QueryPublicTracksWorker> _logger;
        private readonly IConfiguration _configuration;
        private readonly OsmDbContext _db;

        public QueryPublicTracksWorker(ILogger<QueryPublicTracksWorker> logger, IConfiguration configuration,
            OsmDbContext db)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("{worker} running at: {time}, triggered every {refreshTime}", 
                    nameof(QueryPublicTracksWorker), DateTimeOffset.Now, _configuration.GetValueOrDefault<int>("query-batch-wait-time", 1000));

                var doSync = _configuration.GetValueOrDefault("QUERY_TRACKS", true);
                if (!doSync)
                {
                    await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time", 1000), stoppingToken);
                    continue;
                }

                await this.RunAsync(stoppingToken);
                
                await Task.Delay(_configuration.GetValueOrDefault<int>("query-batch-wait-time", 1000), stoppingToken);
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
                
                // get current synchronization state.
                var latestPublic = await _db.GetLatestPublic();
                var latestPublicId = latestPublic?.OsmTrackId ?? -1L;
                var latestUnknown = await _db.GetLatestUnknown();
                var latestUnknownId = latestUnknown?.OsmTrackId ?? 0L;
                
                // make sure last unknown is after.
                if (latestPublicId > latestUnknownId) latestUnknownId = latestPublicId + 1;
                
                // increase the range.
                latestUnknownId += 10000;
                
                // start from 0 and work upwards.
                // get all public tracks until one with tag cycling is encountered.
            
                // synchronization algorithm:
                // every x-minutes try to access traces in range: [last public id -> last public id + average range]
                // if success : update last public id.
                // if not success: increase range for next try.
                
                // loop over the tracks.
                for (var osmId = latestPublicId + 1; osmId <= latestUnknownId; osmId++)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    
                    GpxFile? track = null;
                    try
                    {
                        track = await client.GetTraceDetails(osmId);
                    }
                    catch (Exception e)
                    {
                        track = null;
                    }
                    
                    if (track != null)
                    {
                        var user = await _db.GetOrCreateUser(track.User);

                        await _db.GetOrCreatePublicTrack(user, osmId, track.TimeStamp, track.Name, track.Tags);

                        for (var p = latestPublicId + 1; p < osmId; p++)
                        {
                            await _db.SetToPrivateIfUnknown(p);
                        }

                        latestPublicId = osmId;
                        
                        _logger.LogInformation($"Found public track: {osmId}");
                    }
                    else
                    {
                        await _db.GetOrCreateUnknownTrack(osmId);
                        
                        _logger.LogInformation($"Assuming private track: {osmId}");
                    }

                    await Task.Delay(_configuration.GetValueOrDefault<int>("query-wait-time", 1000), stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unhandled exception while querying OSM-API.");
            }
        }
    }
}