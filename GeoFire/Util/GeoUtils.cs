/*
 * Copyright 2019 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * https://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Diagnostics;
using Plugin.CloudFirestore;

namespace GeoFire.Util
{
    public static class GeoUtils 
    {
        private const double MaxSupportedRadius = 8587;

        public static double Distance(GeoPoint location1, GeoPoint location2)
        {
            return Distance(location1.Latitude, location1.Longitude, location2.Latitude, location2.Longitude);
        }

        public static double Distance(double lat1, double long1, double lat2, double long2) 
        {
            // Earth's mean radius in meters
            var radius = (Constants.EarthEqRadius + Constants.EarthPolarRadius)/2;
            double latDelta = ConvertToRadians(lat1 - lat2);
            double lonDelta = ConvertToRadians(long1 - long2);

            double a = Math.Sin(latDelta/2)*Math.Sin(latDelta/2) +
                       Math.Cos(ConvertToRadians(lat1))*Math.Cos(ConvertToRadians(lat2)) *
                       Math.Sin(lonDelta/2) * Math.Sin(lonDelta/2);
            return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        public static double DistanceToLatitudeDegrees(double distance) 
        {
            return distance/Constants.MetersPerDegreeLatitude;
        }

        public static double DistanceToLongitudeDegrees(double distance, double latitude) 
        {
            var radians = ConvertToRadians(latitude);
            var numerator = Math.Cos(radians) * Constants.EarthEqRadius * Math.PI / 180;
            var denoMinator = 1/Math.Sqrt(1 - Constants.EarthE2*Math.Sin(radians)*Math.Sin(radians));
            var deltaDegrees = numerator*denoMinator;
            if (deltaDegrees < Constants.Epsilon) 
            {
                return distance > 0 ? 360 : distance;
            }

            return Math.Min(360, distance/deltaDegrees);
        }

        private static double ConvertToRadians(double angle)
        {
            return Math.PI / 180 * angle;
        }
        
        public static double WrapLongitude(double longitude) 
        {
            if (longitude >= -180 && longitude <= 180) 
            {
                return longitude;
            }
            var adjusted = longitude + 180;
            if (adjusted > 0) 
            {
                return adjusted % 360.0 - 180;
            }

            return 180 - -adjusted % 360;
        }

        public static double CapRadius(double radius) 
        {
            if (!(radius > MaxSupportedRadius)) return radius;
            
            Debug.WriteLine("The radius is bigger than " + MaxSupportedRadius + " and hence we'll use that value");
            
            return MaxSupportedRadius;
        }

        public static bool CoordinatesValid(double latitude, double longitude) {
            return latitude >= -90 && latitude <= 90 && longitude >= -180 && longitude <= 180;
        }
    }
}
