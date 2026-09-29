using BongoCatAutoClaim.Core;

namespace BongoCatAutoClaim.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var catalog = CompatibilityCatalog.LoadEmbedded();
            var storage = new AppStorage();
            Application.Run(new MainForm(new GameDetector(), new AutoClaimManager(catalog, storage)));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Bongo Cat Auto Claim", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
