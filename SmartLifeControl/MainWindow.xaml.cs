using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace SmartLifeControl
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HomeAssistantClient client = new HomeAssistantClient();

        string credentialsFile = "credentials.txt";
        string tokenFile = "token.json";
        string devicesPath = "devices.json";

        string appFolderPath = "SmartLifeControl";

        public MainWindow()
        {
            

            InitPaths();

            InitializeComponent();

            Setup();

            client.discoveryOnCooldown += (s, _) =>
            {
                ShowDiscoveryAlert();
            };
        }

        private void InitPaths()
        {
            appFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/" + appFolderPath;

            if(!Directory.Exists(appFolderPath)) Directory.CreateDirectory(appFolderPath);

            tokenFile = appFolderPath + "/" + tokenFile;
            credentialsFile = appFolderPath + "/" + credentialsFile;
            devicesPath = appFolderPath + "/" + devicesPath;

            client.TokenFilePath = tokenFile;
            client.DeviceFilePath = devicesPath;
        }


        private void Setup()
        {
            if (UserLoggedIn())
            {
                LoginGrid.Visibility = Visibility.Collapsed;
                UpdateDeviceList();
                SetWelcomeLabel();
            }
        }

        private void SetWelcomeLabel()
        {
            var encryptedCredentials = File.ReadAllText(credentialsFile);
            var decryptedCredentials = EncryptDecrypt(encryptedCredentials);

            var login = decryptedCredentials.Split('|')[0];

            WelcomeLabel.Content = "Welcome, " + login + "!";
        }


        private async void UpdateDeviceList(bool forceUpdate = false)
        {


            var devices = await GetDevices(forceUpdate);



            if (devices.Count > 0) TileGrid.Children.Clear();

            foreach (var device in devices)
            {
                var tile = new ScenarioTile();
                tile.SetTileText(device.Item1);
                tile.id = device.Item2;
                tile.SetTileColor(ColorGen.GenerateVibrantColor(device.Item2));

                tile.TileClicked += async (s, _) =>
                {
                    await client.DeviceControl(tile.id, true);
                };

                TileGrid.Children.Add(tile);
            }

            if (forceUpdate && devices.Count > 0)
            {
                ShowDiscoveryAlert("Devices refreshed!");
            }
        }

        private bool UserLoggedIn()
        {


            if (File.Exists(tokenFile))
            {
                client.LoadTokenFromFile(tokenFile);
                var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (currentUnixTime >= client._token.Expires)
                {
                    var encryptedCredentials = File.ReadAllText(credentialsFile);
                    var decryptedCredentials = EncryptDecrypt(encryptedCredentials);

                    var login = decryptedCredentials.Split('|')[0];
                    var password = decryptedCredentials.Split('|')[1];

                    client.Login(login, password).RunSynchronously();
                }

                return true;
            }

            return false;
        }


        public async Task<List<(string, string)>> GetDevices(bool forceUpdate = false)
        {
            try
            {

                if (!File.Exists(devicesPath) || forceUpdate)
                {
                    await client.DeviceDiscovery();
                }

                var devicesDict = client.LoadDevicesFromFile(devicesPath);

                if (devicesDict == null || !devicesDict.ContainsKey("devices"))
                {
                    Console.WriteLine("No devices to display.");
                    return new List<(string, string)>();
                }

                var devicesArray = devicesDict["devices"] as JArray;
                if (devicesArray == null || devicesArray.Count == 0)
                {
                    Console.WriteLine("No devices found.");
                    return new List<(string, string)>();
                }

                var devicesList = new List<(string, string)>();

                foreach (var device in devicesArray)
                {
                    var deviceDict = device.ToObject<Dictionary<string, object>>();
                    devicesList.Add((deviceDict["name"].ToString(), deviceDict["id"].ToString()));
                }

                return devicesList;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error displaying devices: {ex.Message}");
                return new List<(string, string)>();
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var login = LoginTextbox.Text;
            var password = PasswordTextbox.Text;

            var credString = login + "|" + password;
            var encryptedCredentials = EncryptDecrypt(credString);

            File.WriteAllText(credentialsFile, encryptedCredentials);

            await client.Login(login, password);

            UpdateDeviceList();
            SetWelcomeLabel();
            SwipeUpAndFadeOut(LoginGrid);

        }

        public void ShowDiscoveryAlert(string text = "")
        {

            if (string.IsNullOrEmpty(text))
            {
                DiscoveryAlertText.Content = "You can refresh devices only once per 17 minutes!";
            }
            else
            {
                DiscoveryAlertText.Content = text;
            }

            DiscoveryAlert.Opacity = 1;
            DiscoveryAlert.Visibility = Visibility.Visible;

            Task.Run(async () =>
            {
                await Task.Delay(3000);
                Dispatcher.Invoke(new Action(() =>
                {
                    SwipeUpAndFadeOut(DiscoveryAlert);
                }));
            });
        }

        public void SwipeUpAndFadeOut(UIElement element)
        {
            // Create a TranslateTransform for upward movement
            TranslateTransform translateTransform = new TranslateTransform();
            element.RenderTransform = translateTransform;

            // Animation for the upward movement (Y axis)
            DoubleAnimation moveUpAnimation = new DoubleAnimation
            {
                From = 0,              // Start at original position
                To = -100,             // Move up by 100 units
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new QuadraticEase() // Optional: for smoother animation
            };

            // Animation for the opacity change (fade-out)
            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 1,              // Start fully visible
                To = 0,                // End completely invisible
                Duration = TimeSpan.FromSeconds(0.5)
            };

            // Apply the animations
            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(moveUpAnimation);
            storyboard.Children.Add(opacityAnimation);

            storyboard.Completed += (s, _) =>
            {
                element.Visibility = Visibility.Collapsed;

                storyboard.Stop();
            };

            // Set the target properties for the animations
            Storyboard.SetTarget(moveUpAnimation, element);
            Storyboard.SetTargetProperty(moveUpAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));

            Storyboard.SetTarget(opacityAnimation, element);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(UIElement.OpacityProperty));

            // Start the animation
            storyboard.Begin();
        }

        public static string EncryptDecrypt(string input, string key = "SmartLifeAppXorCrypt")
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] resultBytes = new byte[inputBytes.Length];

            for (int i = 0; i < inputBytes.Length; i++)
            {
                resultBytes[i] = (byte)(inputBytes[i] ^ keyBytes[i % keyBytes.Length]);
            }

            return Encoding.UTF8.GetString(resultBytes);
        }

        private void RefreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateDeviceList(true);
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            File.Delete(credentialsFile);
            File.Delete(tokenFile);
            File.Delete(devicesPath);
            Environment.Exit(1);
        }
    }


    public static class ColorGen
    {
        public static Color GenerateVibrantColor(string seed)
        {
            // Ensure seed is not null or empty
            if (string.IsNullOrWhiteSpace(seed))
            {
                throw new ArgumentException("Seed cannot be null or empty.", nameof(seed));
            }

            // Create a hash code from the seed string
            int hashCode = seed.GetHashCode();

            // Map the hash code to hue values (0-360)
            float hue = (hashCode & 0xFFFF) % 360; // Use 0-360 for hue
            float saturation = 0.9f; // High saturation for vibrant colors
            float lightness = 0.7f; // Lightness to avoid dark colors

            // Convert HSL to RGB
            return HslToRgb(hue, saturation, lightness);
        }

        private static Color HslToRgb(float hue, float saturation, float lightness)
        {
            float c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            float x = c * (1 - Math.Abs((hue / 60f) % 2 - 1));
            float m = lightness - c / 2;

            float r, g, b;

            if (hue < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (hue < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (hue < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (hue < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (hue < 300)
            {
                r = x; g = 0; b = c;
            }
            else
            {
                r = c; g = 0; b = x;
            }

            int red = (int)((r + m) * 255);
            int green = (int)((g + m) * 255);
            int blue = (int)((b + m) * 255);

            return Color.FromArgb(200, (byte)red, (byte)green, (byte)blue);
        }
    }
}
