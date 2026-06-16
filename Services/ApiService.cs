using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace APP.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    private const string BaseUrl = "http://localhost/api/";

    public ApiService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<bool> RegisterAsync(string username, string password, float[] embedding)
    {
        try
        {
            var url = $"{BaseUrl}register.php";
            var faceJson = JsonSerializer.Serialize(embedding);

            var values = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "face", faceJson } 
            };

            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                return responseString.Contains("success") || responseString.Contains("true");
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ada error di regis: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LoginAsync(string username, string password, float[] embedding)
    {
        try
        {
            var url = $"{BaseUrl}login.php";
            var faceJson = JsonSerializer.Serialize(embedding);

            var values = new Dictionary<string, string>
            {
                { "username", username },
                { "password", password },
                { "face", faceJson } 
            };

            var content = new FormUrlEncodedContent(values);
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                return responseString.Contains("success") || responseString.Contains("true");
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ada error di login: {ex.Message}");
            return false;
        }
    }
}