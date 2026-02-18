using System.Windows;
using Frequify.ViewModels;
using System.IO;
using System;

namespace Frequify
{

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        LoadComponentFallback();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void LoadComponentFallback()
    {
        var resourceLocater = new Uri("/Frequify;component/mainwindow.xaml", UriKind.Relative);
        Application.LoadComponent(this, resourceLocater);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
        {
            return;
        }

        var path = files[0];
        var ext = Path.GetExtension(path);
        if (!string.Equals(ext, ".wav", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewModel.LoadFromPath(path);
    }
}
}