using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Streams;

// The Basic Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234237

namespace MetroUsageMeter
{
    /// <summary>
    /// A basic page that provides characteristics common to most applications.
    /// </summary>
    public sealed partial class MainPage : MetroUsageMeter.Common.LayoutAwarePage
    {
        private NodeConnector nodeConnector;
        private Uri apiUri;
        private String encryptedPassword;
        private bool credsHaveChanged;

        public MainPage()
        {
            this.InitializeComponent();
            apiUri = new Uri("https://customer-webtools-api.internode.on.net/api/v1.5/");
            nodeConnector = new NodeConnector(apiUri);
            encryptedPassword = null;
            credsHaveChanged = false;
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            // Restore values stored in app data.
            Windows.Storage.ApplicationDataContainer roamingSettings =
                Windows.Storage.ApplicationData.Current.RoamingSettings;

            CredentialsEncrypter decrypter = new CredentialsEncrypter();

            if (roamingSettings.Values.ContainsKey("username"))
            {
                try
                {
                    usernameInput.Text = decrypter.Decrypt(roamingSettings.Values["username"].ToString());
                }
                catch (Exception e)
                {
                    // ignore errors...
                }
            }
            if (roamingSettings.Values.ContainsKey("password"))
            {
                try
                {
                    encryptedPassword = roamingSettings.Values["password"].ToString();
                    passwordInput.Password = "********";
                }
                catch (Exception e)
                {
                    // ignore errors...
                }
            }
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache.  Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="pageState">An empty dictionary to be populated with serializable state.</param>
        protected override void SaveState(Dictionary<String, Object> pageState)
        {
            if (credsHaveChanged)
            {
                CredentialsEncrypter encrypter = new CredentialsEncrypter();
                Windows.Storage.ApplicationDataContainer roamingSettings = Windows.Storage.ApplicationData.Current.RoamingSettings;
                roamingSettings.Values["username"] = encrypter.Encrypt(usernameInput.Text);
                encryptedPassword = encrypter.Encrypt(passwordInput.Password);
                roamingSettings.Values["password"] = encryptedPassword;
            }
        }

        private async void Login(object sender, RoutedEventArgs e)
        {
            SaveState(null);

            CredentialsEncrypter decrypter = new CredentialsEncrypter();
            nodeConnector.Creds = new NetworkCredential(usernameInput.Text, decrypter.Decrypt(encryptedPassword));
            try
            {
                await nodeConnector.refresh();

                ImakeMaker imageMaker = new ImakeMaker(nodeConnector.UsageBytes, nodeConnector.QuotaBytes, nodeConnector.RolloverDate, nodeConnector.StartDate);
                StorageFile squareFile = await imageMaker.getSquareImage();
                StorageFile wideFile = await imageMaker.getWideImage();
                string alt = "Usage: " + nodeConnector.UsageBytes / 1000000000 + " / " + nodeConnector.QuotaBytes / 1000000000 + " GB\n" + "Rollover: " + nodeConnector.RolloverDate.ToString("d");

                XmlDocument tileXmlWide = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWideImageAndText02);
                XmlNodeList wideTileImageAttributes = tileXmlWide.GetElementsByTagName("image");
                ((XmlElement)wideTileImageAttributes[0]).SetAttribute("src", "ms-appdata:///local/images/wide/" + wideFile.Name);
                ((XmlElement)wideTileImageAttributes[0]).SetAttribute("alt", alt);
                XmlNodeList wideTileTextAttributes = tileXmlWide.GetElementsByTagName("text");
                ((XmlElement)wideTileTextAttributes[0]).InnerText = "Data Remaining: " + (nodeConnector.QuotaBytes - nodeConnector.UsageBytes) / 1000000000 + " GB";
                ((XmlElement)wideTileTextAttributes[1]).InnerText = "Time Remaining: " + (new TimeSpan(nodeConnector.RolloverDate.Ticks - DateTime.Now.Ticks)).Days + " days";

                XmlDocument tileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquarePeekImageAndText03);
                XmlNodeList tileImageAttributes = tileXml.GetElementsByTagName("image");
                ((XmlElement)tileImageAttributes[0]).SetAttribute("src", "ms-appdata:///local/images/square/" + squareFile.Name);
                ((XmlElement)tileImageAttributes[0]).SetAttribute("alt", alt);
                XmlNodeList tileTextAttributes = tileXml.GetElementsByTagName("text");
                ((XmlElement)tileTextAttributes[0]).InnerText = "Data Remaining: ";
                ((XmlElement)tileTextAttributes[1]).InnerText = (nodeConnector.QuotaBytes - nodeConnector.UsageBytes) / 1000000000 + " GB";
                ((XmlElement)tileTextAttributes[2]).InnerText = "Time Remaining: ";
                ((XmlElement)tileTextAttributes[3]).InnerText = (new TimeSpan(nodeConnector.RolloverDate.Ticks - DateTime.Now.Ticks)).Days + " days";

                IXmlNode node = tileXml.ImportNode(tileXmlWide.GetElementsByTagName("binding").Item(0), deep: true);
                tileXml.GetElementsByTagName("visual").Item(0).AppendChild(node);

                TileNotification tileNotification = new TileNotification(tileXml);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(tileNotification);

                nope.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            catch (Exception ex)
            {
                //todo: handle error
            }
            return;
        }

        private void credsChanged(object sender, RoutedEventArgs e)
        {
            this.credsHaveChanged = true;
        }
    }
}
