using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using APP.PageModels;
using Camera.MAUI;

namespace APP.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginPageModel _viewModel;

    public LoginPage(LoginPageModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        cameraView.CamerasLoaded += CameraView_CamerasLoaded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await StartCamera();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await StopCamera();
    }

    private void CameraView_CamerasLoaded(object? sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 0)
        {
            var frontCam = cameraView.Cameras.FirstOrDefault(c => c.Position == CameraPosition.Front);
            cameraView.Camera = frontCam ?? cameraView.Cameras.First();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await StartCamera();
            });
        }
    }

    private async Task StartCamera()
    {
        try
        {
            if (cameraView.Camera != null)
            {
                await cameraView.StopCameraAsync();
                await cameraView.StartCameraAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"kamera error: {ex.Message}");
        }
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        double winW = this.Width;
        double winH = this.Height;

        if (winW <= 0 || winH <= 0) return;

        double scale = winH / 700.0;

        double clampedScale = Math.Max(1.0, Math.Min(2.0, scale));

        mainCard.Scale = clampedScale;
    }
    private async Task StopCamera()
    {
        try
        {
            if (cameraView.Camera != null)
            {
                await cameraView.StopCameraAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"gagal matiin kamera: {ex.Message}");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Username) || string.IsNullOrWhiteSpace(_viewModel.Password))
        {
            _viewModel.StatusMessage = "Username dan Password tidak boleh kosong!";
            _viewModel.StatusColor = Colors.Red;
            return;
        }

        _viewModel.IsBusy = true;
        _viewModel.StatusMessage = "Mengambil snapshot wajah...";
        _viewModel.StatusColor = Colors.Blue;

        try
        {
            using var stream = await cameraView.TakePhotoAsync(Camera.MAUI.ImageFormat.JPEG);
            if (stream == null)
            {
                _viewModel.StatusMessage = "Gagal menangkap gambar dari kamera";
                _viewModel.StatusColor = Colors.Red;
                _viewModel.IsBusy = false;
                return;
            }

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            byte[] faceBytes = memoryStream.ToArray();

            await _viewModel.LoginUserAsync(faceBytes);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Kesalahan sistem login: {ex.Message}";
            _viewModel.StatusColor = Colors.Red;
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    private async void OnGoToRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//RegisterPage");
    }
}