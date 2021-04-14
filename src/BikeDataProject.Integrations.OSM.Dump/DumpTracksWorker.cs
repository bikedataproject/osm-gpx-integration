using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using BikeDataProject.Integrations.OSM.Db;
using FlatGeobuf;
using FlatGeobuf.NTS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Feature = NetTopologySuite.Features.Feature;

namespace BikeDataProject.Integrations.OSM.Dump
{
    public class DumpTracksWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DumpTracksWorker> _logger;

        private static readonly string RefreshTimeConfig = "refresh-time";
        
        public DumpTracksWorker(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, 
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            
            _logger = loggerFactory.CreateLogger<DumpTracksWorker>();
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("{worker} running at: {time}, triggered every {refreshTime}", 
                    nameof(DumpTracksWorker), DateTimeOffset.Now, _configuration.GetValueOrDefault<int>(RefreshTimeConfig, 1000));
                
                await this.RunAsync(stoppingToken);
                
                await Task.Delay(_configuration.GetValueOrDefault<int>(RefreshTimeConfig, 1000), stoppingToken);
            }
        }

        private async Task RunAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider
                    .GetRequiredService<OsmDbContext>();
                
                IEnumerable<Feature> GetFeatures()
                {
                    var maxTrackCount = _configuration.GetValueOrDefault("MaxFeatures", int.MaxValue);
                    var tracks = db.Tracks.Where(x => x.Id < maxTrackCount)
                        .Where(x => x.UserId != null).OrderBy(x => x.Id);

                    var trackCount = 0;
                    foreach (var track in tracks) {
                        trackCount++;
                        if (stoppingToken.IsCancellationRequested) break;
                        if (track.GpxFile == null) continue;

                        _logger.LogInformation(
                            "Track {trackCount}/{maxTrackCount} {trackId}: {trackLength} bytes",
                            trackCount + 1, maxTrackCount, track.Id, track.GpxFile?.Length);
                        
                        
                        // build attributes
                        var attributes = new AttributesTable()
                        {
                            {"bdp_id", track.Id},
                            {"track_id", track.OsmTrackId},
                            {"user_id", track.UserId ?? -1},
                            {"file_name", track.GpxFileName ?? string.Empty},
                            {"timestamp", (track.OsmTimeStamp ?? DateTime.UnixEpoch).ToUnixTime()},
                            {"tags", string.Join(',', track.Tags ?? Array.Empty<string>())}
                        };

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
                                                yield return new Feature(ls, attributes);
                                            }
                                        }
                                    }
                                }
                                else if (tf.Geometry is LineString ls) {
                                    if (ls.Count >= 2) {
                                        yield return new Feature(tf.Geometry, attributes);
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

                var outputFileInfo = new FileInfo(
                    this._configuration.GetValueOrDefault("OutputFile", "osm-gpx.fbg"));
                var tempOutputFile = Path.Combine(outputFileInfo.Directory.FullName, ".osm-gpx.fbg.part");
                
                _logger.LogDebug("Building output file: {tempOutputFile}", tempOutputFile);
                await using var outputFileStream = File.Open(tempOutputFile, FileMode.Create);
                FeatureCollectionConversions.Serialize(outputFileStream, GetFeatures(), FlatGeobuf.GeometryType.LineString,
                    columns: new []
                    {
                        new ColumnMeta()
                        {
                            Name = "bdp_id",
                            Type = ColumnType.Long
                        },
                        new ColumnMeta()
                        {
                            Name = "track_id",
                            Type = ColumnType.Long
                        },
                        new ColumnMeta()
                        {
                            Name = "user_id",
                            Type = ColumnType.Int
                        },
                        new ColumnMeta()
                        {
                            Name = "file_name",
                            Type = ColumnType.String
                        },
                        new ColumnMeta()
                        {
                            Name = "timestamp",
                            Type = ColumnType.Long
                        },
                        new ColumnMeta()
                        {
                            Name = "tags",
                            Type = ColumnType.String
                        }
                    }.ToList());
                
                if (outputFileInfo.Exists) outputFileInfo.Delete();
                File.Move(tempOutputFile, outputFileInfo.FullName);
                _logger.LogDebug("Moved output file: {tempOutputFile}->{outputFile}", 
                    tempOutputFile, outputFileInfo.FullName);
                
                _logger.LogInformation("Built output file: {outputFile}", outputFileInfo.FullName);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception exporting GPX tracks");
            }
        }
    }
}