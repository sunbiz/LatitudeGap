using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.IO.IsolatedStorage;
using Microsoft.Phone.Tasks;

namespace LatitudeGap
{
    public partial class SettingsPage : PhoneApplicationPage
    {
        IsolatedStorageSettings isoSettings = IsolatedStorageSettings.ApplicationSettings;
        public SettingsPage()
        {
            InitializeComponent();
            if (!isoSettings.Contains("Enablelocation")) 
                EnableLocation.IsChecked = true;
            else EnableLocation.IsChecked = (bool?)isoSettings["Enablelocation"] ?? true;
            isoSettings.Save();
        }

        private void EnableLocation_Checked(object sender, RoutedEventArgs e)
        {
            isoSettings["Enablelocation"] = true;
            isoSettings.Save();
        }

        private void EnableLocation_Unchecked(object sender, RoutedEventArgs e)
        {
            isoSettings["Enablelocation"] = false;
            isoSettings.Save();
        }

        private void TextBlock_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            new EmailComposeTask
            {
                Subject = "Privacy Question",
                Body = "Latitude Gap",
                To = "latitudegap@moksholutions.com",
            }.Show();
        }
    }
}