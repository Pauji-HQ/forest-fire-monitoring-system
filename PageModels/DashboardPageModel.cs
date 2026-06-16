using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using APP.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Plugin.Maui.Audio;

namespace APP.PageModels;

public class DashboardPageModel : INotifyPropertyChanged
{
    private readonly YoloService _yoloService;
    private readonly IAudioManager _audioManager;
    private readonly FuzzyService _fuzzyService;
    private readonly NeuralNetworkService _neuralNetworkService;
    private readonly GeminiService _geminiService;

    private IAudioPlayer? _alarmPlayer;

    private string _statusMessage = "Menghubungkan Kamera...";
    private Color _statusBgColor = Colors.Gray;
    private bool _isAlarmActive = false;
    private bool _isBusy = false;
    private bool _isAlarmLatching = false;

    private double _windSpeed = 15.0; 
    private double _fireArea = 0.0;
    private double _yoloConfidence = 0.0;
    private double _fireDangerIndex = 0.0;
    private double _estimatedSpreadRate = 0.0;

    private string _trainingStatus = "MLP siap dilatih";
    private string _publicReleaseText = "Laporan humas untuk warga setempat akan muncul saat api terdeteksi";
    private string _sitrepReportText = "Draft laporan untuk BPBD / BNPB akan muncul saat api terdeteksi";
    private bool _isNlpGenerating = false;

    public DashboardPageModel(
        YoloService yoloService,
        IAudioManager audioManager,
        FuzzyService fuzzyService,
        NeuralNetworkService neuralNetworkService,
        GeminiService geminiService)
    {
        _yoloService = yoloService;
        _audioManager = audioManager;
        _fuzzyService = fuzzyService;
        _neuralNetworkService = neuralNetworkService;
        _geminiService = geminiService;

        ResetAlarmCommand = new Command(ResetAlarm);
        TrainAiCommand = new Command(async () => await TrainAiAsync());
        GenerateNlpManualCommand = new Command(async () => await GenerateNlpReportsAsync());
    }

    #region Binding Properties
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Color StatusBgColor
    {
        get => _statusBgColor;
        set => SetProperty(ref _statusBgColor, value);
    }

    public bool IsAlarmActive
    {
        get => _isAlarmActive;
        set => SetProperty(ref _isAlarmActive, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
                OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    public bool IsNotBusy => !IsBusy;

    public double WindSpeed
    {
        get => _windSpeed;
        set
        {
            if (SetProperty(ref _windSpeed, value))
            {
                if (FireArea > 0)
                {
                    FireDangerIndex = _fuzzyService.CalculateDangerIndex(FireArea, _windSpeed);
                }
            }
        }
    }

    public double FireArea
    {
        get => _fireArea;
        set => SetProperty(ref _fireArea, value);
    }

    public double YoloConfidence
    {
        get => _yoloConfidence;
        set => SetProperty(ref _yoloConfidence, value);
    }

    public double FireDangerIndex
    {
        get => _fireDangerIndex;
        set => SetProperty(ref _fireDangerIndex, value);
    }

    public double EstimatedSpreadRate
    {
        get => _estimatedSpreadRate;
        set => SetProperty(ref _estimatedSpreadRate, value);
    }

    public string TrainingStatus
    {
        get => _trainingStatus;
        set => SetProperty(ref _trainingStatus, value);
    }

    public string PublicReleaseText
    {
        get => _publicReleaseText;
        set => SetProperty(ref _publicReleaseText, value);
    }

    public string SitrepReportText
    {
        get => _sitrepReportText;
        set => SetProperty(ref _sitrepReportText, value);
    }

    public bool IsNlpGenerating
    {
        get => _isNlpGenerating;
        set => SetProperty(ref _isNlpGenerating, value);
    }
    #endregion

    #region Commands
    public ICommand ResetAlarmCommand { get; }
    public ICommand TrainAiCommand { get; }
    public ICommand GenerateNlpManualCommand { get; }
    #endregion

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Menginisialisasi Model YOLO...";
        StatusBgColor = Colors.Gray;

        try
        {
            await _yoloService.InitializeAsync();

            if (!_isAlarmLatching)
            {
                StatusMessage = "Sistem Aman - Pemantauan Aktif";
                StatusBgColor = Colors.Green;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Inisialisasi Detektor Gagal: {ex.Message}";
            StatusBgColor = Colors.Red;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task AnalyzeFrameAsync(byte[] frameBytes)
    {
        if (IsBusy || _isAlarmLatching) return;

        IsBusy = true;

        YoloResult result = await Task.Run(() => _yoloService.DetectFireAsync(frameBytes));

        if (result.IsDetected)
        {
            FireArea = result.FireAreaPercentage;
            YoloConfidence = result.Confidence;

            FireDangerIndex = _fuzzyService.CalculateDangerIndex(FireArea, WindSpeed);

            EstimatedSpreadRate = _neuralNetworkService.Predict(FireArea, YoloConfidence, WindSpeed);

            TriggerAlarm();

            _ = Task.Run(async () => await GenerateNlpReportsAsync());
        }

        IsBusy = false;
    }

    public async Task GenerateNlpReportsAsync()
    {
        if (IsNlpGenerating) return;
        IsNlpGenerating = true;

        try
        {
            var (pub, sit) = await _geminiService.GenerateReportsAsync(
                FireArea, YoloConfidence, WindSpeed, FireDangerIndex, EstimatedSpreadRate);

            PublicReleaseText = pub;
            SitrepReportText = sit;
        }
        catch (Exception ex)
        {
            PublicReleaseText = $"Gagal memanggil asisten AI.";
            SitrepReportText = $"Kesalahan: {ex.Message}";
        }
        finally
        {
            IsNlpGenerating = false;
        }
    }

    private async Task TrainAiAsync()
    {
        IsBusy = true;
        TrainingStatus = "Sedang melatih MLP dengan Backpropagation...";

        await Task.Run(() =>
        {
            double[][] trainInputs = [
                [0.05, 0.60, 0.10], 
                [0.20, 0.75, 0.35], 
                [0.55, 0.85, 0.60],
                [0.85, 0.95, 0.85] 
            ];

            double[][] trainTargets = [
                [0.05],
                [0.25], 
                [0.65],
                [0.95]  
            ];

            double finalLoss = 0.0;
            int epochs = 1500;
            double learningRate = 0.15;

            for (int epoch = 0; epoch < epochs; epoch++)
            {
                double totalLoss = 0.0;
                for (int i = 0; i < trainInputs.Length; i++)
                {
                    totalLoss += _neuralNetworkService.Train(trainInputs[i], trainTargets[i], learningRate);
                }
                finalLoss = totalLoss / trainInputs.Length;
            }

            TrainingStatus = $"Selesai! Latihan {epochs} iterasi sukses. MSE Akhir: {finalLoss:F6}";
        });

        IsBusy = false;
    }

    private async void TriggerAlarm()
    {
        _isAlarmLatching = true;
        IsAlarmActive = true;
        StatusMessage = "WARNING: API TERDETEKSI!";
        StatusBgColor = Colors.Red;

        try
        {
            if (_alarmPlayer == null)
            {
                var audioStream = await FileSystem.OpenAppPackageFileAsync("alarm.wav");
                _alarmPlayer = _audioManager.CreatePlayer(audioStream);
                _alarmPlayer.Loop = true;
            }

            if (_alarmPlayer != null)
            {
                _alarmPlayer.Volume = 1.0;

                if (!_alarmPlayer.IsPlaying)
                {
                    _alarmPlayer.Play();
                    System.Diagnostics.Debug.WriteLine("oke alarm jalan");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"gagal jalanin alarm: {ex.Message}");
        }
    }

    private void ResetAlarm()
    {
        _isAlarmLatching = false;
        IsAlarmActive = false;
        StatusMessage = "Sistem Aman - Pemantauan Aktif";
        StatusBgColor = Colors.Green;

        try
        {
            if (_alarmPlayer != null && _alarmPlayer.IsPlaying)
            {
                _alarmPlayer.Stop();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"gagal reset alarm: {ex.Message}");
        }
    }

    public void Cleanup()
    {
        try
        {
            if (_alarmPlayer != null)
            {
                if (_alarmPlayer.IsPlaying) _alarmPlayer.Stop();
                _alarmPlayer.Dispose();
                _alarmPlayer = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"gagal beresin audio resource: {ex.Message}");
        }
    }

    #region Implementasi INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion
}