using LiveCharts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Core;
using InfluxDB.Client.Writes;
using System.Threading;
using System.Net;
using System.Net.WebSockets;

namespace Appli_CocoriCO2
{
    /// <summary>
    /// Logique d'interaction pour ComDebugWindow.xaml
    /// </summary>
    public partial class ComDebugWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        DateTime lastFileWrite = DateTime.Now.ToUniversalTime();

        string token = Properties.Settings.Default["InfluxDBToken"].ToString();
        string bucket = Properties.Settings.Default["InfluxDBBucket"].ToString();
        string org = Properties.Settings.Default["InfluxDBOrg"].ToString();

        InfluxDBClient client;
        CancellationTokenSource cts = new CancellationTokenSource();

        public ComDebugWindow()
        {
            InitializeComponent();
#pragma warning disable CS4014 // Dans la mesure où cet appel n'est pas attendu, l'exécution de la méthode actuelle continue avant la fin de l'appel. Envisagez d'appliquer l'opérateur 'await' au résultat de l'appel.
            InitializeAsync();
#pragma warning restore CS4014 // Dans la mesure où cet appel n'est pas attendu, l'exécution de la méthode actuelle continue avant la fin de l'appel. Envisagez d'appliquer l'opérateur 'await' au résultat de l'appel.

            client = InfluxDBClientFactory.Create("http://localhost:8086", token.ToCharArray());
        }
        private static async Task RunPeriodicAsync(Action onTick,
                                          TimeSpan dueTime,
                                          TimeSpan interval,
                                          CancellationToken token)
        {
            // Initial wait time before we begin the periodic loop.
            if (dueTime > TimeSpan.Zero)
                await Task.Delay(dueTime, token);

            // Repeat this loop until cancelled.
            while (!token.IsCancellationRequested)
            {
                // Call our onTick function.
                onTick?.Invoke();

                // Wait to repeat again.
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, token);
            }
        }
        private async Task InitializeAsync()
        {
            int t;
            Int32.TryParse(Properties.Settings.Default["dataLogInterval"].ToString(), out t);
            var dueTime = TimeSpan.FromMinutes(t);
            var interval = TimeSpan.FromMinutes(t);

            // TODO: Add a CancellationTokenSource and supply the token here instead of None.
            await RunPeriodicAsync(saveData, dueTime, interval, cts.Token);
        }

        private void tb2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (tb2.Text.Length > 0)
            {
                //ReadData(tb2.Text);
            }
        }


        private double calculateSalinity(double cond)
        {
            double[] a = new double[] { 0.008, -0.1692, 25.3851, 14.0941, -7.0261, 2.7081 };
            double[] b = new double[] { 0.0005, -0.0056, -0.0066, -0.0375, 0.0636, -0.0144 };
            double[] c = new double[] { 0.6766097, 0.0200564, 0.000011043, -0.00000009698, 0.00000000010031 };

            double k = 0.0162;

            double rt = c[0] + c[1] * 25.0 + c[2] * 25.0 * 25.0 + c[3] * 25.0 * 25.0 * 25.0 + c[4] * 25.0 * 25.0 * 25.0 * 25.0;
            double Rt = (cond / 1000) / (42.914 * rt);
            double S = a[0] + a[1] * Math.Pow(Rt, 0.5) + a[2] * Math.Pow(Rt, 1) + a[3] * Math.Pow(Rt, 1.5) + a[4] * Math.Pow(Rt, 2) + a[5] * Math.Pow(Rt, 2.5) + ((25.0 - 15) / (1 + k * (25.0 - 15)) * (b[0] + b[1] * Math.Pow(Rt, 0.5) + b[2] * Math.Pow(Rt, 1) + b[3] * Math.Pow(Rt, 1.5) + b[4] * Math.Pow(Rt, 2) + b[5] * Math.Pow(Rt, 2.5)));
            return S;
        }

        private void saveData()
        {
            

                DateTime dt = DateTime.Now.ToUniversalTime();
                string filePath = Properties.Settings.Default["dataFileBasePath"].ToString() + "_" + dt.ToString("yyyy-MM-dd") + ".csv";
                filePath = filePath.Replace('\\', '/');




                try
                {
                    if (MW.lastDataReceived != lastFileWrite)
                    {
                        saveToFile(filePath, dt);
                    }
                }
                catch (Exception e)
                {
                    //MessageBox.Show("Error writing data: " + e.Message, "Error saving data");
                    MW.statusLabel3.Text = dt.ToString() + ": Error writing data file: " + e.Message;
                }

                if (MW.lastDataReceived.Hour != lastFileWrite.Hour)
                {
                    ftpTransfer(filePath);
                    lastFileWrite = MW.lastDataReceived;
                }


        }

        private async Task writeDataPointAsync(int conditionId, int MesoID, string field, double value, DateTime dt)
        {
            string tag;
            if (MesoID == -1) tag = "AmbientData";
            else tag = MesoID.ToString();
            var point = PointData
              .Measurement("CRCM")
              .Tag("Condition", conditionId.ToString())
              .Tag("Mesocosm", tag)
              .Field(field, value)
              .Timestamp(dt.ToUniversalTime(), WritePrecision.S);

            try
            {
                var writeApi = client.GetWriteApiAsync();
                await writeApi.WritePointAsync(bucket, org, point);

            }
            catch (Exception e)
            {

            }

        }


        private void saveToFile(string filePath, DateTime dt)
        {
            if (!System.IO.File.Exists(filePath))
            {
                //Write headers
                String header = "Time;Sun;Tide;Ambient_O2;Ambient_Conductivity;Ambient_Salinity;Ambient_Turbidity;Ambient_Fluo;Ambient_Temperature;Ambient_pH;Cold_Water_Pressure;Hot_Water_Pressure;Hot_Water_Temperature;Cleanup_Mode;";

                // Salinity Data headers
                header += "Salinity_ConductiviteC0;Salinity_ConductiviteControl;Salinity_ConductiviteC1;Salinity_ConductiviteC2;Salinity_ConductiviteC3;";
                header += "Salinity_SaliniteC0;Salinity_SaliniteControl;Salinity_SaliniteC1;Salinity_SaliniteC2;Salinity_SaliniteC3;";
                header += "Salinity_TemperatureC0;Salinity_TemperatureControl;Salinity_TemperatureC1;Salinity_TemperatureC2;Salinity_TemperatureC3;";
                header += "Salinity_DebitC0;Salinity_DebitC1;Salinity_DebitC2;Salinity_DebitC3;";
                header += "Salinity_VanneC0;Salinity_VanneC1;Salinity_VanneC2;Salinity_VanneC3;Salinity_VanneC2_filtre;";
                header += "CTD_Temperature;CTD_Conductivity;CTD_Oxygen;CTD_PSU;CTD_CalculatedPSU;";

                // Desalinator Data headers
                header += "Desalinator_PressionEntree;Desalinator_PressionFresh;Desalinator_PressionSaumure;";
                header += "Desalinator_DebitEntree;Desalinator_DebitFresh;Desalinator_DebitSaumure;Desalinator_DebitMOI;";
                header += "Desalinator_Conductivity;Desalinator_Salinity;Desalinator_Temperature;";
                header += "Desalinator_V3VMOI;Desalinator_VanneEntree;Desalinator_VanneFresh;Desalinator_VanneSaumure;";

                for (int i = 0; i < 4; i++)
                {
                    header += "Condition["; header += i; header += "]_Temperature;";
                    header += "Condition["; header += i; header += "]_pH;";
                    header += "Condition["; header += i; header += "]_consigne_pH;";
                    header += "Condition["; header += i; header += "]_sortiePID_pH;";
                    if (i > 0)
                    {
                        header += "Condition["; header += i; header += "]_consigne_Temperature;";
                        header += "Condition["; header += i; header += "]_sortiePID_Temperature;";
                    }
                    for (int j = 0; j < 3; j++)
                    {
                        header += "Condition["; header += i; header += "]_Meso["; header += j; header += "]_Temperature;";
                        header += "Condition["; header += i; header += "]_Meso["; header += j; header += "]_pH;";
                        header += "Condition["; header += i; header += "]_Meso["; header += j; header += "]_FlowRate;";
                        header += "Condition["; header += i; header += "]_Meso["; header += j; header += "]_LevelH;";
                        header += "Condition["; header += i; header += "]_Meso["; header += j; header += "]_LevelL;";
                        header += "Condition["; header += i; header += "]_Meso["; header += j; header += "]_LevelLL;";
                    }
                }
                header += "\n";
                System.IO.File.WriteAllText(filePath, header);
            }

            string data = dt.ToString(); ; data += ";";

            double sun = MW.ambiantConditions.sun ? 1 : 0;
            double tide = MW.ambiantConditions.tide ? 1 : 0;

            data += sun; data += ";";
            data += MW.ambiantConditions.tide ? 1 : 0; data += ";";
            data += MW.ambiantConditions.oxy; data += ";";
            data += MW.ambiantConditions.cond; data += ";";
            data += MW.ambiantConditions.salinite; data += ";";
            data += MW.ambiantConditions.turb; data += ";";
            data += MW.ambiantConditions.fluo; data += ";";
            data += MW.ambiantConditions.temperature; data += ";";
            data += MW.ambiantConditions.pH; data += ";";
            data += MW.ambiantConditions.pressionEA; data += ";";
            data += MW.ambiantConditions.pressionEC; data += ";";
            data += MW.ambiantConditions.tempPAC; data += ";";
            data += MW.cleanupMode; data += ";";

            // Salinity Data
            data += MW.salinityData?.conductiviteC0 ?? 0; data += ";";
            data += MW.salinityData?.conductiviteControl ?? 0; data += ";";
            data += MW.salinityData?.conductiviteC1 ?? 0; data += ";";
            data += MW.salinityData?.conductiviteC2 ?? 0; data += ";";
            data += MW.salinityData?.conductiviteC3 ?? 0; data += ";";
            data += MW.salinityData?.saliniteC0 ?? 0; data += ";";
            data += MW.salinityData?.saliniteControl ?? 0; data += ";";
            data += MW.salinityData?.saliniteC1 ?? 0; data += ";";
            data += MW.salinityData?.saliniteC2 ?? 0; data += ";";
            data += MW.salinityData?.saliniteC3 ?? 0; data += ";";
            data += MW.salinityData?.temperatureC0 ?? 0; data += ";";
            data += MW.salinityData?.temperatureControl ?? 0; data += ";";
            data += MW.salinityData?.temperatureC1 ?? 0; data += ";";
            data += MW.salinityData?.temperatureC2 ?? 0; data += ";";
            data += MW.salinityData?.temperatureC3 ?? 0; data += ";";
            data += MW.salinityData?.debitC0 ?? 0; data += ";";
            data += MW.salinityData?.debitC1 ?? 0; data += ";";
            data += MW.salinityData?.debitC2 ?? 0; data += ";";
            data += MW.salinityData?.debitC3 ?? 0; data += ";";
            data += MW.salinityData?.vanneC0 ?? 0; data += ";";
            data += MW.salinityData?.vanneC1 ?? 0; data += ";";
            data += MW.salinityData?.vanneC2 ?? 0; data += ";";
            data += MW.salinityData?.vanneC3 ?? 0; data += ";";
            data += MW.salinityData?.vanneC2_filtre ?? 0; data += ";";
            data += MW.salinityData?.CTD_Temperature ?? 0; data += ";";
            data += MW.salinityData?.CTD_Conductivity ?? 0; data += ";";
            data += MW.salinityData?.CTD_Oxygen ?? 0; data += ";";
            data += MW.salinityData?.CTD_PSU ?? 0; data += ";";
            data += MW.salinityData?.CTD_CalculatedPSU ?? 0; data += ";";

            // Desalinator Data
            data += MW.desalinatorData?.pressionEntree ?? 0; data += ";";
            data += MW.desalinatorData?.pressionFresh ?? 0; data += ";";
            data += MW.desalinatorData?.pressionSaumure ?? 0; data += ";";
            data += MW.desalinatorData?.debitEntree ?? 0; data += ";";
            data += MW.desalinatorData?.debitFresh ?? 0; data += ";";
            data += MW.desalinatorData?.debitSaumure ?? 0; data += ";";
            data += MW.desalinatorData?.debitMOI ?? 0; data += ";";
            data += MW.desalinatorData?.conductivity ?? 0; data += ";";
            data += MW.desalinatorData?.salinity ?? 0; data += ";";
            data += MW.desalinatorData?.temperature ?? 0; data += ";";
            data += MW.desalinatorData?.v3VMOI ?? 0; data += ";";
            data += MW.desalinatorData?.vanneEntree ?? 0; data += ";";
            data += MW.desalinatorData?.vanneFresh ?? 0; data += ";";
            data += MW.desalinatorData?.vanneSaumure ?? 0; data += ";";

            writeDataPointAsync(0, -1, "amb_sun", sun, dt);
            writeDataPointAsync(0, -1, "amb_tide", tide, dt);
            writeDataPointAsync(0, -1, "amb_oxy", MW.ambiantConditions.oxy, dt);
            writeDataPointAsync(0, -1, "amb_conductivity", MW.ambiantConditions.cond, dt);
            writeDataPointAsync(0, -1, "amb_salinity", MW.ambiantConditions.salinite, dt);
            writeDataPointAsync(0, -1, "amb_turb", MW.ambiantConditions.turb, dt);
            writeDataPointAsync(0, -1, "amb_fluo", MW.ambiantConditions.fluo, dt);
            writeDataPointAsync(0, -1, "amb_temperature", MW.ambiantConditions.temperature, dt);
            writeDataPointAsync(0, -1, "amb_pH", MW.ambiantConditions.pH, dt);

            writeDataPointAsync(0, -1, "amb_Cold_Water_Pressure", MW.ambiantConditions.pressionEA, dt);
            writeDataPointAsync(0, -1, "amb_Hot_Water_Pressure", MW.ambiantConditions.pressionEC, dt);
            writeDataPointAsync(0, -1, "amb_Hot_Water_Temperature", MW.ambiantConditions.tempPAC, dt);


            writeDataPointAsync(0, -1, "amb_Hot_Water_Pressure.sortiePID", MW.ambiantConditions.sortiePID_EC, dt);
            writeDataPointAsync(0, -1, "amb_Cold_Water_Pressure.sortiePID", MW.ambiantConditions.sortiePID_EA, dt);

            // Salinity Data writeDataPointAsync
            if (MW.salinityData != null)
            {
                writeDataPointAsync(0, -1, "Salinity.conductiviteC0", MW.salinityData.conductiviteC0, dt);
                writeDataPointAsync(0, -1, "Salinity.conductiviteControl", MW.salinityData.conductiviteControl, dt);
                writeDataPointAsync(0, -1, "Salinity.conductiviteC1", MW.salinityData.conductiviteC1, dt);
                writeDataPointAsync(0, -1, "Salinity.conductiviteC2", MW.salinityData.conductiviteC2, dt);
                writeDataPointAsync(0, -1, "Salinity.conductiviteC3", MW.salinityData.conductiviteC3, dt);
                writeDataPointAsync(0, -1, "Salinity.saliniteC0", MW.salinityData.saliniteC0, dt);
                writeDataPointAsync(0, -1, "Salinity.saliniteControl", MW.salinityData.saliniteControl, dt);
                writeDataPointAsync(0, -1, "Salinity.saliniteC1", MW.salinityData.saliniteC1, dt);
                writeDataPointAsync(0, -1, "Salinity.saliniteC2", MW.salinityData.saliniteC2, dt);
                writeDataPointAsync(0, -1, "Salinity.saliniteC3", MW.salinityData.saliniteC3, dt);
                writeDataPointAsync(0, -1, "Salinity.temperatureC0", MW.salinityData.temperatureC0, dt);
                writeDataPointAsync(0, -1, "Salinity.temperatureControl", MW.salinityData.temperatureControl, dt);
                writeDataPointAsync(0, -1, "Salinity.temperatureC1", MW.salinityData.temperatureC1, dt);
                writeDataPointAsync(0, -1, "Salinity.temperatureC2", MW.salinityData.temperatureC2, dt);
                writeDataPointAsync(0, -1, "Salinity.temperatureC3", MW.salinityData.temperatureC3, dt);
                writeDataPointAsync(0, -1, "Salinity.debitC0", MW.salinityData.debitC0, dt);
                writeDataPointAsync(0, -1, "Salinity.debitC1", MW.salinityData.debitC1, dt);
                writeDataPointAsync(0, -1, "Salinity.debitC2", MW.salinityData.debitC2, dt);
                writeDataPointAsync(0, -1, "Salinity.debitC3", MW.salinityData.debitC3, dt);
                writeDataPointAsync(0, -1, "Salinity.vanneC0", MW.salinityData.vanneC0, dt);
                writeDataPointAsync(0, -1, "Salinity.vanneC1", MW.salinityData.vanneC1, dt);
                writeDataPointAsync(0, -1, "Salinity.vanneC2", MW.salinityData.vanneC2, dt);
                writeDataPointAsync(0, -1, "Salinity.vanneC3", MW.salinityData.vanneC3, dt);
                writeDataPointAsync(0, -1, "Salinity.vanneC2_filtre", MW.salinityData.vanneC2_filtre, dt);
                writeDataPointAsync(0, -1, "CTD.Temperature", MW.salinityData.CTD_Temperature, dt);
                writeDataPointAsync(0, -1, "CTD.Conductivity", MW.salinityData.CTD_Conductivity, dt);
                writeDataPointAsync(0, -1, "CTD.Oxygen", MW.salinityData.CTD_Oxygen, dt);
                writeDataPointAsync(0, -1, "CTD.PSU", MW.salinityData.CTD_PSU, dt);
                writeDataPointAsync(0, -1, "CTD.CalculatedPSU", MW.salinityData.CTD_CalculatedPSU, dt);
            }

            // Desalinator Data writeDataPointAsync
            if (MW.desalinatorData != null)
            {
                writeDataPointAsync(0, -1, "Desalinator.pressionEntree", MW.desalinatorData.pressionEntree, dt);
                writeDataPointAsync(0, -1, "Desalinator.pressionFresh", MW.desalinatorData.pressionFresh, dt);
                writeDataPointAsync(0, -1, "Desalinator.pressionSaumure", MW.desalinatorData.pressionSaumure, dt);
                writeDataPointAsync(0, -1, "Desalinator.debitEntree", MW.desalinatorData.debitEntree, dt);
                writeDataPointAsync(0, -1, "Desalinator.debitFresh", MW.desalinatorData.debitFresh, dt);
                writeDataPointAsync(0, -1, "Desalinator.debitSaumure", MW.desalinatorData.debitSaumure, dt);
                writeDataPointAsync(0, -1, "Desalinator.debitMOI", MW.desalinatorData.debitMOI, dt);
                writeDataPointAsync(0, -1, "Desalinator.conductivity", MW.desalinatorData.conductivity, dt);
                writeDataPointAsync(0, -1, "Desalinator.salinity", MW.desalinatorData.salinity, dt);
                writeDataPointAsync(0, -1, "Desalinator.temperature", MW.desalinatorData.temperature, dt);
                writeDataPointAsync(0, -1, "Desalinator.v3VMOI", MW.desalinatorData.v3VMOI, dt);
                writeDataPointAsync(0, -1, "Desalinator.vannePressionEntree", MW.desalinatorData.vanneEntree, dt);
                writeDataPointAsync(0, -1, "Desalinator.vanneFresh", MW.desalinatorData.vanneFresh, dt);
                writeDataPointAsync(0, -1, "Desalinator.vanneSaumure", MW.desalinatorData.vanneSaumure, dt);


            }

            for (int i = 0; i < 4; i++)
            {
                if (i == 0)
                {
                    data += MW.ambiantConditions.C0_temp; data += ";";
                    data += MW.ambiantConditions.C0_pH; data += ";";
                }
                else
                {
                    data += MW.conditions[i].temperature; data += ";";
                    data += MW.conditions[i].pH; data += ";";
                }
                data += MW.conditions[i].rpH.consigne; data += ";";
                data += MW.conditions[i].rpH.sortiePID_pc; data += ";";


                writeDataPointAsync(i, -1, "pH", MW.conditions[i].pH, dt);
                writeDataPointAsync(i, -1, "rpH.consigne", MW.conditions[i].rpH.consigne, dt);
                writeDataPointAsync(i, -1, "rpH.sortiePID", MW.conditions[i].rpH.sortiePID_pc, dt);

                if (i > 0)
                {
                    data += MW.conditions[i].rTemp.consigne; data += ";";
                    data += MW.conditions[i].rTemp.sortiePID_pc; data += ";";
                    writeDataPointAsync(i, -1, "temperature", MW.conditions[i].temperature, dt);
                    writeDataPointAsync(i, -1, "rTemp.consigne", MW.conditions[i].rTemp.consigne, dt);
                    writeDataPointAsync(i, -1, "rTemp.sortiePID", MW.conditions[i].rTemp.sortiePID_pc, dt);
                }
                else
                {
                    writeDataPointAsync(i, -1, "temperature", MW.ambiantConditions.C0_temp, dt);
                    writeDataPointAsync(i, -1, "pH", MW.ambiantConditions.C0_pH, dt);
                }

                for (int j = 0; j < 3; j++)
                {
                    bool LH = MW.conditions[i].Meso[j].alarmeNiveauHaut;
                    bool LL = MW.conditions[i].Meso[j].alarmeNiveauBas;
                    bool LLL = MW.conditions[i].Meso[j].alarmeNiveauTresBas;

                    data += MW.conditions[i].Meso[j].temperature; data += ";";
                    data += MW.conditions[i].Meso[j].pH; data += ";";
                    data += MW.conditions[i].Meso[j].debit; data += ";";
                    data += LH; data += ";";
                    data += LL; data += ";";
                    data += LLL; data += ";";

                    writeDataPointAsync(i, j, "temperature", MW.conditions[i].Meso[j].temperature, dt);
                    writeDataPointAsync(i, j, "pH", MW.conditions[i].Meso[j].pH, dt);
                    writeDataPointAsync(i, j, "debit", MW.conditions[i].Meso[j].debit, dt);
                    /*writeDataPointAsync(i, j, "LH", LH, dt);
                    writeDataPointAsync(i, j, "LL", LL, dt);
                    writeDataPointAsync(i, j, "LLL", LLL, dt);*/

                }
            }
            data += "\n";
            System.IO.File.AppendAllText(filePath, data);
        }

        private void ftpTransfer(string fileName)
        {
            try
            {
                string ftpUsername = Properties.Settings.Default["ftpUsername"].ToString();
                string ftpPassword = Properties.Settings.Default["ftpPassword"].ToString();
                string ftpDir = "ftp://" + Properties.Settings.Default["ftpDir"].ToString();

                string fn = fileName.Substring(fileName.LastIndexOf('/') + 1);
                ftpDir += fn;
                using (var client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(ftpUsername, ftpPassword);
                    client.UploadFile(ftpDir, WebRequestMethods.Ftp.UploadFile, fileName);
                }
                MW.statusLabel3.Text = "";
            }
            catch (Exception e)
            {
                DateTime dt = DateTime.Now;
                MW.statusLabel3.Text = dt.ToString() + ": Error sending data file on FTP server: " + e.Message;
                //MessageBox.Show("Error sending data file on FTP server: " + e.Message, "Error sending FTP data");
            }

        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
    }

}
