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

/**
 * A GeoFire instance is used to store geo location data in Firebase.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GeoFire.Core;
using GeoFire.Util;
using Plugin.CloudFirestore;

namespace GeoFire
{

    public class GeoFire
    {

        public static GeoPoint GetLocationValue(IDocumentSnapshot documentSnapshot)
        {
            var data = documentSnapshot.Data;
            var geoPoint = (GeoPoint) data["l"];
            if (!GeoUtils.CoordinatesValid(geoPoint.Latitude, geoPoint.Longitude))
            {
                throw new ArgumentException($"Check {documentSnapshot.Id}. " +
                                            $"GeoPoint [lat: {geoPoint.Latitude}, long: {geoPoint.Longitude}] is invalid");
            }
            return geoPoint;
        }

        private readonly ICollectionReference _collectionRef;

        /**
         * Creates a new GeoFire instance at the given Firebase reference.
         *
         * @param databaseReference The Firebase reference this GeoFire instance uses
         */
        public GeoFire(string collectionPath)
        {
            var firestore = CrossCloudFirestore.Current.Instance;
            _collectionRef = firestore.GetCollection(collectionPath);
        }

        /**
         * @return The Firebase reference this GeoFire instance uses
         */
        public ICollectionReference GetCollectionRef()
        {
            return _collectionRef;
        }

        public IDocumentReference GetDocumentRef(string key)
        {
            return _collectionRef.GetDocument(key);
        }

        /**
         * Sets the location for a given key.
         *
         * @param key                The key to save the location for
         * @param location           The location of this key
         * @param completionListener A listener that is called once the location was successfully saved on the server or an
         *                           error occurred
         */
        public Task SetLocationAsync(string path, GeoPoint point) {
            if (path == null)
            {
                throw new NullReferenceException();
            }

            var keyRef = GetDocumentRef(path);
            var geoHash = new GeoHash(point);
            var data = new Dictionary<string, object>
            {
                {"g", geoHash.GetGeoHashString()}, 
                {"l", point}
            };
            return keyRef.SetDataAsync(data, true);
        }
        
        /**
         * Sets the location for a given key.
         *
         * @param key                The key to save the location for
         * @param location           The location of this key
         * @param completionListener A listener that is called once the location was successfully saved on the server or an
         *                           error occurred
         */
        public void SetLocation(string path, GeoPoint point, CompletionHandler completionHandler) {
            if (path == null)
            {
                throw new NullReferenceException();
            }

            var keyRef = GetDocumentRef(path);
            var geoHash = new GeoHash(point);
            var data = new Dictionary<string, object>
            {
                {"g", geoHash.GetGeoHashString()}, 
                {"l", point}
            };
            keyRef.SetData(data, true, completionHandler);
        }

        /**
         * Removes the location for a key from this GeoFire.
         *
         * @param key                The key to remove from this GeoFire
         * @param completionListener A completion listener that is called once the location is successfully removed
         *                           from the server or an error occurred
         */
        public async Task RemoveLocationAsync(string key) {
            if (key == null)
            {
                throw new NullReferenceException();
            }
            var keyRef = GetDocumentRef(key);
            await keyRef.UpdateDataAsync(
                "g", FieldValue.Delete, 
                "l", FieldValue.Delete);
        }

        /**
         * Gets the current location for a key and calls the callback with the current value.
         *
         * @param key      The key whose location to get
         */
        public async Task<GeoPoint> GetLocationAsync(string key)
        {
            var keyRef = GetDocumentRef(key);
            return GetLocationValue(await keyRef.GetDocumentAsync());
        }

        /**
         * Returns a new Query object centered at the given location and with the given radius.
         *
         * @param center The center of the query
         * @param radius The radius of the query, in kilometers. The Maximum radius that is
         * supported is about 8587km. If a radius bigger than this is passed we'll cap it.
         * @return The new GeoQuery object
         */
        public GeoQuery<T> QueryAtLocation<T>(GeoPoint center, double radius)
        {
            return new GeoQuery<T>(this, center, GeoUtils.CapRadius(radius));
        }
    }
}
