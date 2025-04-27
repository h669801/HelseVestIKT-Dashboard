using Microsoft.Web.WebView2.Core;
using System;
using System.Windows;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class ApiKeyWindow : Window
    {
        private const string ApiKeyUrl = "https://steamcommunity.com/dev/apikey";

        public ApiKeyWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            // Sørg for WebView2 er klar
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Navigate(ApiKeyUrl);
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Om ønskelig kan du her intercept’e visse URL’er,
            // men vi lar brukeren selv kopiere nøkkelen fra Steam-siden.
        }
    }
}
