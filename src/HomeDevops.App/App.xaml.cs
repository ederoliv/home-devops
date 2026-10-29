using System.IO;
using System.Windows;
using System.Windows.Threading;
using HomeDevops.Services;
using HomeDevops.ViewModels;
using HomeDevops.Views;

namespace HomeDevops;

public partial class App : Application
{
    private const string ProjectsFolderName = "HomeDevops Projects";
    private const string ConfigFolderName = ".homeDevops";

    private ProjectStore? _store;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        // Documentos\HomeDevops Projects (um .hdp por projeto) e <pasta pessoal>\.homeDevops\config.json.
        // As variáveis abaixo, se definidas, trocam as pastas (úteis para testar sem mexer nos dados reais).
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var projectsFolder = Environment.GetEnvironmentVariable("HOMEDEVOPS_PROJECTS_DIR")
            ?? Path.Combine(documents, ProjectsFolderName);
        var configFolder = Environment.GetEnvironmentVariable("HOMEDEVOPS_CONFIG_DIR")
            ?? Path.Combine(home, ConfigFolderName);

        var config = new ConfigService(configFolder);
        var theme = new ThemeService();
        // toda janela nova ganha a barra de título nas cores do tema assim que abre
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => theme.ApplyTitleBar((Window)sender)));
        theme.Apply(config.Get<AppearanceSettings>(ConfigSections.Appearance));

        _store = new ProjectStore(projectsFolder, new DispatcherSynchronizationContext(Dispatcher));
        var transfer = new ProjectTransfer(_store);

        var main = new MainViewModel(_store, transfer, config, theme, new DialogService());
        _store.SaveFailed += main.ShowToast;
        main.Load();

        new MainWindow { DataContext = main }.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _store?.Flush();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Home DevOps", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
