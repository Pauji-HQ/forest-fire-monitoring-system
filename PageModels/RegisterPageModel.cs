using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using APP.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace APP.PageModels;

public class RegisterPageModel : INotifyPropertyChanged
{
    private readonly ApiService _apiService;
    private readonly FaceService _faceService;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _statusMessage = "Siap mendaftar";
    private Color _statusColor = Colors.Gray;
    private bool _isBusy;

    public RegisterPageModel(ApiService apiService, FaceService faceService)
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

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
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

    public async Task RegisterUserAsync(byte[] rawImageBytes)
    {
        if (Password != ConfirmPassword)
        {
            StatusMessage = "Password dan Konfirmasi Password tidak cocok!";
            StatusColor = Colors.Red;
            return;
        }

        try
        {
            StatusMessage = "Menganalisis karakteristik wajah...";
            StatusColor = Colors.Orange;

            float[] embedding = await Task.Run(() => _faceService.ExtractEmbeddingAsync(rawImageBytes));

            if (embedding == null || embedding.Length != 512)
            {
                StatusMessage = "Gagal mengambil deskriptor wajah yang stabil";
                StatusColor = Colors.Red;
                return;
            }

            StatusMessage = "Menyimpan data pendaftaran ke database server...";
            StatusColor = Colors.Orange;

            bool isSuccess = await _apiService.RegisterAsync(Username, Password, embedding);

            if (isSuccess)
            {
                StatusMessage = "Registrasi Berhasil!";
                StatusColor = Colors.Green;

                await Task.Delay(2000);

                Username = string.Empty;
                Password = string.Empty;
                StatusMessage = "Siap mendaftar";
                StatusColor = Colors.Gray;

                await Shell.Current.GoToAsync("//LoginPage");
            }
            else
            {
                StatusMessage = "Registrasi gagal username mungkin sudah ada";
                StatusColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Pendaftaran gagal: {ex.Message}";
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