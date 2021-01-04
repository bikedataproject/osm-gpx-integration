
namespace BikeDataProject.Integrations.OSM.Db
{
    /// <summary>
    /// Represents possible states of tracks in the OSM-API.
    /// </summary>
    public enum TrackOsmStateEnum : int
    {
        /// <summary>
        /// The track is not publicly accessible or doesn't exist yet.
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// The track is not publicly accessible.
        /// </summary>
        Private = 1,
        /// <summary>
        /// The track is publicly accessible.
        /// </summary>
        Public = 2
    }
}