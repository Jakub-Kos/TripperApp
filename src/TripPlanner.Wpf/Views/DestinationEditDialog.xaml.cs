using System.Windows;
using TripPlanner.Wpf.ViewModels;

namespace TripPlanner.Wpf.Views
{
    public partial class DestinationEditDialog : Window
    {
        public DestinationEditDialog(DestinationEditViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.Close = ok => { DialogResult = ok; Close(); };
        }
    }
}