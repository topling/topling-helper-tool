﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ToplingHelperModels.Models;

namespace ToplingHelper.Ava.Views;

public partial class TodisResult : Window
{

    public Instance Instance { get; init; } = default!;
    public ToplingConstants ToplingConstants { get; init; } = default!;

    public TodisResult()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)!;
        Owner = window.MainWindow;
        InitializeComponent();



    }
}