using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace APP.Services;

public class YoloResult
{
    public bool IsDetected { get; set; }
    public float Confidence { get; set; }
    public float FireAreaPercentage { get; set; } 
}

public class YoloService : IDisposable
{
    private InferenceSession? _yoloSession;
    private bool _isInitialized = false;

    public bool IsInitialized => _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("yolo.onnx");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            _yoloSession = new InferenceSession(ms.ToArray());
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"yolo ny gagal di init: {ex.Message}");
            throw;
        }
    }

    public async Task<YoloResult> DetectFireAsync(byte[] imageBytes, float confidenceThreshold = 0.6f)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        try
        {
            using var originalMat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            if (originalMat.Empty()) return new YoloResult { IsDetected = false };

            int targetWidth = 640;
            int targetHeight = 640;

            var inputTensor = ConvertMatToYoloTensor(originalMat, targetWidth, targetHeight);

            var inputName = _yoloSession!.InputMetadata.Keys.First();
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var results = _yoloSession.Run(inputs);
            var output = results.First();
            var tensor = output.AsTensor<float>();

            return ParseYoloOutputForFire(tensor, confidenceThreshold);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ada error dari yolo: {ex.Message}");
            return new YoloResult { IsDetected = false };
        }
    }

    private YoloResult ParseYoloOutputForFire(Microsoft.ML.OnnxRuntime.Tensors.Tensor<float> tensor, float threshold)
    {
        var dims = tensor.Dimensions;
        bool detected = false;
        float maxConf = 0f;
        float totalAreaPercent = 0f;

        if (dims.Length == 3)
        {
            int dim1 = dims[1];
            int dim2 = dims[2];

            if (dim1 < dim2) 
            {
                for (int col = 0; col < dim2; col++)
                {
                    for (int classIdx = 4; classIdx < dim1; classIdx++)
                    {
                        float score = tensor[0, classIdx, col];
                        if (score >= threshold)
                        {
                            detected = true;
                            if (score > maxConf) maxConf = score;

                            float w = tensor[0, 2, col];
                            float h = tensor[0, 3, col];

                            float normW = w > 1.0f ? w / 640f : w;
                            float normH = h > 1.0f ? h / 640f : h;

                            float area = normW * normH * 100f; 
                            if (area > totalAreaPercent)
                            {
                                totalAreaPercent = area; 
                            }
                        }
                    }
                }
            }
            else 
            {
                for (int row = 0; row < dim1; row++)
                {
                    float objConf = tensor[0, row, 4];
                    if (objConf >= threshold)
                    {
                        for (int classIdx = 5; classIdx < dim2; classIdx++)
                        {
                            float classProb = tensor[0, row, classIdx];
                            float finalScore = objConf * classProb;
                            if (finalScore >= threshold)
                            {
                                detected = true;
                                if (finalScore > maxConf) maxConf = finalScore;

                                float w = tensor[0, row, 2];
                                float h = tensor[0, row, 3];

                                float normW = w > 1.0f ? w / 640f : w;
                                float normH = h > 1.0f ? h / 640f : h;

                                float area = normW * normH * 100f;
                                if (area > totalAreaPercent)
                                {
                                    totalAreaPercent = area;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (totalAreaPercent > 100f) totalAreaPercent = 100f;

        return new YoloResult
        {
            IsDetected = detected,
            Confidence = maxConf,
            FireAreaPercentage = totalAreaPercent
        };
    }

    private DenseTensor<float> ConvertMatToYoloTensor(Mat img, int width, int height)
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
                tensor[0, 0, y, x] = color.Item0 / 255.0f;
                tensor[0, 1, y, x] = color.Item1 / 255.0f; 
                tensor[0, 2, y, x] = color.Item2 / 255.0f; 
            }
        }

        return tensor;
    }

    public void Dispose()
    {
        _yoloSession?.Dispose();
    }
}