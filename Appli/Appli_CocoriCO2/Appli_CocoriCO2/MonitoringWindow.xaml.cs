
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
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
    /// Logique d'interaction pour MonitoringWindow.xaml
    /// </summary>
    public class DateModel
    {
        public DateTime DateTime { get; set; }
    public double Value { get; set; }
    }
public partial class MonitoringWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        public MonitoringWindow()
        {
            InitializeComponent();

            var dayConfig = Mappers.Xy<DateModel>()
          .X(dateModel => dateModel.DateTime.Ticks / TimeSpan.FromDays(1).Ticks)
          .Y(dateModel => dateModel.Value);

            seriesCollection = new SeriesCollection(dayConfig);


            seriesCollection.Add(new LineSeries
            {
                Title = "Series 1",
                Values = new ChartValues<DateModel>{
                 new DateModel
                    {
                        DateTime    = System.DateTime.UtcNow,
                        Value       = 5
                    },
                    new DateModel
                    {
                        DateTime    = System.DateTime.UtcNow.AddDays(1),
                        Value       = 9
                    },
                    new DateModel
                    {
                        DateTime    = System.DateTime.UtcNow.AddDays(2),
                        Value       = 4
                    }}
            });
            


            //Labels = new[] { "Jan", "Feb", "Mar", "Apr", "May" };
            //Labels = MW.Labels;
            YFormatter = value => value.ToString();

            Formatter = value => value.ToString();
            DataContext = this;
        }

        public SeriesCollection seriesCollection { get; set; }
        public Func<double, string> YFormatter { get; set; }
        public Func<double, string> Formatter { get; set; }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

    }
}
