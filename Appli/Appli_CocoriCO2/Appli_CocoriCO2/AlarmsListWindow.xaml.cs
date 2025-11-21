using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Appli_CocoriCO2
{
    /// <summary>
    /// Logique d'interaction pour AlarmsListWindow.xaml
    /// </summary>
    public partial class AlarmsListWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        public List<Alarme> alarms;

        public GridViewColumnHeader _lastHeaderClicked = null;
        public ListSortDirection _lastDirection = ListSortDirection.Descending;
        public AlarmsListWindow()
        {
            InitializeComponent();
            alarms = MW.alarms.ToList<Alarme>();
            AlarmsList.ItemsSource = alarms;
            Sort("raised", _lastDirection);
        }


        private void lvColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else
                        {
                            direction = ListSortDirection.Ascending;
                        }
                    }

                    string header = headerClicked.Tag as string;
                    Sort(header, direction);

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }

        public void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
              CollectionViewSource.GetDefaultView(AlarmsList.ItemsSource);

            dataView.SortDescriptions.Clear();
            SortDescription sd = new SortDescription(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void btnAckClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            Alarme al = button.DataContext as Alarme;
            Alarme a = alarms.Single(alarm => alarm.libelle == al.libelle);
            a.triggered = false;
            a.raised = false;
            a.acknowledged = true;
            a.dtAcknowledged = DateTime.Now;

            if (_lastHeaderClicked != null) Sort(_lastHeaderClicked.Tag as string, _lastDirection);
            else Sort("raised", _lastDirection);

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

    }

}

