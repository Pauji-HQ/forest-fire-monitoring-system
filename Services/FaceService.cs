using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace APP.Services;

public partial class FaceService : IDisposable
{
    private InferenceSession? _scrfdSession;
    private InferenceSession? _arcfaceSession;
    private InferenceSession? _minifasnetSession;
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            using var scrfdStream = await FileSystem.OpenAppPackageFileAsync("scrfd.onnx");
            using var scrfdMs = new MemoryStream();
            await scrfdStream.CopyToAsync(scrfdMs);
            _scrfdSession = new InferenceSession(scrfdMs.ToArray());

            using var arcfaceStream = await FileSystem.OpenAppPackageFileAsync("arcface.onnx");
            using var arcfaceMs = new MemoryStream();
            await arcfaceStream.CopyToAsync(arcfaceMs);
            _arcfaceSession = new InferenceSession(arcfaceMs.ToArray());

            using var miniStream = await FileSystem.OpenAppPackageFileAsync("minifasnet.onnx");
            using var miniMs = new MemoryStream();
            await miniStream.CopyToAsync(miniMs);
            _minifasnetSession = new InferenceSession(miniMs.ToArray());

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"file onnx pada error: {ex.Message}");
            throw;
        }
    }

    public async Task<float[]> ExtractEmbeddingAsync(byte[] imageBytes)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        using var originalMat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (originalMat.Empty())
        {
            throw new Exception("Gambar tidak valid atau gagal didecode oleh OpenCV");
        }

        Rect rawFaceRect = DetectRawFaceRect(originalMat);

        //using var wideCroppedFace = CropFaceWithScale(originalMat, rawFaceRect, 2.7f);

        if (_minifasnetSession != null)
        {
            using var wideCroppedFace = CropFaceWithScale(originalMat, rawFaceRect, 2.7f);
            bool isRealFace = CheckLiveness(wideCroppedFace);
            if (!isRealFace)
            {
                throw new Exception("Wajah terindikasi sebagai foto atau layar HP");
            }
        }

        //try
        //{
        //    string debugPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "potongan_muka.jpg");
        //    Cv2.ImWrite(debugPath, wideCroppedFace);
        //    System.Diagnostics.Debug.WriteLine($"fotonya ada di: {debugPath}");
        //}
        //catch (Exception ex)
        //{
        //    System.Diagnostics.Debug.WriteLine($"gagak nyimpen ada error: {ex.Message}");
        //}

        using var normalCroppedFace = CropFaceWithScale(originalMat, rawFaceRect, 1.2f);

        float[] embedding = RunArcFaceInference(normalCroppedFace);
        return embedding;
    }

    private Rect DetectRawFaceRect(Mat original)
    {
        int inputWidth = 640;
        int inputHeight = 640;

        var inputTensor = ConvertMatToTensor(original, inputWidth, inputHeight, normMeanStd: false);

        var inputName = _scrfdSession!.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

        using var results = _scrfdSession.Run(inputs);
        return ParseScrfdOutputs(results, original.Width, original.Height);
    }

     private static Mat CropFaceWithScale(Mat original, Rect faceRect, float scaleFactor)
    {
        int src_h = original.Height;
        int src_w = original.Width;

        int x = faceRect.X;
        int y = faceRect.Y;
        int box_w = faceRect.Width;
        int box_h = faceRect.Height;

        if (box_w <= 0 || box_h <= 0)
        {
            int fallbackSize = Math.Min(src_w, src_h);
            int fallbackX = (src_w - fallbackSize) / 2;
            int fallbackY = (src_h - fallbackSize) / 2;
            return new Mat(original, new Rect(fallbackX, fallbackY, fallbackSize, fallbackSize));
        }

        float scale = Math.Min(Math.Min((float)(src_h - 1) / box_h, (float)(src_w - 1) / box_w), scaleFactor);

        float new_w = box_w * scale;
        float new_h = box_h * scale;

        float center_x = x + box_w / 2.0f;
        float center_y = y + box_h / 2.0f;

        int x1 = Math.Max(0, (int)(center_x - new_w / 2.0f));
        int y1 = Math.Max(0, (int)(center_y - new_h / 2.0f));
        int x2 = Math.Min(src_w - 1, (int)(center_x + new_w / 2.0f));
        int y2 = Math.Min(src_h - 1, (int)(center_y + new_h / 2.0f));

        int crop_w = x2 - x1;
        int crop_h = y2 - y1;

        if (crop_w <= 0 || crop_h <= 0)
        {
            return new Mat(original, faceRect);
        }

        return new Mat(original, new Rect(x1, y1, crop_w, crop_h));
    }

    private static Rect ParseScrfdOutputs(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, int origWidth, int origHeight)
    {
        try
        {
            var boxOutput = results.FirstOrDefault(r => r.Name.Contains("bbox") || r.Name.Contains("output"));
            if (boxOutput != null)
            {
                var tensor = boxOutput.AsTensor<float>();
                if (tensor.Dimensions.Length >= 2 && tensor.Dimensions[1] >= 4)
                {
                    float x1 = tensor[0, 0] * origWidth / 640f;
                    float y1 = tensor[0, 1] * origHeight / 640f;
                    float x2 = tensor[0, 2] * origWidth / 640f;
                    float y2 = tensor[0, 3] * origHeight / 640f;

                    return new Rect((int)x1, (int)y1, (int)(x2 - x1), (int)(y2 - y1));
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"gagal parsing output ny scrfd: {ex.Message}");
        }

        return new Rect(0, 0, 0, 0);
    }

    private bool CheckLiveness(Mat faceMat)
    {
        if (_minifasnetSession == null) return true;

        try
        {
            int size = 80;
            var tensor = ConvertMatToLivenessTensor(faceMat, size, size);

            var inputName = _minifasnetSession.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

            using var results = _minifasnetSession.Run(inputs);
            var output = results[0].AsTensor<float>().ToArray();

            if (output.Length >= 2)
            {
                int maxIndex = 0;
                float maxValue = output[0];
                for (int i = 1; i < output.Length; i++)
                {
                    if (output[i] > maxValue)
                    {
                        maxValue = output[i];
                        maxIndex = i;
                    }
                }

                string valuesStr = string.Join(", ", output.Select((v, idx) => $"[{idx}]: {v:F4}"));
                System.Diagnostics.Debug.WriteLine($"[Liveness-New] Indeks Dominan: {maxIndex} (1 adalah Asli) | Nilai -> {valuesStr}");

                return maxIndex == 1;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error pada deteksi Liveness: {ex.Message}");
        }
        return true;
    }

    private float[] RunArcFaceInference(Mat faceMat)
    {
        int targetSize = 112;
        var tensor = ConvertMatToTensor(faceMat, targetSize, targetSize, normMeanStd: true);

        var inputName = _arcfaceSession!.InputMetadata.Keys.First();
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, tensor)
        };

        using var results = _arcfaceSession.Run(inputs);
        var embedding = results.First().AsTensor<float>().ToArray();

        return NormalizeEmbedding(embedding);
    }

    private static float[] NormalizeEmbedding(float[] vector)
    {
        double norm = 0;
        for (int i = 0; i < vector.Length; i++)
        {
            norm += vector[i] * vector[i];
        }
        norm = Math.Sqrt(norm);

        float[] normalized = new float[vector.Length];
        if (norm > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                normalized[i] = (float)(vector[i] / norm);
            }
        }
        return normalized;
    }

    private static DenseTensor<float> ConvertMatToTensor(Mat img, int width, int height, bool normMeanStd)
    {
        using var resized = new Mat();
        Cv2.Resize(img, resized, new Size(width, height));

        using var floatMat = new Mat();
        resized.ConvertTo(floatMat, MatType.CV_32FC3);

        using var rgbMat = new Mat();
        Cv2.CvtColor(floatMat, rgbMat, ColorConversionCodes.BGR2RGB);

        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        var indexer = rgbMat.GetGenericIndexer<Vec3f>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = indexer[y, x];

                if (normMeanStd)
                {
                    tensor[0, 0, y, x] = (color.Item0 - 127.5f) / 128.0f;
                    tensor[0, 1, y, x] = (color.Item1 - 127.5f) / 128.0f; 
                    tensor[0, 2, y, x] = (color.Item2 - 127.5f) / 128.0f; 
                }
                else
                {
                    tensor[0, 0, y, x] = color.Item0;
                    tensor[0, 1, y, x] = color.Item1;
                    tensor[0, 2, y, x] = color.Item2;
                }
            }
        }

        return tensor;
    }

    private static DenseTensor<float> ConvertMatToLivenessTensor(Mat img, int width, int height)
    {
        using var resized = new Mat();
        Cv2.Resize(img, resized, new Size(width, height));

        using var floatMat = new Mat();
        resized.ConvertTo(floatMat, MatType.CV_32FC3);

        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        var indexer = floatMat.GetGenericIndexer<Vec3f>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = indexer[y, x];

                tensor[0, 0, y, x] = color.Item0;
                tensor[0, 1, y, x] = color.Item1; 
                tensor[0, 2, y, x] = color.Item2; 
            }
        }

        return tensor;
    }

    public void Dispose()
    {
        _scrfdSession?.Dispose();
        _arcfaceSession?.Dispose();
        _minifasnetSession?.Dispose();
        GC.SuppressFinalize(this); 
    }
}