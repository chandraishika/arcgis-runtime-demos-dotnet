using System;
using Microsoft.Azure.SpatialAnchors;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
#if __IOS__
using ARKit;
#elif __ANDROID__
using Google.AR.Sceneform;
using Java.Util.Concurrent;
#endif

namespace ArcGISSpatialAnchor
{
    public partial class SpatialAnchorsManager
    {
        private Esri.ArcGISRuntime.ARToolkit.Forms.ARSceneView _view;
        private Esri.ArcGISRuntime.ARToolkit.ARSceneView _nativeView;
        private CloudSpatialAnchorSession cloudSession;
        private string _accountId;
        private string _accountKey;

        public SpatialAnchorsManager(string accountId, string accountKey, Esri.ArcGISRuntime.ARToolkit.Forms.ARSceneView view)
        {
            if(string.IsNullOrEmpty(accountId) || string.IsNullOrEmpty(accountKey))
            {
                throw new ArgumentException("Please set the Azure Spatial Anchors Account ID and Keys");
            }
            _accountId = accountId;
            _accountKey = accountKey;
            _view = view;
            view.PropertyChanged += View_PropertyChanged;
#if __ANDROID__
            _updateListener = new UpdateListener(OnUpdate);
#endif
        }

        public void StartSession()
        {
            cloudSession = new CloudSpatialAnchorSession()
            {
#if __IOS__
                Session = _nativeView.ARSCNView.Session,
#elif __ANDROID__
                Session = _nativeView.ArSceneView?.Session,
#endif
                LogLevel = SessionLogLevel.Information
            };
            this.cloudSession.Configuration.AccountId = _accountId;
            this.cloudSession.Configuration.AccountKey = _accountKey;

            //Delegate events hook here
#if __IOS__
            this.cloudSession.OnLogDebug += this.SpatialCloudSession_LogDebug;
#elif __ANDROID__
            this.cloudSession.LogDebug += this.SpatialCloudSession_LogDebug;
#endif
            this.cloudSession.Error += this.SpatialAnchorsSession_Error;
            this.cloudSession.AnchorLocated += this.SpatialAnchorsSession_AnchorLocated;
            //this.cloudSession.LocateAnchorsCompleted += this.SpatialAnchorsSession_LocateAnchorsCompleted;
            //this.cloudSession.SessionUpdated += this.SpatialAnchorsSession_SessionUpdated;

            this.cloudSession.Start();
            //this.statusLabelIsHidden = false;
            //this.errorLabelIsHidden = true;
            //this.enoughDataForSaving = false;
        }
        public void StopSession()
        {
            this.cloudSession.Stop();
            //this.cloudAnchor = null;
            //this.localAnchor = null;
            this.cloudSession = null;

            //foreach (AnchorVisual visual in this.anchorVisuals.Values)
            //{
            //    if (visual.node != null)
            //    {
            //        visual.node.RemoveFromParentNode();
            //    }
            //}
            //
            //this.anchorVisuals.Clear();
        }

#if __IOS__
        private void SpatialCloudSession_LogDebug(object sender, OnLogDebugEventArgs e)
        {
            string message = e.Message;
#elif __ANDROID__
        private void SpatialCloudSession_LogDebug(object sender, LogDebugEventArgs e)
        {
            string message = e.Args.Message;
#endif
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Debug.WriteLine(message);
        }

        public CloudSpatialAnchorWatcher StartLocating(AnchorLocateCriteria locateCriteria)
        {
            // Only 1 active watcher at a time is permitted.
            this.StopLocating();

            return this.cloudSession.CreateWatcher(locateCriteria);
        }

        public void StopLocating()
        {
            // Only 1 active watcher at a time is permitted.
#if __ANDROID__
            CloudSpatialAnchorWatcher watcher = this.cloudSession.ActiveWatchers.FirstOrDefault();
            watcher?.Stop();
            watcher?.Dispose();
#elif __IOS__
            CloudSpatialAnchorWatcher watcher = this.cloudSession.GetActiveWatchers().FirstOrDefault();
            watcher?.Stop();
#endif
        }

        private void SpatialAnchorsSession_AnchorLocated(object sender, AnchorLocatedEventArgs e)
        {
#if __IOS__
            LocateAnchorStatus status = e.Status;
            CloudSpatialAnchor anchor = e.Anchor;
#elif __ANDROID__
            LocateAnchorStatus status = e.Args.Status;
            CloudSpatialAnchor anchor = e.Args.Anchor;
#endif

            if (status == LocateAnchorStatus.Located)
            {
                Debug.WriteLine("Cloud Anchor found! Identifier : " + anchor.Identifier);
                //AnchorVisual visual = new AnchorVisual
                //{
                //    cloudAnchor = anchor,
                //    identifier = anchor.Identifier,
                //    localAnchor = anchor.LocalAnchor
                //};
                //this.anchorVisuals[visual.identifier] = visual;
#if __IOS__
                _nativeView.ARSCNView.Session.AddAnchor(anchor.LocalAnchor);
#elif __ANDROID__
                //_nativeView.ArSceneView.Session.AllAnchors.Add(
#endif
                //this.PlaceCube(visual.localAnchor);
                AnchorLocated?.Invoke(sender, anchor);
            }

        }

        public event EventHandler<CloudSpatialAnchor> AnchorLocated;

        public async Task<CloudSpatialAnchor> CreateAnchorAsync(CloudSpatialAnchor newCloudAnchor)
        {
            if (newCloudAnchor == null)
            {
                throw new ArgumentNullException(nameof(newCloudAnchor));
            }

            if (newCloudAnchor.LocalAnchor == null || !string.IsNullOrEmpty(newCloudAnchor.Identifier))
            {
                throw new ArgumentException("The specified cloud anchor cannot be saved.", nameof(newCloudAnchor));
            }

            //if (!this.CanCreateAnchor)
            //{
            //    throw new ArgumentException("Not ready to create. Need more data.");
            //}

            try
            {
#if __IOS__
                await this.cloudSession.CreateAnchorAsync(newCloudAnchor).ConfigureAwait(false);
#elif __ANDROID__
                await this.cloudSession.CreateAnchorAsync(newCloudAnchor).GetAsync().ConfigureAwait(false);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            return newCloudAnchor;
        }


        private void SpatialAnchorsSession_Error(object sender, SessionErrorEventArgs e)
        {
#if __ANDROID__
            var sessionErrorEvent = e.Args;
#elif __IOS__
            var sessionErrorEvent = e;
#endif
            string errorMessage = sessionErrorEvent.ErrorMessage;
            Console.WriteLine("Error Code : " + sessionErrorEvent.ErrorCode + ", Message : " + sessionErrorEvent.ErrorMessage);
        }

        private void View_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Renderer")
            {
                if (_nativeView != null)
                {
#if __IOS__
                    _nativeView.ARSCNViewInterruptionEnded -= NativeView_ARSCNViewInterruptionEnded;
                    _nativeView.ARSCNViewWillRenderScene -= NativeView_ARSCNViewWillRenderScene;
#elif __ANDROID__
                    _nativeView.ArSceneView.Scene.RemoveOnUpdateListener(_updateListener);
#endif
                    _nativeView = null;
                }
                //Get renderer
                _nativeView = NativeARSceneView();
#if __IOS__
                _nativeView.ARSCNViewInterruptionEnded += NativeView_ARSCNViewInterruptionEnded;
                _nativeView.ARSCNViewWillRenderScene += NativeView_ARSCNViewWillRenderScene;
#elif __ANDROID__
                _nativeView.ArSceneView.Scene.AddOnUpdateListener(_updateListener);
#endif
            }
        }

        private Esri.ArcGISRuntime.ARToolkit.ARSceneView NativeARSceneView()
        {
#if __ANDROID__
            return (global::Xamarin.Forms.Platform.Android.Platform.GetRenderer(_view) as Esri.ArcGISRuntime.ARToolkit.Forms.Platform.Android.ARSceneViewRenderer)?.Control as Esri.ArcGISRuntime.ARToolkit.ARSceneView;
#elif __IOS__
            return (global::Xamarin.Forms.Platform.iOS.Platform.GetRenderer(_view) as Esri.ArcGISRuntime.ARToolkit.Forms.Platform.iOS.ARSceneViewRenderer)?.Control as Esri.ArcGISRuntime.ARToolkit.ARSceneView;
#elif NETFX_CORE
            var r = global::Xamarin.Forms.Platform.UWP.VisualElementExtensions.GetOrCreateRenderer(_view);
            return r?.GetNativeElement() as Esri.ArcGISRuntime.ARToolkit.ARSceneView;
#endif
        }
    }

}

