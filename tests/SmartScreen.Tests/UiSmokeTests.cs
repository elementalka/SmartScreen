using System.Runtime.ExceptionServices;
using System.Windows;
using SmartScreen.App;
using SmartScreen.App.Services;
using SmartScreen.Domain.Models;
using DomainThemeMode = SmartScreen.Domain.Enums.ThemeMode;

namespace SmartScreen.Tests;

[TestClass]
public sealed class UiSmokeTests
{
    [TestMethod]
    public void MainWindowLoadsWithThemeAndLocalizationResources()
    {
        RunOnSta(() =>
        {
            var application = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            try
            {
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/SmartScreen.App;component/Resources/Styles/AppStyles.xaml",
                        UriKind.Absolute)
                });

                LocalizationResourceService.Apply(new Dictionary<string, string>
                {
                    ["app.title"] = "SmartScreen Test",
                    ["main.workspace"] = "Workspace Test"
                });

                ThemeResourceService.Apply(new ThemeSettings
                {
                    Mode = DomainThemeMode.Dark,
                    AccentColor = "#38BDF8"
                });

                var window = new MainWindow();
                try
                {
                    window.UpdateLayout();

                    Assert.AreEqual("SmartScreen Test", window.Title);
                    Assert.IsNotNull(application.Resources["TextBrush"]);
                    Assert.IsNotNull(application.Resources["Loc.main.workspace"]);
                    Assert.IsNotNull(window.Content);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                application.Shutdown();
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caught)
            {
                exception = ExceptionDispatchInfo.Capture(caught);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }
}
