﻿using Avalonia;
using Avalonia.Controls;
using ToplingHelperModels;

namespace ToplingHelper.Ava.Views;

public partial class MyToplingResult : Window
{
    public ToplingConstants ToplingConstants { get; init; } = default!;
    public MyToplingResult()
    {
        InitializeComponent();
    }

}