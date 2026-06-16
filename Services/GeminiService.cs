using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace APP.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;

    private const string ApiKey = "API_KEY";

    public GeminiService()  
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<(string PublicRelease, string SitrepReport)> GenerateReportsAsync(
            double fireArea, double confidence, double windSpeed, double dangerIndex, double spreadRate)
    {

        var url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        var prompt = "Analisis situasi Kebakaran Hutan dan Lahan (Karhutla) berikut ini:\n" +
                     $"- Luas Bounding Box Api: {fireArea:F2}%\n" +
                     $"- Keyakinan Deteksi (YOLO): {confidence:P1}\n" +
                     $"- Kecepatan Angin Lokasi: {windSpeed:F1} km/jam\n" +
                     $"- Indeks Bahaya Kebakaran (Fuzzy): {dangerIndex:F1}%\n" +
                     $"- Laju Penyebaran Api (MLP AI): {spreadRate:F2} Hektar/Jam\n\n" +
                     "Buatlah dua laporan menggunakan Bahasa Indonesia:\n" +
                     "1. 'publicRelease': Siaran pers humas asisten yang informatif, mendalam, dan menenangkan namun waspada untuk masyarakat umum agar tidak panik dan siap evakuasi jika bahaya tinggi.\n" +
                     "2. 'sitrepReport': Draft Laporan Situasi (SITREP) taktis, padat, rapi, dan formal militer untuk dikirim ke BPBD/BNPB.\n\n" +
                     "Format output HARUS berupa JSON dengan struktur persis seperti ini:\n" +
                     "{\n" +
                     "  \"publicRelease\": \"tulis siaran pers di sini\",\n" +
                     "  \"sitrepReport\": \"tulis draft sitrep di sini\"\n" +
                     "}\n" +
                     "Kembalikan data langsung sebagai raw JSON tanpa menyertakan blok kode markdown ```json.";

        var payload = new
        {
            contents = new[]
           {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json"
            }
        };

        int maxRetries = 3;             
        int delayMilliseconds = 2000;    

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                var jsonString = JsonSerializer.Serialize(payload);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                request.Headers.Add("x-goog-api-key", ApiKey);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var textResult = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    if (!string.IsNullOrEmpty(textResult))
                    {
                        using var parsedOutput = JsonDocument.Parse(textResult);
                        string pub = parsedOutput.RootElement.GetProperty("publicRelease").GetString() ?? string.Empty;
                        string sit = parsedOutput.RootElement.GetProperty("sitrepReport").GetString() ?? string.Empty;
                        return (pub, sit);
                    }
                }

                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || (int)response.StatusCode == 429)
                {
                    System.Diagnostics.Debug.WriteLine($"[Gemini API] Server sibuk/terbatas (503/429). Mencoba ulang ke-{retry + 1} dalam {delayMilliseconds} ms...");

                    if (retry < maxRetries - 1)
                    {
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds *= 2; 
                        continue; 
                    }
                }

                var errorBody = await response.Content.ReadAsStringAsync();
                return ($"Error API ({response.StatusCode}): {errorBody}", "SITREP Gagal digenerate.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Gemini API] Kendala koneksi pada percobaan ke-{retry + 1}: {ex.Message}");
                if (retry == maxRetries - 1)
                {
                    return ($"Kesalahan sistem saat generate berita: {ex.Message}", $"Kesalahan SITREP: {ex.Message}");
                }
                await Task.Delay(delayMilliseconds);
                delayMilliseconds *= 2;
            }
        }

        return ("Server AI Google saat ini sedang sangat sibuk memproses antrean global. Silakan tekan tombol 'Mutakhirkan Siaran & SITREP' secara manual dalam beberapa saat.",
                "Laporan resmi (SITREP) tertunda karena kepadatan server Google.");
    }
}