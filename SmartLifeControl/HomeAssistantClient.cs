using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class HomeAssistantClient
{
    private readonly HttpClient _client;
    public Token _token { get; private set; }
    private static readonly string DefaultRegion = "eu"; // Default region if not specified
    private static string tokenFilePath = "token.json"; // Path to save the token file
    private static string deviceFilePath = "devices.json"; // Path to save the devices file
    private static string baseUrl = $"https://px1.tuya{DefaultRegion}.com/homeassistant";

    public string TokenFilePath
    {
        get => tokenFilePath;
        set => tokenFilePath = value;
    }

    public string DeviceFilePath
    {
        get => deviceFilePath;
        set => deviceFilePath = value;
    }

    public event EventHandler discoveryOnCooldown;

    public HomeAssistantClient()
    {
        _client = new HttpClient();
    }


    private void EnsureSuccess(HttpResponseMessage response, string content)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Request failed with status code {response.StatusCode}");
        }

        var data = JsonConvert.DeserializeObject<JObject>(content);

        if (data.ContainsKey("access_token"))
        {
            return;
        }

        if (data["responseStatus"]?.ToString() == "error")
        {
            throw new Exception(data["errorMsg"]?.ToString());
        }

        var header = data["header"];
        if (header?["code"]?.ToString() != "SUCCESS")
        {
            if (header["msg"]?.ToString() == "you can discovery once in 1020 seconds")
            {
                discoveryOnCooldown?.Invoke(this, new EventArgs());
                return;
            }

            throw new Exception(header["msg"]?.ToString());
        }
    }

    private Token NormalizeToken(Dictionary<string, object> tokenData)
    {
        var expiresIn = Convert.ToInt32(tokenData["expires_in"]);
        return new Token
        {
            AccessToken = tokenData["access_token"].ToString(),
            RefreshToken = tokenData["refresh_token"].ToString(),
            Expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn
        };
    }

    public async Task Login(string userName, string password, string region = null)
    {
        region = region ?? DefaultRegion;

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("userName", userName),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("countryCode", "00"),
            new KeyValuePair<string, string>("bizType", "smart_life"),
            new KeyValuePair<string, string>("from", "tuya")
        });

        var response = await _client.PostAsync(baseUrl + "/auth.do", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, responseBody);

        var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
        _token = NormalizeToken(tokenData);

        // Save the token to a file
        SaveTokenToFile(tokenFilePath);
    }

    public async Task RefreshAuthToken()
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", _token.RefreshToken),
            new KeyValuePair<string, string>("rand", new Random().NextDouble().ToString())
        });

        var response = await _client.PostAsync(baseUrl + "/access.do", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, responseBody);

        var tokenData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseBody);
        _token = NormalizeToken(tokenData);

        // Save the refreshed token to a file
        SaveTokenToFile(tokenFilePath);
    }

    public async Task<Dictionary<string, object>> DeviceDiscovery()
    {
        var request = new
        {
            header = new
            {
                payloadVersion = 1,
                @namespace = "discovery",
                name = "Discovery"
            },
            payload = new
            {
                accessToken = _token.AccessToken
            }
        };

        var content = new StringContent(JsonConvert.SerializeObject(request));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.PostAsync(baseUrl + "/skill", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        EnsureSuccess(response, responseBody);

        var responseData = JsonConvert.DeserializeObject<JObject>(responseBody);

        // Ensure the response contains the payload
        var payload = responseData["payload"] as JObject;
        if (payload == null || !payload.ContainsKey("devices"))
        {
            throw new Exception("No devices found in the response payload.");
        }

        var devicesArray = payload["devices"] as JArray;
        if (devicesArray == null || devicesArray.Count == 0)
        {
            Console.WriteLine("No devices found.");
            return new Dictionary<string, object>();
        }

        var devices = devicesArray
            .Select(device => device.ToObject<Dictionary<string, object>>())
            .ToList();

        var deviceDict = new Dictionary<string, object> { { "devices", devices } };
        SaveDevicesToFile(deviceFilePath, deviceDict);

        return deviceDict;
    }




    public async Task DeviceControl(string deviceId, object fieldValue, string action = "turnOnOff", string fieldName = "value")
    {
        if (action == "turnOnOff" && fieldName == "value" && fieldValue is bool)
        {
            fieldValue = (bool)fieldValue ? 1 : 0;
        }

        var request = new
        {
            header = new
            {
                payloadVersion = 1,
                @namespace = "control",
                name = action
            },
            payload = new Dictionary<string, object>
            {
                { "accessToken", _token.AccessToken },
                { "devId", deviceId },
                { fieldName, fieldValue }
            }
        };

        var content = new StringContent(JsonConvert.SerializeObject(request));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await _client.PostAsync(baseUrl + "/skill", content);
        var responseBody = await response.Content.ReadAsStringAsync();
        EnsureSuccess(response, responseBody);
    }

    public void SaveTokenToFile(string filePath)
    {
        if (_token == null)
        {
            throw new InvalidOperationException("No token available to save.");
        }

        var json = JsonConvert.SerializeObject(_token);
        File.WriteAllText(filePath, json);
    }

    public void LoadTokenFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Token file not found.", filePath);
        }

        var json = File.ReadAllText(filePath);
        _token = JsonConvert.DeserializeObject<Token>(json);
    }

    public void SaveDevicesToFile(string filePath, Dictionary<string, object> devices)
    {
        try
        {
            var json = JsonConvert.SerializeObject(devices, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving devices to file: {ex.Message}");
        }
    }



    public Dictionary<string, object> LoadDevicesFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Device file not found.", filePath);
        }

        var json = File.ReadAllText(filePath);
        var devices = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

        return devices;
    }


}


public class Token
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public long Expires { get; set; }
}
