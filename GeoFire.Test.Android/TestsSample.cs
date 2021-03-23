using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Plugin.CloudFirestore;
using Plugin.CloudFirestore.Attributes;
using Xunit;
using Xunit.Abstractions;

namespace GeoFire.Test.Android
{
    public class TestsSample
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public TestsSample(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        class Test
        {
            public string Name { get; set; }
        }
        
        [Fact]
        public async Task GetAndSetLocation()
        {
            var geoFire = new GeoFire("test");
            try
            {
                await CrossCloudFirestore.Current.Instance
                    .GetCollection("test")
                    .GetDocument("ququ2")
                    .SetDataAsync(new Dictionary<string, object>
                    {
                        {"test", "test"}
                    });
                await geoFire.SetLocationAsync("ququ2", new GeoPoint(5, 5));
                var geoPoint = await geoFire.GetLocationAsync("ququ2");
                Assert.Equal(new GeoPoint(5, 5), geoPoint);
            }
            catch (Exception e)
            {
                Assert.Null(e);
            }
            Assert.True(true);
        }
        
        [Fact]
        public async Task RemoveLocation()
        {
            var geoFire = new GeoFire("test");
            try
            {
                await CrossCloudFirestore.Current.Instance
                    .GetCollection("test")
                    .GetDocument("ququ2")
                    .SetDataAsync(new Dictionary<string, object>
                    {
                        {"test", "test"}
                    });
                await geoFire.SetLocationAsync("ququ2", new GeoPoint(5, 5));
                await geoFire.RemoveLocationAsync("ququ2");
                var doc = await CrossCloudFirestore.Current.Instance.GetDocument("/test/ququ2").GetDocumentAsync();
                Assert.True(doc.Exists);
                Assert.True(!doc.Data.ContainsKey("q"));
                Assert.True(!doc.Data.ContainsKey("l"));
                Assert.True(doc.Data.ContainsKey("test"));
            }
            catch (Exception e)
            {
                Assert.Null(e);
            }
            Assert.True(true);
        }

        [Fact]
        public async Task TestQuery()
        {
            var geoFire = new GeoFire("test");
            try
            {
                var doc = CrossCloudFirestore.Current.Instance
                    .GetCollection("test")
                    .CreateDocument();
                await doc.SetDataAsync(new Test { Name = "Sofia" });
                await geoFire.SetLocationAsync(doc.Id, new GeoPoint(5, 5));
                
                var query = geoFire.QueryAtLocation<Test>(new GeoPoint(5, 5), 10);
                query.OnDocumentEntered += (sender, args) =>
                {
                    Assert.IsType<Test>(args.Document);
                };
            }
            catch (Exception e)
            {
                Assert.Null(e);
            }
            Assert.True(true);
        }

        [Fact]
        public async Task UploadData()
        {
            var geoFire = new GeoFire("test");
            using (var stream = Helper.Assets.Open("petmap-export.json"))
            {
                using (var reader = new StreamReader(stream))
                {
                    var text = reader.ReadToEnd();
                    var pointsContainer = JsonConvert.DeserializeObject<PointsContainer>(text);
                    var collection = CrossCloudFirestore.Current.Instance.GetCollection("points");
                    foreach (var point in pointsContainer.Points)
                    {
                        var doc = collection.CreateDocument();
                        doc.SetData(point, e =>
                        {
                            if (e != null)
                            {
                                throw e;
                            }
                        });
                        geoFire.SetLocation(doc.Id, new GeoPoint(point.lt, point.ln), e1 =>
                        {
                            if (e1 != null)
                            {
                                _testOutputHelper.WriteLine(e1.ToString());   
                            }
                        });
                        if (point.org == null)
                        {
                            doc.SetData(new Dictionary<string, object> {{"org", FieldValue.Delete}}, true,
                                e =>
                                {
                                    if (e != null)
                                    {
                                        _testOutputHelper.WriteLine(e.ToString());   
                                    }
                                });
                        }
                    }
                }
            }
        }
    }

    public class Point
    {
        [Ignored] public double lt { get; set; }
        [Ignored] public double ln { get; set; }

        public string title { get; set; }
        public string org { get; set; }
        public decimal prPr { get; set; }
        public decimal regPr { get; set; }
        
    }

    public class PointsContainer
    {
        [MapTo("points")] public Point[] Points { get; set; }
    }
}
