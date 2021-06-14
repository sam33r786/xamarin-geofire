# GeoFire for Xamarin — Realtime location queries with Firebase

GeoFire is an open-source library for Xamarin that allows you to store and query a set of keys based on their geographic location.

At its heart, GeoFire simply stores locations with string keys. Its main benefit however, is the possibility of querying keys within a given geographic area - all in realtime.

GeoFire uses the Firebase Firestore Database for data storage, allowing query results to be updated in realtime as they change. GeoFire selectively loads only the data near certain locations, keeping your applications light and responsive, even with extremely large datasets.

This is a fork of the original work done by @exxbrain
