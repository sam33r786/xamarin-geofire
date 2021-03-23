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
using System.Collections.Generic;
using System.Linq;
using GeoFire.Util;
using Plugin.CloudFirestore;
using static System.String;

namespace GeoFire.Core
{
    public class GeoHashQuery 
    {

        public static class Utils 
        {

            public static double BitsLatitude(double resolution) 
            {
                return Math.Min(Math.Log(Constants.EarthMeridionalCircumference/2/resolution)/Math.Log(2),
                    GeoHash.MaxPrecisionBits);
            }

            public static double BitsLongitude(double resolution, double latitude) 
            {
                var degrees = GeoUtils.DistanceToLongitudeDegrees(resolution, latitude);
                return Math.Abs(degrees) > 0 ? Math.Max(1, Math.Log(360/degrees)/Math.Log(2)) : 1;
            }

            public static int BitsForBoundingBox(GeoPoint location, double size) 
            {
                var latitudeDegreesDelta = GeoUtils.DistanceToLatitudeDegrees(size);
                var latitudeNorth = Math.Min(90, location.Latitude + latitudeDegreesDelta);
                var latitudeSouth = Math.Max(-90, location.Latitude - latitudeDegreesDelta);
                var bitsLatitude = (int)Math.Floor(BitsLatitude(size)) *2;
                var bitsLongitudeNorth = (int)Math.Floor(BitsLongitude(size, latitudeNorth)) *2 - 1;
                var bitsLongitudeSouth = (int)Math.Floor(BitsLongitude(size, latitudeSouth)) *2 - 1;
                return Math.Min(bitsLatitude, Math.Min(bitsLongitudeNorth, bitsLongitudeSouth));
            }
        }

        private readonly string _startValue;
        private readonly string _endValue;

        public GeoHashQuery(string startValue, string endValue) {
            _startValue = startValue;
            _endValue = endValue;
        }

        public static GeoHashQuery QueryForGeoHash(GeoHash geohash, int bits) 
        {
            var hash = geohash.GetGeoHashString();
            var precision = (int)Math.Ceiling((double)bits/Base32Utils.BitsPerBase32Char);
            if (hash.Length < precision) 
            {
                return new GeoHashQuery(hash, hash+"~");
            }
            hash = hash.Substring(0, precision);
            var hashBase = hash.Substring(0, hash.Length - 1);
            var lastValue = Base32Utils.Base32CharToValue(hash[hash.Length - 1]);
            var significantBits = bits - hashBase.Length * Base32Utils.BitsPerBase32Char;
            var unusedBits = Base32Utils.BitsPerBase32Char - significantBits;
            // delete unused bits
            var startValue = (lastValue >> unusedBits) << unusedBits;
            var endValue = startValue + (1 << unusedBits);
            var startHash = hashBase + Base32Utils.ValueToBase32Char(startValue);
            var endHash = endValue > 31 ? hashBase + "~" : hashBase + Base32Utils.ValueToBase32Char(endValue);
            return new GeoHashQuery(startHash, endHash);
        }

        public static HashSet<GeoHashQuery> QueriesAtLocation(GeoPoint location, double radius) 
        {
            var queryBits = Math.Max(1, Utils.BitsForBoundingBox(location, radius));
            var geoHashPrecision = (int) Math.Ceiling((float)queryBits /Base32Utils.BitsPerBase32Char);

            var latitude = location.Latitude;
            var longitude = location.Longitude;
            var latitudeDegrees = radius/Constants.MetersPerDegreeLatitude;
            var latitudeNorth = Math.Min(90, latitude + latitudeDegrees);
            var latitudeSouth = Math.Max(-90, latitude - latitudeDegrees);
            var longitudeDeltaNorth = GeoUtils.DistanceToLongitudeDegrees(radius, latitudeNorth);
            var longitudeDeltaSouth = GeoUtils.DistanceToLongitudeDegrees(radius, latitudeSouth);
            var longitudeDelta = Math.Max(longitudeDeltaNorth, longitudeDeltaSouth);

            var queries = new HashSet<GeoHashQuery>();

            var geoHash = new GeoHash(latitude, longitude, geoHashPrecision);
            var geoHashW = new GeoHash(latitude, GeoUtils.WrapLongitude(longitude - longitudeDelta), geoHashPrecision);
            var geoHashE = new GeoHash(latitude, GeoUtils.WrapLongitude(longitude + longitudeDelta), geoHashPrecision);

            var geoHashN = new GeoHash(latitudeNorth, longitude, geoHashPrecision);
            var geoHashNw = new GeoHash(latitudeNorth, GeoUtils.WrapLongitude(longitude - longitudeDelta), geoHashPrecision);
            var geoHashNE = new GeoHash(latitudeNorth, GeoUtils.WrapLongitude(longitude + longitudeDelta), geoHashPrecision);

            var geoHashS = new GeoHash(latitudeSouth, longitude, geoHashPrecision);
            var geoHashSW = new GeoHash(latitudeSouth, GeoUtils.WrapLongitude(longitude - longitudeDelta), geoHashPrecision);
            var geoHashSE = new GeoHash(latitudeSouth, GeoUtils.WrapLongitude(longitude + longitudeDelta), geoHashPrecision);

            queries.Add(QueryForGeoHash(geoHash, queryBits));
            queries.Add(QueryForGeoHash(geoHashE, queryBits));
            queries.Add(QueryForGeoHash(geoHashW, queryBits));
            queries.Add(QueryForGeoHash(geoHashN, queryBits));
            queries.Add(QueryForGeoHash(geoHashNE, queryBits));
            queries.Add(QueryForGeoHash(geoHashNw, queryBits));
            queries.Add(QueryForGeoHash(geoHashS, queryBits));
            queries.Add(QueryForGeoHash(geoHashSE, queryBits));
            queries.Add(QueryForGeoHash(geoHashSW, queryBits));

            // Join queries
            bool didJoin;
            do {
                GeoHashQuery query1 = null;
                GeoHashQuery query2 = null;
                foreach (var query in queries)
                {
                    foreach (var other in queries.Where(other => query != other && query.CanJoinWith(other)))
                    {
                        query1 = query;
                        query2 = other;
                        break;
                    }
                }
                if (query1 != null && query2 != null) {
                    queries.Remove(query1);
                    queries.Remove(query2);
                    queries.Add(query1.JoinWith(query2));
                    didJoin = true;
                } else {
                    didJoin = false;
                }
            } while (didJoin);

            return queries;
        }

        private bool IsPrefix(GeoHashQuery other) 
        {
            return Compare(other._endValue, this._startValue, StringComparison.Ordinal) >= 0 &&
                   Compare(other._startValue, this._startValue, StringComparison.Ordinal) < 0 &&
                   Compare(other._endValue, this._endValue, StringComparison.Ordinal) < 0;
        }

        private bool IsSuperQuery(GeoHashQuery other) 
        {
            var startCompare = Compare(other._startValue, this._startValue, StringComparison.Ordinal);
            return startCompare <= 0 && Compare(other._endValue, this._endValue, StringComparison.Ordinal) >= 0;
        }

        public bool CanJoinWith(GeoHashQuery other) 
        {
            return IsPrefix(other) || other.IsPrefix(this) || IsSuperQuery(other) || other.IsSuperQuery(this);
        }

        public GeoHashQuery JoinWith(GeoHashQuery other)
        {
            if (other.IsPrefix(this)) 
            {
                return new GeoHashQuery(_startValue, other._endValue);
            }

            if (IsPrefix(other)) {
                return new GeoHashQuery(other._startValue, _endValue);
            }

            if (IsSuperQuery(other)) {
                return other;
            }

            if (other.IsSuperQuery(this)) {
                return this;
            }

            throw new ArgumentException("Can't join these 2 queries: " + this + ", " + other);
        }

        public bool ContainsGeoHash(GeoHash hash) {
            var hashStr = hash.GetGeoHashString();
            return Compare(_startValue, hashStr, StringComparison.Ordinal) <= 0 && 
                   Compare(_endValue, hashStr, StringComparison.Ordinal) > 0;
        }

        public string GetStartValue() {
            return _startValue;
        }

        public string GetEndValue() {
            return _endValue;
        }

        protected bool Equals(GeoHashQuery other)
        {
            return _startValue == other._startValue && _endValue == other._endValue;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((GeoHashQuery) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_startValue != null ? _startValue.GetHashCode() : 0) * 397) ^ (_endValue != null ? _endValue.GetHashCode() : 0);
            }
        }
        
        public override string ToString() {
            return "GeoHashQuery{" +
                   "startValue='" + _startValue + '\'' +
                   ", endValue='" + _endValue + '\'' +
                   '}';
        }

    }
}
