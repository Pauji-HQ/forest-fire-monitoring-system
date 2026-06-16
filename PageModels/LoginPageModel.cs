using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using APP.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace APP.PageModels;

public class LoginPageModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private readonly FaceService _faceService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _statusMessage = "Siap memverifikasi";
    private Color _statusColor = Colors.Gray;
    private bool _isBusy;

    public LoginPageModel(ApiService apiService, FaceService faceService)
    {
        _apiService = apiService;
        _faceService = faceService;
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public async Task LoginUserAsync(byte[] rawImageBytes)
    {
        try
        {
            StatusMessage = "Menganalisis kemiripan struktur wajah...";
            StatusColor = Colors.Orange;

            float[] embedding = await Task.Run(() => _faceService.ExtractEmbeddingAsync(rawImageBytes));

            if (embedding == null || embedding.Length != 512)
            {
                StatusMessage = "Gagal mengambil fitur unik wajah";
                StatusColor = Colors.Red;
                return;
            }

            StatusMessage = "Mencocokkan data kredensial di server...";
            StatusColor = Colors.Orange;

            bool isSuccess = await _apiService.LoginAsync(Username, Password, embedding);

            if (isSuccess)
            {
                StatusMessage = "Verifikasi Berhasil! Selamat Datang";
                StatusColor = Colors.Green;

                await Task.Delay(1500);

                Username = string.Empty;
                Password = string.Empty;
                StatusMessage = "Siap memverifikasi";
                StatusColor = Colors.Gray;

                await Shell.Current.GoToAsync("//DashboardPage");
            }
            else
            {
                StatusMessage = "Login Gagal pastikan username, password, dan wajah anda cocok";
                StatusColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Proses verifikasi gagal: {ex.Message}";
            StatusColor = Colors.Red;
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