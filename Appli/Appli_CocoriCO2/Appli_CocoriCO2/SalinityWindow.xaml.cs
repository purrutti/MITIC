using System;
using System.Globalization;
using System.Windows;
using System.Threading;
using Newtonsoft.Json;

namespace Appli_CocoriCO2
{
    public partial class SalinityWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        public CultureInfo ci;

        public SalinityWindow()
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
        }

        public void RefreshData()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (MW.salinityData != null)
                    {
                        // Update Salinity Data
                        lbl_saliniteControl.Content = MW.salinityData.saliniteControl.ToString("F2", ci);
                        lbl_saliniteC0.Content = MW.salinityData.saliniteC0.ToString("F2", ci);
                        lbl_saliniteC1.Content = MW.salinityData.saliniteC1.ToString("F2", ci);
                        lbl_saliniteC2.Content = MW.salinityData.saliniteC2.ToString("F2", ci);
                        lbl_saliniteC3.Content = MW.salinityData.saliniteC3.ToString("F2", ci);

                        lbl_temperatureControl.Content = MW.salinityData.temperatureControl.ToString("F2", ci);
                        lbl_temperatureC0.Content = MW.salinityData.temperatureC0.ToString("F2", ci);
                        lbl_temperatureC1.Content = MW.salinityData.temperatureC1.ToString("F2", ci);
                        lbl_temperatureC2.Content = MW.salinityData.temperatureC2.ToString("F2", ci);
                        lbl_temperatureC3.Content = MW.salinityData.temperatureC3.ToString("F2", ci);

                        lbl_conductiviteControl.Content = MW.salinityData.conductiviteControl.ToString("F2", ci);
                        lbl_conductiviteC0.Content = MW.salinityData.conductiviteC0.ToString("F2", ci);
                        lbl_conductiviteC1.Content = MW.salinityData.conductiviteC1.ToString("F2", ci);
                        lbl_conductiviteC2.Content = MW.salinityData.conductiviteC2.ToString("F2", ci);
                        lbl_conductiviteC3.Content = MW.salinityData.conductiviteC3.ToString("F2", ci);

                        lbl_debitC0.Content = MW.salinityData.debitC0.ToString("F2", ci);
                        lbl_debitC1.Content = MW.salinityData.debitC1.ToString("F2", ci);
                        lbl_debitC2.Content = MW.salinityData.debitC2.ToString("F2", ci);
                        lbl_debitC3.Content = MW.salinityData.debitC3.ToString("F2", ci);

                        lbl_vanneC0.Content = MW.salinityData.vanneC0.ToString("F1", ci);
                        lbl_vanneC1.Content = MW.salinityData.vanneC1.ToString("F1", ci);
                        lbl_vanneC2.Content = MW.salinityData.vanneC2.ToString("F1", ci);
                        lbl_vanneC3.Content = MW.salinityData.vanneC3.ToString("F1", ci);

                        lbl_lastUpdated.Content = MW.salinityData.lastUpdated.ToString("yyyy-MM-dd HH:mm:ss");
                    }

                    // Update CTD Data (integrated in salinityData)
                    if (MW.salinityData != null)
                    {
                        lbl_ctd_temperature.Content = MW.salinityData.CTD_Temperature.ToString("F4", ci);
                        lbl_ctd_conductivity.Content = MW.salinityData.CTD_Conductivity.ToString("F5", ci);
                        lbl_ctd_oxygen.Content = MW.salinityData.CTD_Oxygen.ToString("F3", ci);
                        lbl_ctd_psu.Content = MW.salinityData.CTD_PSU.ToString("F4", ci);
                        lbl_ctd_calculated_psu.Content = MW.salinityData.CTD_CalculatedPSU.ToString("F4", ci);
                        lbl_ctd_date.Content = MW.salinityData.CTD_Date;
                        lbl_ctd_time.Content = MW.salinityData.CTD_Time;
                    }

                    // Update Regulation Parameters
                    if (MW.salinityRegulParams != null)
                    {
                        // Regulation C0
                        lbl_param_c0_cons.Content = MW.salinityRegulParams.regulSaliniteC0.consigne.ToString("F2", ci);
                        lbl_param_c0_kp.Content = MW.salinityRegulParams.regulSaliniteC0.Kp.ToString("F2", ci);
                        lbl_param_c0_ki.Content = MW.salinityRegulParams.regulSaliniteC0.Ki.ToString("F2", ci);
                        lbl_param_c0_kd.Content = MW.salinityRegulParams.regulSaliniteC0.Kd.ToString("F2", ci);
                        lbl_param_c0_pid.Content = MW.salinityRegulParams.regulSaliniteC0.sortiePID_pc.ToString("F1", ci) + "%";

                        // Regulation C1
                        lbl_param_c1_cons.Content = MW.salinityRegulParams.regulSaliniteC1.consigne.ToString("F2", ci);
                        lbl_param_c1_kp.Content = MW.salinityRegulParams.regulSaliniteC1.Kp.ToString("F2", ci);
                        lbl_param_c1_ki.Content = MW.salinityRegulParams.regulSaliniteC1.Ki.ToString("F2", ci);
                        lbl_param_c1_kd.Content = MW.salinityRegulParams.regulSaliniteC1.Kd.ToString("F2", ci);
                        lbl_param_c1_pid.Content = MW.salinityRegulParams.regulSaliniteC1.sortiePID_pc.ToString("F1", ci) + "%";

                        // Regulation C2
                        lbl_param_c2_cons.Content = MW.salinityRegulParams.regulSaliniteC2.consigne.ToString("F2", ci);
                        lbl_param_c2_kp.Content = MW.salinityRegulParams.regulSaliniteC2.Kp.ToString("F2", ci);
                        lbl_param_c2_ki.Content = MW.salinityRegulParams.regulSaliniteC2.Ki.ToString("F2", ci);
                        lbl_param_c2_kd.Content = MW.salinityRegulParams.regulSaliniteC2.Kd.ToString("F2", ci);
                        lbl_param_c2_pid.Content = MW.salinityRegulParams.regulSaliniteC2.sortiePID_pc.ToString("F1", ci) + "%";

                        // Regulation C3
                        lbl_param_c3_cons.Content = MW.salinityRegulParams.regulSaliniteC3.consigne.ToString("F2", ci);
                        lbl_param_c3_kp.Content = MW.salinityRegulParams.regulSaliniteC3.Kp.ToString("F2", ci);
                        lbl_param_c3_ki.Content = MW.salinityRegulParams.regulSaliniteC3.Ki.ToString("F2", ci);
                        lbl_param_c3_kd.Content = MW.salinityRegulParams.regulSaliniteC3.Kd.ToString("F2", ci);
                        lbl_param_c3_pid.Content = MW.salinityRegulParams.regulSaliniteC3.sortiePID_pc.ToString("F1", ci) + "%";

                        // Regulation C2 Filtre
                        if (MW.salinityRegulParams.regulC2_filtre != null)
                        {
                            lbl_param_c2f_cons.Content = MW.salinityRegulParams.regulC2_filtre.consigne.ToString("F2", ci);
                            lbl_param_c2f_kp.Content = MW.salinityRegulParams.regulC2_filtre.Kp.ToString("F2", ci);
                            lbl_param_c2f_ki.Content = MW.salinityRegulParams.regulC2_filtre.Ki.ToString("F2", ci);
                            lbl_param_c2f_kd.Content = MW.salinityRegulParams.regulC2_filtre.Kd.ToString("F2", ci);
                            lbl_param_c2f_pid.Content = MW.salinityRegulParams.regulC2_filtre.sortiePID_pc.ToString("F1", ci) + "%";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                // Handle exceptions silently
            }
        }
    }
}