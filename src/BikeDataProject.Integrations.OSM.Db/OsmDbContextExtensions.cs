using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BikeDataProject.Integrations.OSM.Db
{
    public static class OsmDbContextExtensions
    {
        public static async Task<Track?> GetLatestPublic(this OsmDbContext db)
        {
            return await db.Tracks.Where(w => w.OsmState == TrackOsmStateEnum.Public).
                OrderByDescending(x => x.OsmTrackId).FirstOrDefaultAsync();
        }

        public static async Task<Track?> GetLatestUnknown(this OsmDbContext db)
        {
            return await db.Tracks.Where(w => w.OsmState == TrackOsmStateEnum.Unknown).
                OrderByDescending(x => x.OsmTrackId).FirstOrDefaultAsync();
        }

        public static async Task<User?> GetForOsmUser(this OsmDbContext db, string osmUser)
        {
            return await db.Users.Where(w => w.OsmUser == osmUser)
                .FirstOrDefaultAsync();
        }

        public static async Task<User> GetOrCreateUser(this OsmDbContext db, string osmUser)
        {
            var existing = await db.GetForOsmUser(osmUser);
            if (existing != null) return existing;

            existing = new User()
            {
                OsmUser = osmUser
            };
            await db.Users.AddAsync(existing);
            await db.SaveChangesAsync();
            return existing;
        }

        public static async Task<Track?> GetForOsmTrackId(this OsmDbContext db, long osmTrackId)
        {
            return await db.Tracks.Where(w => w.OsmTrackId == osmTrackId)
                .FirstOrDefaultAsync();
        }

        public static async Task<Track> GetOrCreateUnknownTrack(this OsmDbContext db, long osmTrackId)
        {
            var existing = await db.GetForOsmTrackId(osmTrackId);
            if (existing != null) return existing;

            existing = new Track()
            {
                OsmState = TrackOsmStateEnum.Unknown,
                OsmTrackId = osmTrackId
            };
            await db.Tracks.AddAsync(existing);
            await db.SaveChangesAsync();
            return existing;
        }

        public static async Task SetToPrivateIfUnknown(this OsmDbContext db, long osmTrackId)
        {
            var existing = await db.GetForOsmTrackId(osmTrackId);
            if (existing == null) return;
            if (existing.OsmState != TrackOsmStateEnum.Unknown) return;

            existing.OsmState = TrackOsmStateEnum.Private;
            
            db.Tracks.Update(existing);
            await db.SaveChangesAsync();
        }

        public static async Task<Track> GetOrCreatePublicTrack(this OsmDbContext db, User user, long osmTrackId,
            DateTime osmTimeStamp, string name, string[] tags)
        {
            var existing = await db.GetForOsmTrackId(osmTrackId) ?? new Track();

            existing.OsmTrackId = osmTrackId;
            existing.OsmState = TrackOsmStateEnum.Public;
            existing.GpxFileName = name;
            existing.User = user;
            existing.UserId = user.Id;
            existing.Tags = tags;
            existing.OsmTimeStamp = osmTimeStamp;
            
            db.Tracks.Update(existing);
            await db.SaveChangesAsync();
            return existing;
        }

        public static async Task<Track?> GetUnSyncedPublicTrack(this OsmDbContext db)
        {
            return await db.Tracks.Where(x => x.OsmState == TrackOsmStateEnum.Public &&
                                        x.SyncState == TrackSyncStateEnum.Unknown &&
                                        x.GpxFile == null)
                .FirstOrDefaultAsync();
        }
    }
}