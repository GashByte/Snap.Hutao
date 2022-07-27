﻿// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.Threading;
using Microsoft.Windows.AppLifecycle;
using Snap.Hutao.Core.Logging;
using Snap.Hutao.Extension;
using Snap.Hutao.Service.Abstraction;
using Snap.Hutao.Service.Metadata;
using System.Diagnostics;

namespace Snap.Hutao;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static Window? window;
    private readonly ILogger<App> logger;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        // load app resource
        InitializeComponent();
        InitializeDependencyInjection();

        // Notice that we already call InitializeDependencyInjection() above
        // so we can use Ioc here.
        logger = Ioc.Default.GetRequiredService<ILogger<App>>();

        UnhandledException += AppUnhandledException;
        DebugSettings.BindingFailed += XamlBindingFailed;
    }

    /// <summary>
    /// 当前窗口
    /// </summary>
    public static Window? Window { get => window; set => window = value; }

    /// <inheritdoc cref="Application"/>
    public static new App Current
    {
        get => (App)Application.Current;
    }

    /// <summary>
    /// <inheritdoc cref="Windows.Storage.ApplicationData.Current"/>
    /// </summary>
    [SuppressMessage("", "CA1822")]
    public Windows.Storage.ApplicationData AppData
    {
        get => Windows.Storage.ApplicationData.Current;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    [SuppressMessage("", "VSTHRD100")]
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        AppActivationArguments activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance firstInstance = AppInstance.FindOrRegisterForKey("main");
        firstInstance.Activated += OnActivated;

        if (!firstInstance.IsCurrent)
        {
            // Redirect the activation (and args) to the "main" instance, and exit.
            await firstInstance.RedirectActivationToAsync(activatedEventArgs);
            Process.GetCurrentProcess().Kill();
        }
        else
        {
            Window = Ioc.Default.GetRequiredService<MainWindow>();
            Window.Activate();

            logger.LogInformation(EventIds.CommonLog, "Cache folder : {folder}", AppData.TemporaryFolder.Path);

            Ioc.Default
                .GetRequiredService<IMetadataService>()
                .As<IMetadataInitializer>()?
                .InitializeInternalAsync()
                .SafeForget(logger);
        }
    }

    private static void InitializeDependencyInjection()
    {
        IServiceProvider services = new ServiceCollection()

            // Microsoft extension
            .AddLogging(builder => builder
                .AddDebug()
                .AddDatabase())
            .AddMemoryCache()

            // Hutao extensions
            .AddInjections()
            .AddDatebase()
            .AddHttpClients()
            .AddJsonSerializerOptions()

            // Discrete services
            .AddSingleton<IMessenger>(WeakReferenceMessenger.Default)

            .BuildServiceProvider();

        Ioc.Default.ConfigureServices(services);
    }

    private void AppUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        logger.LogError(EventIds.UnhandledException, e.Exception, "未经处理的异常");
    }

    private void OnActivated(object? sender, AppActivationArguments args)
    {
        if (args.TryGetProtocolActivatedUri(out Uri? uri))
        {
            Ioc.Default.GetRequiredService<IInfoBarService>().Information(uri.ToString());
        }
    }

    private void XamlBindingFailed(object sender, BindingFailedEventArgs e)
    {
        logger.LogCritical(EventIds.XamlBindingError, "XAML绑定失败: {message}", e.Message);
    }
}
