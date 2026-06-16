namespace APP
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Width = 1000;
            window.Height = 600;

            window.MinimumWidth = 1020;
            window.MinimumHeight = 680;

            return window;
        }
    }
}