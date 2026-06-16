using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using APP.PageModels;
using Camera.MAUI;

namespace APP.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardPageModel _viewModel;
    private bool _isFrameLoopActive = false;

    public DashboardPage(DashboardPageModel viewModel)
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

        await _viewModel.InitializeAsync();

        StartFrameLoop();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        StopFrameLoop();
        await StopCamera();
        _viewModel.Cleanup();
    }

    private void CameraView_CamerasLoaded(object? sender, EventArgs e)
    {
        if (cameraView.Cameras.Count > 0)
        {
            cameraView.Camera = cameraView.Cameras[0];
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

        double camH = winH * 0.45;
        cameraBorder.HeightRequest = Math.Clamp(camH, 200, 350);
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
            System.Diagnostics.Debug.WriteLine($"kamera gagal mati: {ex.Message}");
        }
    }

    private async void StartFrameLoop()
    {
        if (_isFrameLoopActive) return;
        _isFrameLoopActive = true;

        while (_isFrameLoopActive)
        {
            await Task.Delay(300);
            if (!_isFrameLoopActive) break;

            try
            {
                using var stream = await cameraView.TakePhotoAsync(Camera.MAUI.ImageFormat.JPEG);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    byte[] frameBytes = ms.ToArray();

                    await _viewModel.AnalyzeFrameAsync(frameBytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ada error pas ngambil frame: {ex.Message}");
            }
        }
    }

    private void StopFrameLoop()
    {
        _isFrameLoopActive = false;
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        StopFrameLoop();
        await StopCamera();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}