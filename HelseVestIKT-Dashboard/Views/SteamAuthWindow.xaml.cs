using Microsoft.Web.WebView2.Core;
using System;
using System.Linq;
using System.Text;
using System.Windows;

namespace HelseVestIKT_Dashboard.Views
{
    public partial class SteamAuthWindow : Window
    {
        public ulong? SteamId { get; private set; }
        const string ReturnUrl = "http://localhost:5000/";

        public SteamAuthWindow()
        {
            InitializeComponent();
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            // Sørg for at WebView2 er initialisert
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.Navigate(BuildOpenIdUrl());
        }

        private string BuildOpenIdUrl()
        {
            var query = new[]
            {
                "openid.ns=http://specs.openid.net/auth/2.0",
                "openid.mode=checkid_setup",
                "openid.return_to=" + Uri.EscapeDataString(ReturnUrl),
                "openid.realm="     + Uri.EscapeDataString(ReturnUrl),
                "openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select",
                "openid.identity=http://specs.openid.net/auth/2.0/identifier_select"
            };
            return "https://steamcommunity.com/openid/login?" + string.Join("&", query);
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri.StartsWith(ReturnUrl, StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                var uri = new Uri(e.Uri);
                var qs = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var idUrl = qs["openid.identity"];
                if (Uri.TryCreate(idUrl, UriKind.Absolute, out var u))
                {
                    var segs = u.Segments;
                    if (ulong.TryParse(segs.Last().TrimEnd('/'), out var sid))
                        SteamId = sid;
                }
                DialogResult = SteamId.HasValue;
            }
        }
    }
}
