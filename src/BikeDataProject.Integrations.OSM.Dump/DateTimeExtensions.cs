using System;

namespace BikeDataProject.Integrations.OSM.Dump
{
    internal static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a standard DateTime into the number of seconds since 1/1/1970.
        /// </summary>
        public static long ToUnixTime(this DateTime date)
        {
            return (long)(date - DateTime.UnixEpoch).TotalSeconds;
        }
    }
}