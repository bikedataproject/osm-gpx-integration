
namespace BikeDataProject.Integrations.OSM.Db
{
    /// <summary>
    /// Represents different synchronization statuses.
    /// </summary>
    public enum TrackSyncStateEnum : int
    {
        /// <summary>
        /// The track has not been synchronized.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// The track has been synchronized.
        /// </summary>
        Synchronized = 1,
        /// <summary>
        /// The track has been processed for synchronization but wasn't suitable.
        /// </summary>
        Unsuitable = 2,
        /// <summary>
        /// An error occured during sync.
        /// </summary>
        Error = 3,
        /// <summary>
        /// The gpx data has been synced.
        /// </summary>
        GpxSynced = 4
    }
}