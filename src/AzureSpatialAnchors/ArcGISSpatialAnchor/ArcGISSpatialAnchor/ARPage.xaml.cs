using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Mapping;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace ArcGISSpatialAnchor
{
    [DesignTimeVisible(false)]
    public partial class ARPage : ContentPage
    {
        private SpatialAnchorsManager manager;
        private bool _scenePlaced = false;

        public ARPage()
        {
            InitializeComponent();
            //We'll set the origin of the scene centered on Mnt Everest so we can use that as the tie-point when we tap to place
            arSceneView.OriginCamera = new Camera(27.988056, 86.925278, 0, 0, 90, 0);
            arSceneView.TranslationFactor = 10000; //1m device movement == 10km

            // Initialize Spatial Anchors
            manager = new SpatialAnchorsManager(AccountDetails.SpatialAnchorsAccountId, AccountDetails.SpatialAnchorsAccountKey, arSceneView);
            manager.AnchorLocated += Manager_AnchorLocated;
        }

        private async void InitializeScene()
        {
            try
            {
                var scene = new Scene(Basemap.CreateImagery());
                scene.BaseSurface = new Surface();
                scene.BaseSurface.BackgroundGrid.IsVisible = false;
                scene.BaseSurface.ElevationSources.Add(new ArcGISTiledElevationSource(new Uri("https://elevation3d.arcgis.com/arcgis/rest/services/WorldElevation3D/Terrain3D/ImageServer")));
                scene.BaseSurface.NavigationConstraint = NavigationConstraint.None;
                await scene.LoadAsync();
                arSceneView.Scene = scene;
            }
            catch (System.Exception ex)
            {
                await DisplayAlert("Failed to load scene", ex.Message, "OK");
                await Navigation.PopAsync();
            }
        }
        Microsoft.Azure.SpatialAnchors.CloudSpatialAnchorWatcher currentWatcher;
        protected override void OnAppearing()
        {
            Status.Text = "Move your device in a circular motion to detect surfaces";
            arSceneView.StartTrackingAsync();
            manager.StartSession();
            currentWatcher = manager.StartLocating(new Microsoft.Azure.SpatialAnchors.AnchorLocateCriteria() { });

            base.OnAppearing();
        }

        private void Manager_AnchorLocated(object sender, Microsoft.Azure.SpatialAnchors.CloudSpatialAnchor e)
        {
        }

        protected override void OnDisappearing()
        {
            arSceneView.StopTrackingAsync();
            manager.StopSession();
            base.OnDisappearing();
        }

        private async void DoubleTap_ToPlace(object sender, Esri.ArcGISRuntime.Xamarin.Forms.GeoViewInputEventArgs e)
        {
            if (!_scenePlaced)
            {
                if (arSceneView.SetInitialTransformation(e.Position))
                {
                    if (arSceneView.Scene == null)
                    {
                        arSceneView.RenderPlanes = false;
                        Status.Text = string.Empty;
                        _scenePlaced = true;
                        InitializeScene();
                    }
                }
            }
            else
            {
                var mp = arSceneView.ARScreenToLocation(e.Position);
                if(mp == null)
                {
                    return;
                }
                var localAnchor = manager.CreateLocalAnchor(e.Position);
                if(localAnchor != null)
                {
                    localAnchor.AppProperties.Add("Latitude", mp.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    localAnchor.AppProperties.Add("Longitude", mp.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    try
                    {
                        await manager.CreateAnchorAsync(localAnchor);
                    }
                    catch(System.Exception ex)
                    {
                        Debug.WriteLine("Couldn't save anchor to cloud: " + ex.Message);
                    }
                }
            }
        }

        private void PlanesDetectedChanged(object sender, bool planesDetected)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                if (!planesDetected)
                    Status.Text = "Move your device in a circular motion to detect surfaces";
                else if (arSceneView.Scene == null)
                    Status.Text = "Double-tap a plane to place your scene";
                else
                    Status.Text = string.Empty;
            });
        }
    }
}
