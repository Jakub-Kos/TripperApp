using System;
using System.Threading.Tasks;
using System.Windows;
using TripPlanner.Wpf.ViewModels;

namespace TripPlanner.Wpf.Views;

public partial class SelectPlaceholderDialog : Window
{
    public SelectPlaceholderDialog(SelectPlaceholderViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Close = ok => { DialogResult = ok; Close(); };
        Loaded += async (_, _) => await SafeLoadAsync(vm);
    }

    private static async Task SafeLoadAsync(SelectPlaceholderViewModel vm)
    {
        try { await vm.LoadAsync(); }
        catch (Exception ex) { vm.Status = ex.Message; }
    }
}
