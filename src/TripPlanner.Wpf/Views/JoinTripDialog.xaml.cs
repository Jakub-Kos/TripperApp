using System.Windows;
using TripPlanner.Wpf.ViewModels;

namespace TripPlanner.Wpf.Views
{
    public partial class JoinTripDialog : Window
    {
        public JoinTripDialog(JoinTripViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.Close = ok => { DialogResult = ok; Close(); };
        }
    }
}