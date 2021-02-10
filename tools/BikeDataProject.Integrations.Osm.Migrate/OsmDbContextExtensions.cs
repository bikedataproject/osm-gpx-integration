using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BikeDataProject.Integrations.OSM.Db;
using Microsoft.EntityFrameworkCore;

namespace BikeDataProject.Integrations.Osm.Migrate
{
    internal static class OsmDbContextExtensions
    {
        public static async Task<Track?> GetEarliestTrack(this OsmDbContext db)
        {
            return await db.Tracks.
                OrderBy(x => x.OsmTrackId).FirstOrDefaultAsync();
        }

        public static async Task<List<Track>> GetTracksBefore(this OsmDbContext db,
            long trackId, int count = 100)
        {
            return await db.Tracks.Where(x => x.OsmTrackId < trackId)
                .OrderByDescending(x => x.OsmTrackId)
                .Include(x => x.User)
                .Take(count).ToListAsync();
        }
    }
}