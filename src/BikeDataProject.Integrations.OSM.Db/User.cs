using System.Collections.Generic;

namespace BikeDataProject.Integrations.OSM.Db
{
    /// <summary>
    /// Represents an OSM user, linked to a bike data project user.
    /// </summary>
    public class User
    {
        /// <summary>
        /// The id.
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// The OSM user.
        /// </summary>
        public string OsmUser { get; set; }
        
        /// <summary>
        /// The tracks associated with this user.
        /// </summary>
        public List<Track> Tracks { get; set; }
    }
}