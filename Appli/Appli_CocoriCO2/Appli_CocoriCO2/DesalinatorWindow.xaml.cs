using System;
using System.Globalization;
using System.Windows;
using System.Threading;
using System.Windows.Media;
using Newtonsoft.Json;

namespace Appli_CocoriCO2
{
    public partial class DesalinatorWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        public CultureInfo ci;

        public DesalinatorWindow()
        {
            InitializeComponent();
            ci = new CultureInfo("en-US");
            ci.NumberFormat.NumberDecimalDigits = 2;
            ci.NumberFormat.NumberDecimalSeparator = ".";
            ci.NumberFormat.NumberGroupSeparator = " ";
            Thread.CurrentThread.CurrentCulture = ci;
            Thread.CurrentThread.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;

            RefreshData();
            UpdatePompeHPButton();
            UpdatePressureThreshold();
            UpdatePressionEntreeThreshold();
            UpdateDebitMOIThreshold();
        }

        public void RefreshData()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (MW.desalinatorData != null)
                    {
                        // Update Desalinator Data
                        lbl_pressionHP.Content = MW.desalinatorData.pressionHP.ToString("F2", ci);
                        lbl_pressionEntree.Content = MW.desalinatorData.pressionEntree.ToString("F2", ci);
                        lbl_pressionSaumure.Content = MW.desalinatorData.pressionSaumure.ToString("F2", ci);
                        lbl_pressionFresh.Content = MW.desalinatorData.pressionFresh.ToString("F2", ci);

                        lbl_debitEntree.Content = MW.desalinatorData.debitEntree.ToString("F2", ci);
                        lbl_debitMOI.Content = MW.desalinatorData.debitMOI.ToString("F2", ci);
                        lbl_debitRecirculation.Content = MW.desalinatorData.debitSaumure.ToString("F2", ci);
                        lbl_debitFresh.Content = MW.desalinatorData.debitFresh.ToString("F2", ci);

                        lbl_vanneEntree.Content = MW.desalinatorData.vanneEntree.ToString("F1", ci);
                        lbl_vanneSaumure.Content = MW.desalinatorData.vanneSaumure.ToString("F1", ci);
                        lbl_vanneFresh.Content = MW.desalinatorData.vanneFresh.ToString("F1", ci);
                        lbl_v3VMOI.Content = MW.desalinatorData.v3VMOI.ToString("F1", ci);

                        lbl_pompeHP.Content = MW.desalinatorData.pompeHP ? "ON" : "OFF";
                        lbl_conductivity.Content = MW.desalinatorData.conductivity.ToString("F2", ci);
                        lbl_salinity.Content = MW.desalinatorData.salinity.ToString("F2", ci);
                        lbl_temperature.Content = MW.desalinatorData.temperature.ToString("F2", ci);

                        lbl_lastUpdated.Content = MW.desalinatorData.lastUpdated.ToString("yyyy-MM-dd HH:mm:ss");

                        // Update Desalinator Parameters
                        // Regulation Entree
                        lbl_param_entree_cons.Content = MW.desalinatorParams.regulPressionEntree.consigne.ToString("F2", ci);
                        lbl_param_entree_kp.Content = MW.desalinatorParams.regulPressionEntree.Kp.ToString("F2", ci);
                        lbl_param_entree_ki.Content = MW.desalinatorParams.regulPressionEntree.Ki.ToString("F2", ci);
                        lbl_param_entree_kd.Content = MW.desalinatorParams.regulPressionEntree.Kd.ToString("F2", ci);
                        lbl_param_entree_pid.Content = MW.desalinatorParams.regulPressionEntree.sortiePID_pc.ToString("F1", ci) + "%";
                        lbl_param_entree_override.Content = MW.desalinatorParams.regulPressionEntree.autorisationForcage ? "YES" : "NO";
                        lbl_param_entree_force.Content = MW.desalinatorParams.regulPressionEntree.consigneForcage.ToString();

                        // Regulation Saumure
                        lbl_param_saumure_cons.Content = MW.desalinatorParams.regulPressionSaumure.consigne.ToString("F2", ci);
                        lbl_param_saumure_kp.Content = MW.desalinatorParams.regulPressionSaumure.Kp.ToString("F2", ci);
                        lbl_param_saumure_ki.Content = MW.desalinatorParams.regulPressionSaumure.Ki.ToString("F2", ci);
                        lbl_param_saumure_kd.Content = MW.desalinatorParams.regulPressionSaumure.Kd.ToString("F2", ci);
                        lbl_param_saumure_pid.Content = MW.desalinatorParams.regulPressionSaumure.sortiePID_pc.ToString("F1", ci) + "%";
                        lbl_param_saumure_override.Content = MW.desalinatorParams.regulPressionSaumure.autorisationForcage ? "YES" : "NO";
                        lbl_param_saumure_force.Content = MW.desalinatorParams.regulPressionSaumure.consigneForcage.ToString();

                        // Regulation Fresh
                        lbl_param_fresh_cons.Content = MW.desalinatorParams.regulPressionFresh.consigne.ToString("F2", ci);
                        lbl_param_fresh_kp.Content = MW.desalinatorParams.regulPressionFresh.Kp.ToString("F2", ci);
                        lbl_param_fresh_ki.Content = MW.desalinatorParams.regulPressionFresh.Ki.ToString("F2", ci);
                        lbl_param_fresh_kd.Content = MW.desalinatorParams.regulPressionFresh.Kd.ToString("F2", ci);
                        lbl_param_fresh_pid.Content = MW.desalinatorParams.regulPressionFresh.sortiePID_pc.ToString("F1", ci) + "%";
                        lbl_param_fresh_override.Content = MW.desalinatorParams.regulPressionFresh.autorisationForcage ? "YES" : "NO";
                        lbl_param_fresh_force.Content = MW.desalinatorParams.regulPressionFresh.consigneForcage.ToString();
                    }
                });

                // Update control states
                UpdatePompeHPButton();
                UpdatePressureThreshold();
                UpdatePressionEntreeThreshold();
                UpdateDebitMOIThreshold();
            }
            catch (Exception ex)
            {
                // Handle exceptions silently
            }
        }

        private void UpdatePompeHPButton()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (MW.desalinatorData != null)
                    {
                        bool pompeStatus = MW.desalinatorData.pompeHP;
                        btn_pompeHP.Content = pompeStatus ? "ON" : "OFF";
                        btn_pompeHP.Background = pompeStatus ? Brushes.LightGreen : Brushes.LightCoral;
                        btn_pompeHP.Foreground = pompeStatus ? Brushes.DarkGreen : Brushes.DarkRed;
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle exceptions silently
            }
        }

        private void UpdatePressureThreshold()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    // Display the HP_threshold from desalinatorParams
                    if (MW.desalinatorParams != null && MW.desalinatorParams.HP_threshold >= 0)
                    {
                        txt_pressureHPThreshold.Text = MW.desalinatorParams.HP_threshold.ToString("F2", ci);
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle exceptions silently
            }
        }

        private void btn_pompeHP_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Toggle pompe HP state
                bool newState = !MW.desalinatorData.pompeHP;
                if (newState)
                {
                        if (MessageBox.Show("Avez vous bien ouvert la vanne HP à fond avant de démarrer la pompe HP? C'est très grave si vous ne l'avez pas fait et que vous répondez oui quand même", "Vanne HP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        {
                        newState = !newState;
                        }
                    
                }

                // Update desalinatorParams
                MW.desalinatorParams.pompeHP = newState;

                // Send the entire desalinatorParams using command 14
                string json = JsonConvert.SerializeObject(MW.desalinatorParams);
                string command = $"{{\"cmd\": 14, \"cID\": 7, " + json.Substring(1);

                MW.SendWebSocketCommand(command);

                // Update local data state
                MW.desalinatorData.pompeHP = newState;
                UpdatePompeHPButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error toggling pompe HP: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btn_setPressureThreshold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(txt_pressureHPThreshold.Text, NumberStyles.Float, ci, out double threshold))
                {
                    // Update desalinatorParams
                    MW.desalinatorParams.HP_threshold = threshold;

                    // Send the entire desalinatorParams using command 14
                    string json = JsonConvert.SerializeObject(MW.desalinatorParams);
                    string command = $"{{\"cmd\": 14, \"cID\": 7, " + json.Substring(1);

                    MW.SendWebSocketCommand(command);

                    MessageBox.Show($"Pressure threshold set to {threshold:F2} bars", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please enter a valid pressure threshold value.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting pressure threshold: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txt_pressureHPThreshold_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Optional: Real-time validation or formatting
        }

        private void UpdatePressionEntreeThreshold()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (MW.desalinatorParams != null && MW.desalinatorParams.pressionEntreeThreshold >= 0)
                    {
                        txt_pressionEntreeThreshold.Text = MW.desalinatorParams.pressionEntreeThreshold.ToString("F2", ci);
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle exceptions silently
            }
        }

        private void UpdateDebitMOIThreshold()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (MW.desalinatorParams != null && MW.desalinatorParams.debitMOIThreshold >= 0)
                    {
                        txt_debitMOIThreshold.Text = MW.desalinatorParams.debitMOIThreshold.ToString("F2", ci);
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle exceptions silently
            }
        }

        private void btn_setPressionEntreeThreshold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(txt_pressionEntreeThreshold.Text, NumberStyles.Float, ci, out double threshold))
                {
                    // Update desalinatorParams
                    MW.desalinatorParams.pressionEntreeThreshold = threshold;

                    // Send the entire desalinatorParams using command 14
                    string json = JsonConvert.SerializeObject(MW.desalinatorParams);
                    string command = $"{{\"cmd\": 14, \"cID\": 7, " + json.Substring(1);

                    MW.SendWebSocketCommand(command);

                    MessageBox.Show($"Pression Entree threshold set to {threshold:F2} bars", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please enter a valid pression entree threshold value.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting pression entree threshold: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btn_setDebitMOIThreshold_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(txt_debitMOIThreshold.Text, NumberStyles.Float, ci, out double threshold))
                {
                    // Update desalinatorParams
                    MW.desalinatorParams.debitMOIThreshold = threshold;

                    // Send the entire desalinatorParams using command 14
                    string json = JsonConvert.SerializeObject(MW.desalinatorParams);
                    string command = $"{{\"cmd\": 14, \"cID\": 7, " + json.Substring(1);

                    MW.SendWebSocketCommand(command);

                    MessageBox.Show($"Debit MOI threshold set to {threshold:F2} L/min", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please enter a valid debit MOI threshold value.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting debit MOI threshold: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshControls()
        {
            UpdatePompeHPButton();
            UpdatePressureThreshold();
            UpdatePressionEntreeThreshold();
            UpdateDebitMOIThreshold();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }

    }
}