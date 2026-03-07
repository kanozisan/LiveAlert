using System.IO;
using System.Windows;

namespace LiveAlert.Windows.Services;

public static class AppAssets
{
    private static readonly Uri ResourceBaseUri = new("pack://application:,,,/", UriKind.Absolute);

    public static Uri DefaultVoiceUri => CreateResourceUri("Assets/voice_live.wav");

    public static Uri DefaultBgmUri => CreateResourceUri("Assets/bgm.mp3");

    public static Uri AboutTextUri => CreateResourceUri("Assets/readme_windows.txt");

    public static Uri LicenseTextUri => CreateResourceUri("Assets/assets_readme.txt");

    public static Uri CenterFontBaseUri => ResourceBaseUri;

    public static string CenterFontFamilyPath => "./Assets/#Tsukuhou Shogo Mincho OFL H";

    public static string ReadText(Uri resourceUri)
    {
        using var stream = OpenResourceStream(resourceUri);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static Stream OpenResourceStream(Uri resourceUri)
    {
        var streamInfo = System.Windows.Application.GetResourceStream(resourceUri);
        if (streamInfo?.Stream is null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {resourceUri}");
        }

        return streamInfo.Stream;
    }

    private static Uri CreateResourceUri(string relativePath)
    {
        return new Uri(ResourceBaseUri, relativePath);
    }
}
