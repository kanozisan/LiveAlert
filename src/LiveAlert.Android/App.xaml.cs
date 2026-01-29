namespace LiveAlert;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(MainPage page)
    {
        InitializeComponent();
        MainPage = page;
    }
}
