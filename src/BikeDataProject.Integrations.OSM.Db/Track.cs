using System;

namespace BikeDataProject.Integrations.OSM.Db
{
    /// <summary>
    /// Represents a track in the OSM-API.
    /// </summary>
    public class Track
    {
        /// <summary>
        /// The id.
        /// </summary>
        public long Id { get; set; }
        
        /// <summary>
        /// The ID in the OSM-API.
        /// </summary>
        public long OsmTrackId { get; set; }   
        
        /// <summary>
        /// The timestamp of the track in OSM. 
        /// </summary>
        public DateTime? OsmTimeStamp { get; set; }
        
        /// <summary>
        /// The id of the user this contribution is for.
        /// </summary>
        public int? UserId { get; set; }
        
        /// <summary>
        /// The user this contribution is for.
        /// </summary>
        public User? User { get; set; }
        
        /// <summary>
        /// The id of the contribution in the bike data project db.
        /// </summary>
        public int? BikeDataProjectId { get; set; }
        
        /// <summary>
        /// The is cyclists flag.
        /// </summary>
        public bool? IsCyclist { get; set; }
        
        /// <summary>
        /// The track OSM-API status.
        /// </summary>
        public TrackOsmStateEnum OsmState { get; set; }
        
        /// <summary>
        /// The track synchronization state.
        /// </summary>
        public TrackSyncStateEnum SyncState { get; set; }
        
        /// <summary>
        /// The date-time this track was synchronized.
        /// </summary>
        public DateTime? SyncTimeStamp { get; set; }
        
        /// <summary>
        /// The tags.
        /// </summary>
        public string[]? Tags { get; set; }
        
        /// <summary>
        /// The name.
        /// </summary>
        public string? GpxFileName { get; set; }
        
        /// <summary>
        /// The raw gpx data.
        /// </summary>
        public byte[]? GpxFile { get; set; }
        
        /// <summary>
        /// The gpx content type.
        /// </summary>
        public string? GpxContentType { get; set; }

        public override string ToString()
        {
            return $"Track[{this.Id}] OsmId:{this.OsmTrackId}, UserId: {this.UserId}";
        }
    }
}