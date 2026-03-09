using ConcurrentBooking.WpfClient.ViewModels;
using System.Windows;

namespace ConcurrentBooking.WpfClient;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.LoadDataCommand.Execute(null);
        }
    }
}
