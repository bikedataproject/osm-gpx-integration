using Microsoft.EntityFrameworkCore;

namespace BikeDataProject.Integrations.OSM.Db
{
    public class OsmDbContext : DbContext
    {
        public OsmDbContext(DbContextOptions<OsmDbContext> options) : base(options)
        {
            
        }
        
        public DbSet<Track> Tracks { get; set; }
        public DbSet<User> Users { get; set; }
    }
}