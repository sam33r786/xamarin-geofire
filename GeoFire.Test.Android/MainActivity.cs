using System.Reflection;

using Android.App;
using Android.Content.Res;
using Android.OS;
using Firebase;
using Plugin.CloudFirestore;
using Xunit.Runners.UI;
using Xunit.Sdk;

namespace GeoFire.Test.Android
{
    [Activity(Label = "GeoFire.Test.Android", MainLauncher = true)]
    public class MainActivity : RunnerActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            FirebaseApp.InitializeApp(this);
            CrossCloudFirestore.Current.Instance.FirestoreSettings = new FirestoreSettings
            {
                AreTimestampsInSnapshotsEnabled = true,
            };

            Helper.Assets = Assets;
            
            // tests can be inside the main assembly
            AddTestAssembly(Assembly.GetExecutingAssembly());
            AddExecutionAssembly(typeof(ExtensibilityPointFactory).Assembly);
            // or in any reference assemblies
            // AddTest (typeof (Your.Library.TestClass).Assembly);

            // Once you called base.OnCreate(), you cannot add more assemblies.
            base.OnCreate(bundle);
        }
    }
}
