using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BikeDataProject.Integrations.OSM.Db;
using FlatGeobuf.NTS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace BikeDataProject.Integrations.OSM.Dump
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _configuration;
        private readonly OsmDbContext _db;

        public Worker(ILogger<Worker> logger, IConfiguration configuration,
            OsmDbContext db)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await this.RunAsync(stoppingToken);
            
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
                
                await Task.Delay(_configuration.GetValueOrDefault<int>("refresh-time-gpx", 1000), stoppingToken);
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            try
            {
                IEnumerable<Feature> GetFeatures()
                {
                    var maxTrackCount = _configuration.GetValueOrDefault("MaxFeatures", int.MaxValue);
                    var tracks = _db.Tracks.Where(x => x.Id < maxTrackCount)
                        .Where(x => x.UserId != null).OrderBy(x => x.Id);

                    var trackCount = 0;
                    foreach (var track in tracks) {
                        trackCount++;
                        if (stoppingToken.IsCancellationRequested) break;
                        if (track.GpxFile == null) continue;

                        _logger.LogInformation(
                            $"Track {trackCount + 1}/{maxTrackCount} {track.Id}: {track.GpxFile?.Length} bytes");

                        // try compressed.
                        string? xml = null;
                        try {
                            using (var memoryStream = new MemoryStream(track.GpxFile))
                            using (var gzipStream1 = new GZipStream(memoryStream, CompressionMode.Decompress))
                            using (var gzipStream2 = new GZipStream(gzipStream1, CompressionMode.Decompress))
                            using (var streamReader = new StreamReader(gzipStream2)) {
                                xml = streamReader.ReadToEnd();
                            }
                        }
                        catch (Exception e) {

                        }

                        // try uncompressed.
                        if (string.IsNullOrWhiteSpace(xml)) {
                            try {
                                using (var memoryStream = new MemoryStream(track.GpxFile))
                                using (var gzipStream =
                                    new GZipStream(memoryStream, CompressionMode.Decompress))
                                using (var streamReader = new StreamReader(gzipStream)) {
                                    xml = streamReader.ReadToEnd();
                                }
                            }
                            catch (Exception e) {

                            }
                        }

                        // read gpx.
                        Feature[]? trackFeatures = null;
                        try {
                            if (!string.IsNullOrWhiteSpace(xml)) {
                                using (var reader = new StringReader(xml))
                                using (var xmlReader = XmlReader.Create(reader)) {
                                    (_, trackFeatures, _) =
                                        GpxReader.ReadFeatures(xmlReader, new GpxReaderSettings()
                                        {
                                            DefaultCreatorIfMissing = "OSM"
                                        }, GeometryFactory.Default);
                                }
                            }
                        }
                        catch (Exception e) {
                            _logger.LogError(e, "Unhandled exception while parsing GPX");
                        }

                        if (trackFeatures != null) {
                            foreach (var tf in trackFeatures) {
                                if (stoppingToken.IsCancellationRequested) break;
                                if (tf.Geometry is Point) continue;
                                if (tf.Geometry is Polygon) continue;

                                if (tf.Geometry is MultiLineString mls) {
                                    foreach (var g in mls) {
                                        if (g is LineString ls) {
                                            if (ls.Count >= 2) {
                                                yield return new Feature(ls, new AttributesTable()
                                                {
                                                    {"track_id", track.Id}
                                                });
                                            }
                                        }
                                    }
                                }
                                else if (tf.Geometry is LineString ls) {
                                    if (ls.Count >= 2) {
                                        yield return new Feature(tf.Geometry, new AttributesTable()
                                        {
                                            {"track_id", track.Id}
                                        });
                                    }
                                }
                            }
                        }
                        else {
                            _logger.LogWarning("Failed to parse track: {trackId} - {trackFileName}", track.Id,
                                track.GpxFileName);
                        }
                    }
                }

                var outputFile = this._configuration.GetValueOrDefault("OutputFile", "osm-gpx.fbg");
                _logger.LogDebug("Building output file: {outputFile}", outputFile);
                using var outputFileStream = File.Open(outputFile, FileMode.Create);
                FeatureCollectionConversions.Serialize(outputFileStream, GetFeatures(), FlatGeobuf.GeometryType.LineString);
                
                _logger.LogInformation("Built output file: {outputFile}", outputFile);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unhandled exception exporting GPX tracks");
            }
        }
    }
}