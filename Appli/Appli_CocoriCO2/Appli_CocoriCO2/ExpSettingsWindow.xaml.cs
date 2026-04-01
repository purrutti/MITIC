using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
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
    /// Logique d'interaction pour ExpSettingsWindow.xaml
    /// </summary>
    public partial class ExpSettingsWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        public CultureInfo ci;
        public ExpSettingsWindow()
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

            comboBox_Condition.SelectedIndex = 0;
        }

        private void btn_SaveToFile_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btn_LoadFromPLC_Click(object sender, RoutedEventArgs e)
        {
            load(comboBox_Condition.SelectedIndex);
        }

        public async void load(int index)
        {
            try
            {
                string msg;
                foreach (var wsd in ((MainWindow)Application.Current.MainWindow)._sockets)
                {
                    var ws = wsd;
                    if (ws.IsAvailable)
                    {
                        if (index == 4)
                        {

                            msg = "{\"cmd\":7,\"cID\":0, \"sID\":4}";
                        }
                        else if (index == 5)
                        {

                            msg = "{\"cmd\":10,\"cID\":0, \"sID\":4}";
                        }
                        else if (index == 6)
                        {

                            msg = "{\"cmd\":13,\"cID\":7, \"sID\":4}";
                        }
                        else if (index == 7)
                        {

                            msg = "{\"cmd\":13,\"cID\":7, \"sID\":4}";
                        }
                        else if (index == 8) // Salinity Regulation (1)
                        {
                            msg = "{\"cmd\":17,\"cID\":8, \"sID\":4}";
                        }
                        else if (index == 9) // Salinity Regulation (2)
                        {
                            msg = "{\"cmd\":17,\"cID\":8, \"sID\":4}";
                        }
                        else if (index == 10) // Salinity Regulation (3)
                        {
                            msg = "{\"cmd\":17,\"cID\":8, \"sID\":4}";
                        }
                        else
                        {
                            //{command:0,condID:0,senderID:4}
                            msg = "{\"cmd\":0,\"cID\":" + index + ", \"sID\":4}";
                        }
                        try
                        {

                            await ws.Send(msg);
                        }
                        catch (Exception e) { }

                    }
                }
            }catch(Exception e)
            {

            }
            

        }

        private async void btn_SaveToPLC_Click(object sender, RoutedEventArgs e)
        {
            int temp;
            double dTemp;
            string msg;
            foreach (var wsd in ((MainWindow)Application.Current.MainWindow)._sockets)
            {
                var ws = wsd;
                if (ws.IsAvailable)
                {
                    if (comboBox_Condition.SelectedIndex == 5)
                    {

                        MW.pacParams.rTempEC.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.pacParams.rTempEC.consigneForcage = temp;
                        if (Double.TryParse(tb_dpH_setPoint.Text, out dTemp)) MW.pacParams.rTempEC.offset = dTemp;
                        MW.pacParams.rTempEC.consigne = MW.ambiantConditions.temperature + MW.pacParams.rTempEC.offset;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.pacParams.rTempEC.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.pacParams.rTempEC.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.pacParams.rTempEC.Kd = dTemp;
                        var response = new
                        {
                            cmd = 9,
                            cID = 0,
                            sID = 4,//Server
                            MW.pacParams.rTempEC
                        };

                        String s = JsonConvert.SerializeObject(response);


                        await ws.Send(s);





                        /*
                                            msg = "{cmd:9,cID:0,sID:4,";
                                            msg += "\"rTempEC\":{";
                                            msg += "\"offset\":" + MW.pacParams.rTempEC.offset.ToString() + ",";
                                            msg += "\"cons\":" + MW.pacParams.rTempEC.consigne.ToString() + ",";
                                            msg += "\"Kp\":" + MW.pacParams.rTempEC.Kp.ToString() + ",";
                                            msg += "\"Ki\":" + MW.pacParams.rTempEC.Ki.ToString() + ",";
                                            msg += "\"Kd\":" + MW.pacParams.rTempEC.Kd.ToString() + ",";
                                            msg += "\"consForcage\":" + MW.pacParams.rTempEC.consigneForcage + ",";
                                            msg += "\"aForcage\":\"" + MW.pacParams.rTempEC.autorisationForcage + "\"}";
                                            msg += "}";*/
                    }
                    else if (comboBox_Condition.SelectedIndex == 6)
                    {
                        // Desalinator (1): Inlet Pressure + Rebouclage (Saumure)
                        MW.desalinatorParams.regulPressionEntree.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.desalinatorParams.regulPressionEntree.consigneForcage = temp;
                        if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) MW.desalinatorParams.regulPressionEntree.consigne = dTemp;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.desalinatorParams.regulPressionEntree.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.desalinatorParams.regulPressionEntree.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.desalinatorParams.regulPressionEntree.Kd = dTemp;

                        MW.desalinatorParams.regulRebouclage.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                        if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) MW.desalinatorParams.regulRebouclage.consigneForcage = temp;
                        if (Double.TryParse(tb_Temp_setPoint.Text, out dTemp)) MW.desalinatorParams.regulRebouclage.consigne = dTemp;
                        if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) MW.desalinatorParams.regulRebouclage.Kp = dTemp;
                        if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) MW.desalinatorParams.regulRebouclage.Ki = dTemp;
                        if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) MW.desalinatorParams.regulRebouclage.Kd = dTemp;

                        var response7 = new
                        {
                            cmd = 14,
                            cID = 7,
                            sID = 4,//Server
                            regulPressionEntree = MW.desalinatorParams.regulPressionEntree,
                            regulRebouclage = MW.desalinatorParams.regulRebouclage,
                            regulPressionFresh = MW.desalinatorParams.regulPressionFresh,
                            regulPressionSaumure = MW.desalinatorParams.regulPressionSaumure
                        };

                        String s7 = JsonConvert.SerializeObject(response7);
                        await ws.Send(s7);
                    }
                    else if (comboBox_Condition.SelectedIndex == 7)
                    {
                        // Desalinator (2): Fresh Pressure + Brine Pressure
                        MW.desalinatorParams.regulPressionFresh.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.desalinatorParams.regulPressionFresh.consigneForcage = temp;
                        if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) MW.desalinatorParams.regulPressionFresh.consigne = dTemp;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.desalinatorParams.regulPressionFresh.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.desalinatorParams.regulPressionFresh.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.desalinatorParams.regulPressionFresh.Kd = dTemp;

                        MW.desalinatorParams.regulPressionSaumure.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                        if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) MW.desalinatorParams.regulPressionSaumure.consigneForcage = temp;
                        if (Double.TryParse(tb_Temp_setPoint.Text, out dTemp)) MW.desalinatorParams.regulPressionSaumure.consigne = dTemp;
                        if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) MW.desalinatorParams.regulPressionSaumure.Kp = dTemp;
                        if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) MW.desalinatorParams.regulPressionSaumure.Ki = dTemp;
                        if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) MW.desalinatorParams.regulPressionSaumure.Kd = dTemp;

                        var response8 = new
                        {
                            cmd = 14,
                            cID = 7,
                            sID = 4,//Server

                            regulPressionEntree = MW.desalinatorParams.regulPressionEntree,
                            regulRebouclage = MW.desalinatorParams.regulRebouclage,
                            regulPressionFresh = MW.desalinatorParams.regulPressionFresh,
                            regulPressionSaumure = MW.desalinatorParams.regulPressionSaumure
                        };

                        String s8 = JsonConvert.SerializeObject(response8);
                        await ws.Send(s8);
                    }
                    else if (comboBox_Condition.SelectedIndex == 8) // Salinity Regulation (1)
                    {

                        MW.salinityRegulParams.regulSaliniteC0.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.salinityRegulParams.regulSaliniteC0.consigneForcage = temp;

                        MW.salinityRegulParams.regulSaliniteC1.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                        if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) MW.salinityRegulParams.regulSaliniteC1.consigneForcage = temp;


                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC0.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC0.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC0.Kd = dTemp;


                        if (Double.TryParse(tb_dT_setPoint.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC1.offset = dTemp;
                        MW.salinityRegulParams.regulSaliniteC1.consigne = Math.Round(MW.salinityRegulParams.regulSaliniteC1.offset + MW.salinityData.saliniteC0,2);
                        //if (Double.TryParse(tb_Temp_setPoint.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC1.consigne = dTemp;
                        if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC1.Kp = dTemp;
                        if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC1.Ki = dTemp;
                        if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC1.Kd = dTemp;


                        if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC0.consigne = dTemp;

                        var response8 = new
                        {
                            cmd = 18,
                            cID = 8,
                            sID = 4,//Server
                            regulC0 = MW.salinityRegulParams.regulSaliniteC0,
                            regulC1 = MW.salinityRegulParams.regulSaliniteC1
                            /*regulC2 = MW.salinityRegulParams.regulSaliniteC2,
                            regulC3 = MW.salinityRegulParams.regulSaliniteC3,
                            regulC2_filtre = MW.salinityRegulParams.regulC2_filtre*/
                        };

                        String s8 = JsonConvert.SerializeObject(response8);
                        await ws.Send(s8);
                    }
                    else if (comboBox_Condition.SelectedIndex == 9) // Salinity Regulation (2)
                    {

                        MW.salinityRegulParams.regulSaliniteC2.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.salinityRegulParams.regulSaliniteC2.consigneForcage = temp;

                        MW.salinityRegulParams.regulC2_filtre.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                        if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) MW.salinityRegulParams.regulC2_filtre.consigneForcage = temp;


                        if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC2.consigne = dTemp;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC2.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC2.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC2.Kd = dTemp;


                        if (Double.TryParse(tb_dT_setPoint.Text, out dTemp)) MW.salinityRegulParams.regulC2_filtre.offset = dTemp;
                        MW.salinityRegulParams.regulSaliniteC1.consigne = Math.Round(MW.salinityRegulParams.regulC2_filtre.offset + MW.salinityData.saliniteC0,2);

                        if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) MW.salinityRegulParams.regulC2_filtre.Kp = dTemp;
                        if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) MW.salinityRegulParams.regulC2_filtre.Ki = dTemp;
                        if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) MW.salinityRegulParams.regulC2_filtre.Kd = dTemp;

                        var response9 = new
                        {
                            cmd = 18,
                            cID = 8,
                            sID = 4,//Server
                            /*regulC0 = MW.salinityRegulParams.regulSaliniteC0,
                            regulC1 = MW.salinityRegulParams.regulSaliniteC1,*/
                            regulC2 = MW.salinityRegulParams.regulSaliniteC2,
                            /*regulC3 = MW.salinityRegulParams.regulSaliniteC3*/
                            regulC2_filtre = MW.salinityRegulParams.regulC2_filtre
                        };

                        String s9 = JsonConvert.SerializeObject(response9);
                        await ws.Send(s9);
                    }
                    else if (comboBox_Condition.SelectedIndex == 10) // Salinity Regulation (3)
                    {

                        MW.salinityRegulParams.regulSaliniteC3.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.salinityRegulParams.regulSaliniteC3.consigneForcage = temp;

                        if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC3.consigne = dTemp;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC3.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC3.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.salinityRegulParams.regulSaliniteC3.Kd = dTemp;

                        var response10 = new
                        {
                            cmd = 18,
                            cID = 8,
                            sID = 4,//Server
                            /*regulC0 = MW.salinityRegulParams.regulSaliniteC0,
                            regulC1 = MW.salinityRegulParams.regulSaliniteC1,
                            regulC2 = MW.salinityRegulParams.regulSaliniteC2,*/
                            regulC3 = MW.salinityRegulParams.regulSaliniteC3
                            /*regulC2_filtre = MW.salinityRegulParams.regulC2_filtre*/
                        };

                        String s10 = JsonConvert.SerializeObject(response10);
                        await ws.Send(s10);
                    }
                    else if (comboBox_Condition.SelectedIndex == 4)
                    {
                        MW.masterParams.regulPressionEA.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) MW.masterParams.regulPressionEA.consigneForcage = temp;
                        if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) MW.masterParams.regulPressionEA.consigne = dTemp;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) MW.masterParams.regulPressionEA.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) MW.masterParams.regulPressionEA.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) MW.masterParams.regulPressionEA.Kd = dTemp;

                        MW.masterParams.regulPressionEC.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                        if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) MW.masterParams.regulPressionEC.consigneForcage = temp;
                        if (Double.TryParse(tb_Temp_setPoint.Text, out dTemp)) MW.masterParams.regulPressionEC.consigne = dTemp;
                        if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) MW.masterParams.regulPressionEC.Kp = dTemp;
                        if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) MW.masterParams.regulPressionEC.Ki = dTemp;
                        if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) MW.masterParams.regulPressionEC.Kd = dTemp;
                        if (Double.TryParse(tb_dT_setPoint.Text, out dTemp)) MW.masterParams.regulPressionEC.offset = dTemp;

                        var response = new
                        {
                            cmd = 8,
                            cID = 0,
                            sID = 4,//Server
                            rPressionEA = MW.masterParams.regulPressionEA,
                            rPressionEC = MW.masterParams.regulPressionEC,
                        };

                        String s = JsonConvert.SerializeObject(response);

                        await ws.Send(s);


                        /*msg = "{cmd:8,cID:0,sID:4,";
                        msg += "\"rPressionEA\":{";
                        msg += "\"cons\":" + MW.masterParams.regulPressionEA.consigne.ToString() + ",";
                        msg += "\"Kp\":" + MW.masterParams.regulPressionEA.Kp.ToString() + ",";
                        msg += "\"Ki\":" + MW.masterParams.regulPressionEA.Ki.ToString() + ",";
                        msg += "\"Kd\":" + MW.masterParams.regulPressionEA.Kd.ToString() + ",";
                        msg += "\"consForcage\":" + MW.masterParams.regulPressionEA.consigneForcage + ",";
                        msg += "\"aForcage\":\"" + MW.masterParams.regulPressionEA.autorisationForcage + "\"},";

                        msg += "\"rPressionEC\":{";
                        msg += "\"cons\":" + MW.masterParams.regulPressionEC.consigne.ToString() + ",";
                        msg += "\"Kp\":" + MW.masterParams.regulPressionEC.Kp.ToString() + ",";
                        msg += "\"Ki\":" + MW.masterParams.regulPressionEC.Ki.ToString() + ",";
                        msg += "\"Kd\":" + MW.masterParams.regulPressionEC.Kd.ToString() + ",";
                        msg += "\"consForcage\":" + MW.masterParams.regulPressionEC.consigneForcage + ",";
                        msg += "\"aForcage\":\"" + MW.masterParams.regulPressionEC.autorisationForcage + "\"}";
                        msg += "}";*/
                    }
                    else
                    {

                        Condition c = MW.conditions[comboBox_Condition.SelectedIndex];
                        c.condID = comboBox_Condition.SelectedIndex;
                        c.command = 2;
                        c.rpH = new Regul();
                        c.rpH.autorisationForcage = (bool)checkBox_pH_Override.IsChecked;
                        if (Int32.TryParse(tb_pH_consigneForcage.Text, out temp)) c.rpH.consigneForcage = temp;
                        if (Double.TryParse(tb_pH_Kp.Text, out dTemp)) c.rpH.Kp = dTemp;
                        if (Double.TryParse(tb_pH_Ki.Text, out dTemp)) c.rpH.Ki = dTemp;
                        if (Double.TryParse(tb_pH_Kd.Text, out dTemp)) c.rpH.Kd = dTemp;
                        if (c.condID > 0)
                        {

                            if (Double.TryParse(tb_dpH_setPoint.Text, out dTemp)) c.rpH.offset = dTemp;
                            //c.rpH.consigne = MW.ambiantConditions.pH + c.rpH.offset;
                            double moyennepHC0 = (MW.conditions[0].Meso[0].pH + MW.conditions[0].Meso[1].pH + MW.conditions[0].Meso[2].pH) / 3;
                            c.rpH.consigne = moyennepHC0 + c.rpH.offset;
                        }
                        else
                        {
                            if (Double.TryParse(tb_pH_setPoint.Text, out dTemp)) c.rpH.consigne = dTemp;
                            c.rpH.offset = 0;
                        }


                        if (c.condID > 0)
                        {
                            c.rTemp = new Regul();
                            c.rTemp.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                            if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) c.rTemp.consigneForcage = temp;
                            if (Double.TryParse(tb_Temp_setPoint.Text, out dTemp)) c.rTemp.consigne = dTemp;
                            if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) c.rTemp.Kp = dTemp;
                            if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) c.rTemp.Ki = dTemp;
                            if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) c.rTemp.Kd = dTemp;
                            if (Double.TryParse(tb_dT_setPoint.Text, out dTemp)) c.rTemp.offset = dTemp;

                            c.rTemp.consigne = MW.ambiantConditions.temperature + c.rTemp.offset;
                        }

                        var response = new
                        {
                            cmd = 2,
                            cID = c.condID,
                            sID = 4,//Server
                            c.rpH,
                            c.rTemp
                        };

                        String s = JsonConvert.SerializeObject(response);

                        await ws.Send(s);




                        /*
                         * {"command":2,"condID":0,"time":"1611595972","rTemp":{"consigne":0,"Kp":0,"Ki":0,"Kd":0},"rpH":{"consigne":0,"Kp":0,"Ki":0,"Kd":0}}
                         * */
                        /* msg = "{cmd:2,cID:" + comboBox_Condition.SelectedIndex + ",sID:4,";

                         if (c.condID > 0)
                         {
                             c.rTemp = new Regul();
                             c.rTemp.autorisationForcage = (bool)checkBox_Temp_Override.IsChecked;
                             if (Int32.TryParse(tb_Temp_consigneForcage.Text, out temp)) c.rTemp.consigneForcage = temp;
                             if (Double.TryParse(tb_Temp_setPoint.Text, out dTemp)) c.rTemp.consigne = dTemp;
                             if (Double.TryParse(tb_Temp_Kp.Text, out dTemp)) c.rTemp.Kp = dTemp;
                             if (Double.TryParse(tb_Temp_Ki.Text, out dTemp)) c.rTemp.Ki = dTemp;
                             if (Double.TryParse(tb_Temp_Kd.Text, out dTemp)) c.rTemp.Kd = dTemp;
                             if (Double.TryParse(tb_dT_setPoint.Text, out dTemp)) c.rTemp.offset = dTemp;
                             msg += "\"rTemp\":{";
                             msg += "\"offset\":" + c.rTemp.offset.ToString() + ",";
                             msg += "\"cons\":" + c.rTemp.consigne.ToString() + ",";
                             msg += "\"Kp\":" + c.rTemp.Kp.ToString() + ",";
                             msg += "\"Ki\":" + c.rTemp.Ki.ToString() + ",";
                             msg += "\"Kd\":" + c.rTemp.Kd.ToString() + ",";
                             msg += "\"consForcage\":" + c.rTemp.consigneForcage + ",";
                             msg += "\"aForcage\":\"" + c.rTemp.autorisationForcage + "\"},";

                         }

                         msg += "\"rpH\":{\"offset\":" + c.rpH.offset.ToString() + ",";
                         msg += "\"cons\":" + c.rpH.consigne.ToString() + ",";
                         msg += "\"Kp\":" + c.rpH.Kp.ToString() + ",";
                         msg += "\"Ki\":" + c.rpH.Ki.ToString() + ",";
                         msg += "\"Kd\":" + c.rpH.Kd.ToString() + ",";
                         msg += "\"consForcage\":" + c.rpH.consigneForcage + ",";
                         msg += "\"aForcage\":\"" + c.rpH.autorisationForcage + "\"}";

                         msg += "}";
                     }
                     Task<string> t2 = Send(((MainWindow)Application.Current.MainWindow).ws, msg, ((MainWindow)Application.Current.MainWindow).comDebugWindow.tb2);
                     t2.Wait(50);*/
                    }
                }
            }
            refreshParams();
        }

        private void btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }



        private void comboBox_Condition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            load(comboBox_Condition.SelectedIndex);
            Visibility v;
            if (comboBox_Condition.SelectedIndex == 5)
            {
                label_pH_title.Content = "Hot water temperature";
                label_pH_setpoint.Content = "Temperature setpoint";
                label_dpH.Content = "delta T°C setpoint";
                label_pH_measure.Content = "Temperature measure";
                tb_pH_setPoint.IsEnabled = false;


                v = Visibility.Hidden;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = Visibility.Visible;
                label_dpH.Visibility = Visibility.Visible;
                tb_dT_setPoint.Visibility = Visibility.Hidden;
                label_dT.Visibility = Visibility.Hidden;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;

            }
            else if (comboBox_Condition.SelectedIndex == 6)
            {
                label_pH_title.Content = "Inlet Pressure regulation";
                label_Temp_title.Content = "Rebouclage regulation";
                label_pH_setpoint.Content = "Pressure setpoint";
                label_Temp_setpoint.Content = "Salinity setpoint";
                label_pH_measure.Content = "Pressure measure";
                label_Temp_measure.Content = "Salinity measure";
                tb_pH_setPoint.IsEnabled = true;
                tb_Temp_setPoint.IsEnabled = true;


                v = Visibility.Visible;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = Visibility.Hidden;
                label_dpH.Visibility = Visibility.Hidden;
                tb_dT_setPoint.Visibility = Visibility.Hidden;
                label_dT.Visibility = Visibility.Hidden;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;

            }
            else if (comboBox_Condition.SelectedIndex == 7)
            {
                label_pH_title.Content = "Fresh Pressure regulation";
                label_Temp_title.Content = "Brine Pressure regulation";
                label_pH_setpoint.Content = "Pressure setpoint";
                label_Temp_setpoint.Content = "Pressure setpoint";
                label_pH_measure.Content = "Pressure measure";
                label_Temp_measure.Content = "Pressure measure";
                tb_pH_setPoint.IsEnabled = true;
                tb_Temp_setPoint.IsEnabled = true;


                v = Visibility.Visible;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = Visibility.Hidden;
                label_dpH.Visibility = Visibility.Hidden;
                tb_dT_setPoint.Visibility = Visibility.Hidden;
                label_dT.Visibility = Visibility.Hidden;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;

            }
            else if (comboBox_Condition.SelectedIndex == 8) // Salinity Regulation (1)
            {
                label_pH_title.Content = "Ratio C0 regulation";
                label_Temp_title.Content = "Salinity C1 regulation";
                label_pH_setpoint.Content = "Ratio consigne (C1)";
                label_Temp_setpoint.Content = "Salinity C1 setpoint";
                label_pH_measure.Content = "Ratio C0 measured";
                label_Temp_measure.Content = "Salinity C1 measure";
                tb_pH_setPoint.IsEnabled = false;
                tb_Temp_setPoint.IsEnabled = false;


                label_dT.Content = "delta Salinity setpoint";


                v = Visibility.Visible;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = Visibility.Hidden;
                label_dpH.Visibility = Visibility.Hidden;
                tb_dT_setPoint.Visibility = v;
                label_dT.Visibility = v;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;
            }
            else if (comboBox_Condition.SelectedIndex == 9) // Salinity Regulation (2)
            {
                label_pH_title.Content = "Ratio C2 regulation";
                label_Temp_title.Content = "Salinity C2 Filtre regulation";
                label_pH_setpoint.Content = "Ratio consigne (C1)";
                label_Temp_setpoint.Content = "Salinity C2 Filtre setpoint";
                label_pH_measure.Content = "Ratio C2 measured";
                label_Temp_measure.Content = "Salinity C2 measure";
                tb_pH_setPoint.IsEnabled = false;
                tb_Temp_setPoint.IsEnabled = false;

                label_dT.Content = "delta Salinity setpoint";


                v = Visibility.Visible;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = Visibility.Hidden;
                label_dpH.Visibility = Visibility.Hidden;
                tb_dT_setPoint.Visibility = v;
                label_dT.Visibility = v;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;
            }
            else if (comboBox_Condition.SelectedIndex == 10) // Salinity Regulation (3)
            {
                label_pH_title.Content = "Ratio C3 regulation";
                label_Temp_title.Visibility = Visibility.Hidden;
                label_pH_setpoint.Content = "Ratio consigne (C1)";
                label_pH_measure.Content = "Ratio C3 measured";
                tb_pH_setPoint.IsEnabled = false;

                v = Visibility.Hidden;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = v;
                label_dpH.Visibility = v;
                tb_dT_setPoint.Visibility = v;
                label_dT.Visibility = v;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = Visibility.Visible;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;
            }
            else if (comboBox_Condition.SelectedIndex == 4)
            {
                label_pH_title.Content = "Ambient water pressure";
                label_Temp_title.Content = "Hot water pressure";
                label_pH_setpoint.Content = "pressure setpoint";
                label_Temp_setpoint.Content = "pressure setpoint";
                label_pH_measure.Content = "pressure measure";
                label_Temp_measure.Content = "pressure measure";
                tb_pH_setPoint.IsEnabled = true;
                tb_Temp_setPoint.IsEnabled = true;


                v = Visibility.Visible;
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = Visibility.Hidden;
                label_dpH.Visibility = Visibility.Hidden;
                tb_dT_setPoint.Visibility = Visibility.Hidden;
                label_dT.Visibility = Visibility.Hidden;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;

            }
            else

            {

                label_dT.Content = "delta T°C setpoint";
                label_dpH.Content = "delta pHsetpoint";
                label_pH_setpoint.Content = "pH setpoint";
                label_Temp_setpoint.Content = "Temperature setpoint";
                label_pH_measure.Content = "pH measure";
                label_Temp_measure.Content = "Temperature measure";
                label_Temp_title.Content = "Temperature regulation";
                if (comboBox_Condition.SelectedIndex == 0)
                {
                    v = Visibility.Hidden;
                    tb_pH_setPoint.IsEnabled = true;
                    label_pH_title.Content = "CO2 valve regulation";
                }
                else
                {
                    v = Visibility.Visible;
                    tb_pH_setPoint.IsEnabled = false;
                    tb_Temp_setPoint.IsEnabled = false;
                    label_pH_title.Content = "pH regulation";
                }
                label_Temp_title.Visibility = v;
                tb_dpH_setPoint.Visibility = v;
                label_dpH.Visibility = v;
                tb_Temp_setPoint.Visibility = v;
                tb_Temp_Kp.Visibility = v;
                tb_Temp_Ki.Visibility = v;
                label_Temp_Kp.Visibility = v;
                label_Temp_setpoint.Visibility = v;
                label_Temp_Ki.Visibility = v;
                tb_Temp_Kd.Visibility = v;
                label_Temp_Kd.Visibility = v;
                tb_Temp_measure.Visibility = v;
                label_Temp_measure.Visibility = v;
                tb_Temp_PIDoutput.Visibility = v;
                label_Temp_sortiePID.Visibility = v;
                label_pc1.Visibility = v;
                label_pc2.Visibility = v;
                tb_Temp_consigneForcage.Visibility = v;
                checkBox_Temp_Override.Visibility = v;
                tb_dT_setPoint.Visibility = v;
                label_dT.Visibility = v;
            }
            refreshParams();
        }

        public void refreshParams()
        {
            try
            {

                Dispatcher.Invoke(() =>
                {
                    if (comboBox_Condition.SelectedIndex < 4)
                    {
                        int condID = comboBox_Condition.SelectedIndex;
                        tb_dpH_setPoint.Text = MW.conditions[condID].rpH.offset.ToString(ci);
                        tb_pH_setPoint.Text = MW.conditions[condID].rpH.consigne.ToString(ci);
                        tb_pH_consigneForcage.Text = MW.conditions[condID].rpH.consigneForcage.ToString(ci);
                        tb_pH_Kp.Text = MW.conditions[condID].rpH.Kp.ToString(ci);
                        tb_pH_Ki.Text = MW.conditions[condID].rpH.Ki.ToString(ci);
                        tb_pH_Kd.Text = MW.conditions[condID].rpH.Kd.ToString(ci);
                        checkBox_pH_Override.IsChecked = MW.conditions[condID].rpH.autorisationForcage;
                        if (condID > 0)
                        {
                            tb_dT_setPoint.Text = MW.conditions[condID].rTemp.offset.ToString(ci);
                            tb_Temp_setPoint.Text = MW.conditions[condID].rTemp.consigne.ToString(ci);
                            tb_Temp_consigneForcage.Text = MW.conditions[condID].rTemp.consigneForcage.ToString(ci);
                            tb_Temp_Kp.Text = MW.conditions[condID].rTemp.Kp.ToString(ci);
                            tb_Temp_Ki.Text = MW.conditions[condID].rTemp.Ki.ToString(ci);
                            tb_Temp_Kd.Text = MW.conditions[condID].rTemp.Kd.ToString(ci);
                            checkBox_Temp_Override.IsChecked = MW.conditions[condID].rTemp.autorisationForcage;
                        }
                    }
                    else
                    {
                        if (comboBox_Condition.SelectedIndex == 4)
                        {
                            tb_dpH_setPoint.Text = MW.masterParams.regulPressionEA.offset.ToString(ci);
                            tb_pH_setPoint.Text = MW.masterParams.regulPressionEA.consigne.ToString(ci);
                            tb_pH_consigneForcage.Text = MW.masterParams.regulPressionEA.consigneForcage.ToString(ci);
                            tb_pH_Kp.Text = MW.masterParams.regulPressionEA.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.masterParams.regulPressionEA.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.masterParams.regulPressionEA.Kd.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.masterParams.regulPressionEA.autorisationForcage;

                            tb_dT_setPoint.Text = MW.masterParams.regulPressionEC.offset.ToString(ci);
                            tb_Temp_setPoint.Text = MW.masterParams.regulPressionEC.consigne.ToString(ci);
                            tb_Temp_consigneForcage.Text = MW.masterParams.regulPressionEC.consigneForcage.ToString(ci);
                            tb_Temp_Kp.Text = MW.masterParams.regulPressionEC.Kp.ToString(ci);
                            tb_Temp_Ki.Text = MW.masterParams.regulPressionEC.Ki.ToString(ci);
                            tb_Temp_Kd.Text = MW.masterParams.regulPressionEC.Kd.ToString(ci);
                            checkBox_Temp_Override.IsChecked = MW.masterParams.regulPressionEC.autorisationForcage;
                        }
                        else if (comboBox_Condition.SelectedIndex == 6)//Desalinator (1)
                        {
                            tb_pH_setPoint.Text = MW.desalinatorParams.regulPressionEntree.consigne.ToString(ci);
                            tb_pH_consigneForcage.Text = MW.desalinatorParams.regulPressionEntree.consigneForcage.ToString(ci);
                            tb_pH_Kp.Text = MW.desalinatorParams.regulPressionEntree.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.desalinatorParams.regulPressionEntree.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.desalinatorParams.regulPressionEntree.Kd.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.desalinatorParams.regulPressionEntree.autorisationForcage;

                            tb_Temp_setPoint.Text = MW.desalinatorParams.regulRebouclage.consigne.ToString(ci);
                            tb_Temp_consigneForcage.Text = MW.desalinatorParams.regulRebouclage.consigneForcage.ToString(ci);
                            tb_Temp_Kp.Text = MW.desalinatorParams.regulRebouclage.Kp.ToString(ci);
                            tb_Temp_Ki.Text = MW.desalinatorParams.regulRebouclage.Ki.ToString(ci);
                            tb_Temp_Kd.Text = MW.desalinatorParams.regulRebouclage.Kd.ToString(ci);
                            checkBox_Temp_Override.IsChecked = MW.desalinatorParams.regulRebouclage.autorisationForcage;
                        }
                        else if (comboBox_Condition.SelectedIndex == 7)//Desalinator (2)
                        {
                            tb_pH_setPoint.Text = MW.desalinatorParams.regulPressionFresh.consigne.ToString(ci);
                            tb_pH_consigneForcage.Text = MW.desalinatorParams.regulPressionFresh.consigneForcage.ToString(ci);
                            tb_pH_Kp.Text = MW.desalinatorParams.regulPressionFresh.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.desalinatorParams.regulPressionFresh.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.desalinatorParams.regulPressionFresh.Kd.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.desalinatorParams.regulPressionFresh.autorisationForcage;

                            tb_Temp_setPoint.Text = MW.desalinatorParams.regulPressionSaumure.consigne.ToString(ci);
                            tb_Temp_consigneForcage.Text = MW.desalinatorParams.regulPressionSaumure.consigneForcage.ToString(ci);
                            tb_Temp_Kp.Text = MW.desalinatorParams.regulPressionSaumure.Kp.ToString(ci);
                            tb_Temp_Ki.Text = MW.desalinatorParams.regulPressionSaumure.Ki.ToString(ci);
                            tb_Temp_Kd.Text = MW.desalinatorParams.regulPressionSaumure.Kd.ToString(ci);
                            checkBox_Temp_Override.IsChecked = MW.desalinatorParams.regulPressionSaumure.autorisationForcage;
                        }
                        else if (comboBox_Condition.SelectedIndex == 8)//Salinity Regulation (1)
                        {
                            // Affichage du ratio C1 comme consigne et ratio C0 comme mesure
                            tb_pH_setPoint.Text = MW.ratioC1.ToString(ci);
                            tb_pH_Kp.Text = MW.salinityRegulParams.regulSaliniteC0.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.salinityRegulParams.regulSaliniteC0.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.salinityRegulParams.regulSaliniteC0.Kd.ToString(ci);
                            tb_pH_measure.Text = (MW.ratioC0*100).ToString("F2");
                            tb_pH_PIDoutput.Text = MW.salinityRegulParams.regulSaliniteC0.sortiePID_pc.ToString(ci);

                            tb_pH_consigneForcage.Text = MW.salinityRegulParams.regulSaliniteC0.consigneForcage.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.salinityRegulParams.regulSaliniteC0.autorisationForcage;

                            tb_Temp_setPoint.Text = (MW.salinityData.saliniteC0 + MW.salinityRegulParams.regulSaliniteC1.offset).ToString(ci);
                            tb_Temp_Kp.Text = MW.salinityRegulParams.regulSaliniteC1.Kp.ToString(ci);
                            tb_Temp_Ki.Text = MW.salinityRegulParams.regulSaliniteC1.Ki.ToString(ci);
                            tb_Temp_Kd.Text = MW.salinityRegulParams.regulSaliniteC1.Kd.ToString(ci);
                            tb_Temp_measure.Text = MW.salinityData.saliniteC1.ToString(ci);
                            tb_Temp_PIDoutput.Text = MW.salinityRegulParams.regulSaliniteC1.sortiePID_pc.ToString(ci);

                            tb_dT_setPoint.Text = MW.salinityRegulParams.regulSaliniteC1.offset.ToString(ci);

                            tb_Temp_consigneForcage.Text = MW.salinityRegulParams.regulSaliniteC1.consigneForcage.ToString(ci);
                            checkBox_Temp_Override.IsChecked = MW.salinityRegulParams.regulSaliniteC1.autorisationForcage;


                        }
                        else if (comboBox_Condition.SelectedIndex == 9)//Salinity Regulation (2)
                        {
                            // Affichage du ratio C1 comme consigne et ratio C2 comme mesure
                            tb_pH_setPoint.Text = (MW.ratioC1*100).ToString("F2");
                            tb_pH_Kp.Text = MW.salinityRegulParams.regulSaliniteC2.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.salinityRegulParams.regulSaliniteC2.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.salinityRegulParams.regulSaliniteC2.Kd.ToString(ci);
                            tb_pH_measure.Text = (MW.ratioC2*100).ToString("F2");
                            tb_pH_PIDoutput.Text = MW.salinityRegulParams.regulSaliniteC2.sortiePID_pc.ToString(ci);


                            tb_pH_consigneForcage.Text = MW.salinityRegulParams.regulSaliniteC2.consigneForcage.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.salinityRegulParams.regulSaliniteC2.autorisationForcage;

                            tb_Temp_setPoint.Text = (MW.salinityData.saliniteC0 + MW.salinityRegulParams.regulC2_filtre.offset).ToString(ci);
                            tb_Temp_Kp.Text = MW.salinityRegulParams.regulC2_filtre.Kp.ToString(ci);
                            tb_Temp_Ki.Text = MW.salinityRegulParams.regulC2_filtre.Ki.ToString(ci);
                            tb_Temp_Kd.Text = MW.salinityRegulParams.regulC2_filtre.Kd.ToString(ci);
                            tb_Temp_measure.Text = MW.salinityData.saliniteC2.ToString(ci);
                            tb_Temp_PIDoutput.Text = MW.salinityRegulParams.regulC2_filtre.sortiePID_pc.ToString(ci);


                            tb_dT_setPoint.Text = MW.salinityRegulParams.regulC2_filtre.offset.ToString(ci);

                            tb_Temp_consigneForcage.Text = MW.salinityRegulParams.regulC2_filtre.consigneForcage.ToString(ci);
                            checkBox_Temp_Override.IsChecked = MW.salinityRegulParams.regulC2_filtre.autorisationForcage;
                        }
                        else if (comboBox_Condition.SelectedIndex == 10)//Salinity Regulation (3)
                        {
                            // Affichage du ratio C1 comme consigne et ratio C3 comme mesure
                            tb_pH_setPoint.Text = (MW.ratioC1*100).ToString("F2");
                            tb_pH_Kp.Text = MW.salinityRegulParams.regulSaliniteC3.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.salinityRegulParams.regulSaliniteC3.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.salinityRegulParams.regulSaliniteC3.Kd.ToString(ci);
                            tb_pH_measure.Text = (MW.ratioC3*100).ToString("F2");
                            tb_pH_PIDoutput.Text = MW.salinityRegulParams.regulSaliniteC3.sortiePID_pc.ToString(ci);

                            tb_pH_consigneForcage.Text = MW.salinityRegulParams.regulSaliniteC3.consigneForcage.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.salinityRegulParams.regulSaliniteC3.autorisationForcage;
                        }
                        else//=5
                        {
                            tb_dpH_setPoint.Text = MW.pacParams.rTempEC.offset.ToString(ci);
                            tb_pH_setPoint.Text = MW.pacParams.rTempEC.consigne.ToString(ci);
                            tb_pH_consigneForcage.Text = MW.pacParams.rTempEC.consigneForcage.ToString(ci);
                            tb_pH_Kp.Text = MW.pacParams.rTempEC.Kp.ToString(ci);
                            tb_pH_Ki.Text = MW.pacParams.rTempEC.Ki.ToString(ci);
                            tb_pH_Kd.Text = MW.pacParams.rTempEC.Kd.ToString(ci);
                            checkBox_pH_Override.IsChecked = MW.pacParams.rTempEC.autorisationForcage;
                        }


                    }
                });
            }
            catch (Exception e)
            {

            }


        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.Hide();
            e.Cancel = true;
        }
    }
}