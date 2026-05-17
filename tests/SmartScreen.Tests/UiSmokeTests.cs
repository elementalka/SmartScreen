using System.Runtime.ExceptionServices;
using System.Windows;
using SmartScreen.Application.Abstractions;
using SmartScreen.App;
using SmartScreen.App.Services;
using SmartScreen.App.ViewModels;
using SmartScreen.App.Views;
using SmartScreen.Domain.Enums;
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
                    ["main.workspace"] = "Workspace Test",
                    ["firstRun.title"] = "First Run Test",
                    ["firstRun.start"] = "Begin Test"
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
                    Assert.AreEqual("Скріншот готовий", application.Resources["Loc.quick.ready"]);
                    Assert.AreEqual("Олівець (P)", application.Resources["Loc.editor.pen"]);
                    Assert.AreEqual("AI-панель", application.Resources["Loc.ai.panel"]);
                    Assert.IsNotNull(window.Content);
                }
                finally
                {
                    window.Close();
                }

                var wizard = new FirstRunWizardWindow(new FirstRunWizardViewModel(new FakeSettingsService()));
                try
                {
                    wizard.UpdateLayout();

                    Assert.AreEqual("First Run Test", wizard.Title);
                    Assert.IsNotNull(wizard.Content);
                }
                finally
                {
                    wizard.Close();
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

    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly AppSettings _settings = new()
        {
            Screenshots =
            {
                AfterCaptureActions =
                [
                    AfterCaptureAction.CopyImageToClipboard,
                    AfterCaptureAction.ShowQuickActions
                ]
            },
            Ai =
            {
                Providers =
                [
                    new AiProviderSettings
                    {
                        Id = "test",
                        DisplayName = "Test Provider",
                        IsEnabled = true
                    }
                ]
            }
        };

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AppSettings> ResetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_settings);
    }
}
