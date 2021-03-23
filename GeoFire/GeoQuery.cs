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
 * A GeoQuery object can be used for geo queries in a given circle. The GeoQuery class is thread safe.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GeoFire.Core;
using GeoFire.Util;
using Plugin.CloudFirestore;

namespace GeoFire
{
    public class GeoQuery<T> : IDisposable
    {
        private readonly object _lock = new object();

        private const int KilometerToMeter = 1000;

        public event EventHandler<DocumentEventArgs<T>> OnDocumentEntered;
        public event EventHandler<DocumentEventArgs<T>> OnDocumentExited;
        public event EventHandler<DocumentEventArgs<T>> OnDocumentChanged;
        public event EventHandler<DocumentEventArgs<T>> OnDocumentMoved;
        public event EventHandler<ErrorEventArgs> OnError;
        public event EventHandler<EventArgs> OnQueryReady;
        
        private class LocationInfo
        {
            public GeoPoint Location { get; }
            public bool InGeoQuery { get; }
            public GeoHash GeoHash { get; }
            public IDocumentSnapshot Snapshot { get; }

            public LocationInfo(GeoPoint location, bool inGeoQuery, IDocumentSnapshot snapshot)
            {
                Location = location;
                InGeoQuery = inGeoQuery;
                GeoHash = new GeoHash(location);
                Snapshot = snapshot;
            }
        }

        private readonly GeoFire _geoFire;
        private readonly Dictionary<GeoHashQuery, IQuery> _firebaseQueries = new Dictionary<GeoHashQuery, IQuery>();
        private readonly Dictionary<GeoHashQuery, IListenerRegistration> _queryListeners = new Dictionary<GeoHashQuery, IListenerRegistration>();
        private readonly HashSet<GeoHashQuery> _outstandingQueries = new HashSet<GeoHashQuery>();
        private readonly Dictionary<string, LocationInfo> _locationInfos = new Dictionary<string, LocationInfo>();
        private GeoPoint _center;
        private double _radius;
        private HashSet<GeoHashQuery> _queries;

        public GeoQuery(GeoFire geoFire, GeoPoint center, double radius)
        {
            _geoFire = geoFire;
            _center = center;
            _radius = radius * KilometerToMeter; // Convert from kilometers to meters.
            SetupQueries();
        }

        private bool LocationIsInQuery(GeoPoint location)
        {
            return GeoUtils.Distance(location, _center) <= _radius;
        }

        private void UpdateLocationInfo(IDocumentSnapshot document, GeoPoint location)
        {
            var key = document.Id;
            _locationInfos.TryGetValue(key, out var oldInfo);
            var isNew = oldInfo == null;
            var changedLocation = oldInfo != null && !Equals(oldInfo.Location, location);
            var wasInQuery = oldInfo != null && oldInfo.InGeoQuery;

            var isInQuery = LocationIsInQuery(location);
            if ((isNew || !wasInQuery) && isInQuery)
            {
                OnDocumentEntered?.Invoke(this, new DocumentEventArgs<T>(document.ToObject<T>(), location));
            }
            else if (!isNew && isInQuery)
            {
                if (changedLocation)
                {
                    OnDocumentMoved?.Invoke(this, new DocumentEventArgs<T>(document.ToObject<T>(), location));
                }
                OnDocumentChanged?.Invoke(this, new DocumentEventArgs<T>(document.ToObject<T>(), location));
            }
            else if (wasInQuery && !isInQuery)
            {
                OnDocumentExited?.Invoke(this, new DocumentEventArgs<T>(document.ToObject<T>()));
            }

            var newInfo = new LocationInfo(location, LocationIsInQuery(location), document);
            _locationInfos.Add(key, newInfo);
        }

        private bool GeoHashQueriesContainGeoHash(GeoHash geoHash)
        {
            return _queries != null && _queries.Any(query => query.ContainsGeoHash(geoHash));
        }

        private void Reset()
        {
            foreach (var entry in _firebaseQueries)
            {
                _queryListeners[entry.Key].Remove();
            }
            _outstandingQueries.Clear();
            _firebaseQueries.Clear();
            _queryListeners.Clear();
            _queries = null;
            _locationInfos.Clear();
        }

        private bool HasEventHandlers()
        {
            return OnDocumentEntered != null
                   || OnDocumentExited != null
                   || OnDocumentChanged != null
                   || OnDocumentMoved != null
                   || OnQueryReady != null
                   || OnError != null;
        }

        private bool CanFireReady()
        {
            return !_outstandingQueries.Any();
        }

        private void CheckAndFireReady()
        {
            if (!CanFireReady()) return;
            OnQueryReady?.Invoke(this, new EventArgs());
        }
        
        private void SetupQueries()
        {
            var oldQueries = _queries ?? new HashSet<GeoHashQuery>();
            var newQueries = GeoHashQuery.QueriesAtLocation(_center, _radius);
            _queries = newQueries;
            foreach (var query in oldQueries.Where(query => !newQueries.Contains(query)))
            {
                _firebaseQueries.Remove(query);
                _outstandingQueries.Remove(query);
            }
            foreach (var query in newQueries.Where(query => !oldQueries.Contains(query)))
            {
                _outstandingQueries.Add(query);
                var collection = _geoFire.GetCollectionRef();
                var firebaseQuery = collection.OrderBy("g").StartAt(query.GetStartValue())
                    .EndAt(query.GetEndValue());
                _queryListeners.Add(query, firebaseQuery.AddSnapshotListener((snapshot, e) =>
                {
                    if (e != null)
                    {
                        OnError?.Invoke(this, new ErrorEventArgs(e));
                        return;
                    }
                    lock (_lock)
                    {
                        var firQuery = _firebaseQueries.First(x => x.Value.Equals(snapshot.Query)).Key;
                        _outstandingQueries.Remove(firQuery);
                        CheckAndFireReady();
                    }

                    foreach (var change in snapshot.DocumentChanges)
                    {
                        switch (change.Type)
                        {
                            case DocumentChangeType.Added:
                                ChildAdded(change.Document);
                                break;
                            case DocumentChangeType.Removed:
                                ChildRemoved(change.Document);
                                break;
                            case DocumentChangeType.Modified:
                                ChildChanged(change.Document);
                                break;
                        }
                    }
                }));
                _firebaseQueries.Add(query, firebaseQuery);
            }
            
            foreach (var oldLocationInfo in _locationInfos.Select(info => info.Value).Where(oldLocationInfo => oldLocationInfo != null))
            {
                UpdateLocationInfo(oldLocationInfo.Snapshot, oldLocationInfo.Location);
            }
            // remove locations that are not part of the geo query anymore
            foreach (var entry in _locationInfos.Where(x => !GeoHashQueriesContainGeoHash(x.Value.GeoHash)))
            {
                _locationInfos.Remove(entry.Key);
            }
            
            CheckAndFireReady();
        }

        private void ChildAdded(IDocumentSnapshot document)
        {
            var location = GeoFire.GetLocationValue(document);
            if (location != null)
            {
                UpdateLocationInfo(document, location);
            }
            else
            {
                Debug.Assert(false, "Got Datasnapshot without location with key " + document.Id);
            }
        }

        private void ChildChanged(IDocumentSnapshot document)
        {
            var location = GeoFire.GetLocationValue(document);
            if (location != null)
            {
                UpdateLocationInfo(document, location);
            }
            else
            {
                Debug.Assert(false, "Got Datasnapshot without location with key " + document.Id);
            }
        }

        private void ChildRemoved(IDocumentSnapshot document)
        {
            var key = document.Id;
            var info = _locationInfos[key];
            if (info == null) return;
            lock (_lock)
            {
                var location = GeoFire.GetLocationValue(document);
                var hash = (location != null) ? new GeoHash(location) : null;
                if (hash != null && GeoHashQueriesContainGeoHash(hash)) return;
                
                _locationInfos.Remove(key);
                
                if (!info.InGeoQuery) return;
                
                OnDocumentExited?.Invoke(this, new DocumentEventArgs<T>(info.Snapshot.ToObject<T>()));
            }

        }

        /**
         * Sets the center and radius (in kilometers) of this query, and triggers new events if necessary.
         * @param center The new center
         * @param radius The radius of the query, in kilometers. The Maximum radius that is
         * supported is about 8587km. If a radius bigger than this is passed we'll cap it.
         */
        public void SetLocation(GeoPoint center, double radius)
        {
            lock (_lock)
            {
                _center = center;
                // convert radius to meters
                _radius = GeoUtils.CapRadius(radius) * KilometerToMeter;
                if (HasEventHandlers())
                {
                    SetupQueries();
                }   
            }
        }

        public void Dispose()
        {
            Reset();
        }
    }

    public class DocumentEventArgs<T> : EventArgs
    {
        public GeoPoint Location { get; }
        public T Document { get; }

        public DocumentEventArgs(T document, GeoPoint location = default(GeoPoint))
        {
            Location = location;
            Document = document;
        }
    }
}
