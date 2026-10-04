using BongoCatAutoClaim.Core;

namespace BongoCatAutoClaim.App;

internal static class Program
{
    private static readonly HttpClient UpdateHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var catalog = CompatibilityCatalog.LoadEmbedded();
            var storage = new AppStorage();
            var updateChecker = new UpdateChecker(new GitHubReleaseFeedClient(UpdateHttpClient));
            Application.Run(new MainForm(new GameDetector(), new AutoClaimManager(catalog, storage), updateChecker));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Bongo Cat Auto Claim", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
