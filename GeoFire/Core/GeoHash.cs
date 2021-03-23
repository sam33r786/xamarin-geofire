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
using GeoFire.Util;
using Plugin.CloudFirestore;

namespace GeoFire.Core
{
    public class GeoHash {
        private readonly string _geoHash;
        private readonly GeoPoint geoPoint;

        // The default precision of a geohash
        private const int DefaultPrecision = 10;

        // The Maximal precision of a geohash
        public const int MaxPrecision = 22;

        // The Maximal number of bits precision for a geohash
        public static readonly int MaxPrecisionBits = MaxPrecision * Base32Utils.BitsPerBase32Char;

        public GeoHash(GeoPoint location) : this(location.Latitude, location.Longitude) {
        }

        public GeoHash(double latitude, double longitude, int precision = DefaultPrecision) {
            if (precision < 1) {
                throw new ArgumentException("Precision of GeoHash must be larger than zero!");
            }
            if (precision > MaxPrecision) {
                throw new ArgumentException("Precision of a GeoHash must be less than " + (MaxPrecision + 1) + "!");
            }
            if (!GeoUtils.CoordinatesValid(latitude, longitude)) {
                throw new ArgumentException(string.Format("Not valid location coordinates: [%f, %f]", latitude, longitude));
            }
            double[] longitudeRange = { -180, 180 };
            double[] latitudeRange = { -90, 90 };

            this.geoPoint = new GeoPoint(latitude, longitude);

            var buffer = new char[precision];

            for (var i = 0; i < precision; i++) {
                var hashValue = 0;
                for (var j = 0; j < Base32Utils.BitsPerBase32Char; j++) {
                    var even = (i * Base32Utils.BitsPerBase32Char + j) % 2 == 0;
                    var val = even ? longitude : latitude;
                    var range = even ? longitudeRange : latitudeRange;
                    var mid = (range[0] + range[1])/2;
                    if (val > mid) {
                        hashValue = (hashValue << 1) + 1;
                        range[0] = mid;
                    } else {
                        hashValue <<= 1;
                        range[1] = mid;
                    }
                }
                buffer[i] = Base32Utils.ValueToBase32Char(hashValue);
            }
            _geoHash = new string(buffer);
        }

        public GeoHash(string hash) {
            if (hash.Length == 0 || !Base32Utils.IsValidBase32String(hash)) {
                throw new ArgumentException("Not a valid geoHash: " + hash);
            }
            _geoHash = hash;
        }

        public string GetGeoHashString() {
            return _geoHash;
        }

        public GeoPoint GetGeoPoint()
        {
            return geoPoint;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((GeoHash) obj);
        }

        public override int GetHashCode()
        {
            return _geoHash != null ? _geoHash.GetHashCode() : 0;
        }

        public override string ToString() {
            return "GeoHash{" +
                   "geoHash='" + _geoHash + '\'' +
                   '}';
        }
    }
}
