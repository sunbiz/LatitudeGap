using System;
using System.Net;
using Microsoft.Phone.Controls;
using RestSharp;
using System.Runtime.Serialization.Json;
using System.Text;
using System.IO;
using System.IO.IsolatedStorage;
using System.Device.Location;
using System.Device;
using Microsoft.Phone.Controls.Maps;
using System.Windows;
using System.Linq;
using Microsoft.Phone.Shell;

namespace LatitudeGap
{
    public partial class MainPage : PhoneApplicationPage
    {

        GeoCoordinateWatcher watcher;

        private const String CURRENT_LOCATION = "https://www.googleapis.com/latitude/v1/currentLocation";

        private const String DEVICE_URL = "https://accounts.google.com/o/oauth2/device/code";
        private const String TOKEN_URL = "https://accounts.google.com/o/oauth2/token";
        private const String CLIENT_ID = "<add your own client_id>";
        private const String CLIENT_SECRET = "<add your own client_secret>";
        private const String SCOPE = "https://www.googleapis.com/auth/latitude.all.best";
        private const String APPROVAL_URL = "https://accounts.google.com/o/oauth2/device/approval";

        // Constructor
        public MainPage()
        {
            InitializeComponent();
            if (watcher == null)
            {
                watcher = new GeoCoordinateWatcher(GeoPositionAccuracy.Default);
                watcher.MovementThreshold = 10;
                watcher.StatusChanged += new EventHandler<GeoPositionStatusChangedEventArgs>(watcher_StatusChanged);
                watcher.PositionChanged += new EventHandler<GeoPositionChangedEventArgs<GeoCoordinate>>(watcher_PositionChanged);
            }
            watcher.Start();
        }

        void watcher_StatusChanged(object sender, GeoPositionStatusChangedEventArgs e)
        {
            switch (e.Status)
            {
                case GeoPositionStatus.Disabled:
                    MessageBox.Show("Location Service is not enabled on the device");
                    break;

                case GeoPositionStatus.NoData:
                    MessageBox.Show(" The Location Service is working, but it cannot get location data.");
                    break;
            }
        }

        void watcher_PositionChanged(object sender, GeoPositionChangedEventArgs<GeoCoordinate> e)
        {
            if (e.Position.Location.IsUnknown)
            {
                MessageBox.Show("Please wait while your position is determined....");
                return;
            }
            this.map.Center = new GeoCoordinate(e.Position.Location.Latitude, e.Position.Location.Longitude);

            if (this.map.Children.Count != 0)
            {
                var pushpin = map.Children.OfType<Pushpin>().FirstOrDefault(p => "locationPushpin".Equals(p.Tag));

                if (pushpin != null)
                {
                    this.map.Children.Remove(pushpin);
                }
            }

            Pushpin locationPushpin = new Pushpin();
            locationPushpin.Tag = "locationPushpin";
            locationPushpin.Content = "You are here!";
            locationPushpin.Location = watcher.Position.Location;
            this.map.Children.Add(locationPushpin);
            this.map.SetView(watcher.Position.Location, 15.0);

            object access_token;
            if (IsolatedStorageSettings.ApplicationSettings.TryGetValue("access_token", out access_token))
            {
                string latitude = e.Position.Location.Latitude.ToString("0.0000");
                string longitude = e.Position.Location.Longitude.ToString("0.0000");
                string altitude = e.Position.Location.Altitude.ToString("0.0000");
                submitToLatitude(latitude, longitude);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("NO ACCESS TOKEN TO START WITH");
            }
        }

        private Boolean submitToLatitude(string latitude, string longitude) 
        {
            string latitudeData = "{ \"data\": { \"kind\":\"latitude#location\", \"latitude\":" + latitude + ", \"longitude\":" + longitude + "}}";
            object access_token;
            if (IsolatedStorageSettings.ApplicationSettings.TryGetValue("access_token", out access_token))
            {
                RestClient client = new RestClient(CURRENT_LOCATION);
                RestRequest request = new RestRequest(Method.POST);
                request.Resource = "?access_token="+ access_token;
                request.AddHeader("Content-Type", "application/json");
                request.AddParameter("application/json", latitudeData, ParameterType.RequestBody);
                var asyncHandle = client.ExecuteAsync(request, response =>
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        System.Diagnostics.Debug.WriteLine("Submitted Fine... Latitude Response -->> " + response.Content);
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        object refresh_token;
                        if (IsolatedStorageSettings.ApplicationSettings.TryGetValue("refresh_token", out refresh_token))
                        {
                            RestClient tokenClient = new RestClient(TOKEN_URL);
                            RestRequest req = new RestRequest(Method.POST);
                            request.AddParameter("client_id", CLIENT_ID);
                            request.AddParameter("client_secret", CLIENT_SECRET);
                            request.AddParameter("refresh_token", refresh_token);
                            request.AddParameter("grant_type", "refresh_token");
                            var asyncHandler = client.ExecuteAsync(req, resp =>
                            {
                                if (resp.StatusCode == HttpStatusCode.OK)
                                {

                                    using (var ms2 = new MemoryStream(Encoding.Unicode.GetBytes(resp.Content)))
                                    {
                                        var ser = new DataContractJsonSerializer(typeof(TokenResponse));
                                        TokenResponse obj = (TokenResponse)ser.ReadObject(ms2);
                                        IsolatedStorageSettings.ApplicationSettings["access_token"] = obj.access_token;
                                        IsolatedStorageSettings.ApplicationSettings["refresh_token"] = obj.refresh_token;
                                    }
                                }
                            });
                        }
                    }
                });
            }
            else
            {
                MessageBox.Show("Please authorize application with Google");
            }
            return true;
        }

        private void loginButton_Click(object sender, EventArgs e)
        {
            RestClient client = new RestClient(DEVICE_URL);
            RestRequest request = new RestRequest(Method.POST);
            request.AddParameter("client_id", CLIENT_ID);
            request.AddParameter("scope", SCOPE);
            var asyncHandle = client.ExecuteAsync(request, response =>
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    map.Visibility = System.Windows.Visibility.Collapsed;
                    using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(response.Content)))
                    {
                        var ser = new DataContractJsonSerializer(typeof(DeviceResponse));
                        DeviceResponse obj = (DeviceResponse)ser.ReadObject(ms);
                        IsolatedStorageSettings.ApplicationSettings["device_code"] = obj.device_code;
                        userCodeField.Visibility = System.Windows.Visibility.Visible;
                        userCodeField.Text = obj.user_code;
                        userCodeLabel.Visibility = System.Windows.Visibility.Visible;
                        webBrowser.Visibility = System.Windows.Visibility.Visible;
                        webBrowser.Navigate(new Uri(obj.verification_url));
                    }
                }
            });
        }

        private void tokenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            object deviceCode;
            if (IsolatedStorageSettings.ApplicationSettings.TryGetValue("device_code", out deviceCode))
            {
                webBrowser.Visibility = System.Windows.Visibility.Collapsed;
                map.Visibility = System.Windows.Visibility.Visible;

                RestClient client = new RestClient(TOKEN_URL);
                RestRequest request = new RestRequest(Method.POST);
                request.AddParameter("client_id", CLIENT_ID);
                request.AddParameter("client_secret", CLIENT_SECRET);
                request.AddParameter("code", deviceCode);
                request.AddParameter("grant_type", "http://oauth.net/grant_type/device/1.0");
                var asyncHandle = client.ExecuteAsync(request, response =>
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        using (var ms = new MemoryStream(Encoding.Unicode.GetBytes(response.Content)))
                        {
                            var ser = new DataContractJsonSerializer(typeof(TokenResponse));
                            TokenResponse obj = (TokenResponse)ser.ReadObject(ms);
                            IsolatedStorageSettings.ApplicationSettings["access_token"] = obj.access_token;
                            IsolatedStorageSettings.ApplicationSettings["refresh_token"] = obj.refresh_token;
                        }
                    }
                });
            }
        }

        private void webBrowser_Navigating(object sender, NavigatingEventArgs e)
        {
            String url = e.Uri.AbsoluteUri;
            if (url.Contains("?"))
            {
                if (url.Substring(0, url.IndexOf("?")).Equals(APPROVAL_URL))
                {
                    TokenButton.Visibility = System.Windows.Visibility.Visible;
                    userCodeField.Visibility = System.Windows.Visibility.Collapsed;
                    userCodeLabel.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IsEnabled = false;
            watcher.Stop();
            watcher.Start();
            ((ApplicationBarIconButton)ApplicationBar.Buttons[0]).IsEnabled = true;
        }
    }

    public class DeviceResponse
    {
        public string device_code { get; set; }
        public string user_code { get; set; }
        public string verification_url { get; set; }
        public int expires_in { get; set; }
        public int interval { get; set; }
    }

    public class TokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
    }
}