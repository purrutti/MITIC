using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
    /// Logique d'interaction pour ReferenceCTDWindow.xaml
    /// </summary>
    public partial class ReferenceCTDWindow : Window
    {
        MainWindow MW = ((MainWindow)Application.Current.MainWindow);
        CancellationTokenSource cts = new CancellationTokenSource();
        CultureInfo ci;

        // Variables pour stocker les moyennes (calculées sur plusieurs échantillons)
        private List<double> samples_ctd = new List<double>();
        private List<double> samples_control = new List<double>();
        private List<double> samples_c0 = new List<double>();
        private List<double> samples_c1 = new List<double>();
        private List<double> samples_c2 = new List<double>();
        private List<double> samples_c3 = new List<double>();
        private List<double> samples_desalinator = new List<double>();

        // Nombre d'échantillons pour la moyenne (configurable)
        private int maxSamples = 10;  // 10 secondes par défaut (1 échantillon par seconde)
        private int averagingTimeMinutes = 10; // Temps en minutes

        // Valeur de référence CTD
        private double ctd_reference = 0;

        public ReferenceCTDWindow()
        {
            InitializeComponent();
            ci = new CultureInfo("en-US");
            RequestSalinityFactors();
            InitializeAsync();
        }

        private async void RequestSalinityFactors()
        {
            // Attendre un peu pour que la fenêtre soit complètement chargée
            await Task.Delay(100);

            // Envoyer la commande 21 pour requêter les facteurs de l'automate Salinity (cID = 8)
            string msgSalinity = "{\"cmd\":21,\"cID\":8,\"sID\":4}";

            // Envoyer la commande 21 pour requêter le facteur de l'automate Desalinator (cID = 7)
            string msgDesalinator = "{\"cmd\":21,\"cID\":7,\"sID\":4}";

            foreach (var ws in MW._sockets)
            {
                if (ws.IsAvailable)
                {
                    await ws.Send(msgSalinity);
                    await Task.Delay(100);
                    await ws.Send(msgDesalinator);
                }
            }
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
            var dueTime = TimeSpan.FromSeconds(0);
            var interval = TimeSpan.FromSeconds(1);

            await RunPeriodicAsync(RefreshData, dueTime, interval, cts.Token);
        }

        private void RefreshData()
        {
            if (MW.salinityData == null || MW.desalinatorData == null)
                return;

            Dispatcher.Invoke(() =>
            {
                // Ajouter les nouveaux échantillons aux listes
                AddSample(samples_ctd, MW.salinityData.CTD_PSU);
                AddSample(samples_control, MW.salinityData.saliniteControl/ MW.salinityFactors.factorControl);
                AddSample(samples_c0, MW.salinityData.saliniteC0/ MW.salinityFactors.factorC0);
                AddSample(samples_c1, MW.salinityData.saliniteC1/ MW.salinityFactors.factorC1);
                AddSample(samples_c2, MW.salinityData.saliniteC2/ MW.salinityFactors.factorC2);
                AddSample(samples_c3, MW.salinityData.saliniteC3/ MW.salinityFactors.factorC3);
                AddSample(samples_desalinator, MW.desalinatorData.salinity/ MW.salinityFactors.factorDesalinator);

                // CTD - Valeur instantanée
                lbl_ctd_instant.Content = MW.salinityData.CTD_PSU.ToString("F2", ci);
                lbl_ctd_moyenne.Content = CalculateAverage(samples_ctd).ToString("F2", ci);
                lbl_ctd_facteur.Content = "1.0000"; // CTD est la référence

                // Control - Valeur instantanée, moyenne et facteur correcteur
                double instant_control = MW.salinityData.saliniteControl;
                double moyenne_control = CalculateAverage(samples_control);
                lbl_control_instant.Content = instant_control.ToString("F2", ci);
                lbl_control_moyenne.Content = moyenne_control.ToString("F2", ci);
                lbl_control_facteur.Content = CalculateCorrectionFactor(moyenne_control).ToString("F4", ci);

                // C0
                double instant_c0 = MW.salinityData.saliniteC0;
                double moyenne_c0 = CalculateAverage(samples_c0);
                lbl_c0_instant.Content = instant_c0.ToString("F2", ci);
                lbl_c0_moyenne.Content = moyenne_c0.ToString("F2", ci);
                lbl_c0_facteur.Content = CalculateCorrectionFactor(moyenne_c0).ToString("F4", ci);

                // C1
                double instant_c1 = MW.salinityData.saliniteC1;
                double moyenne_c1 = CalculateAverage(samples_c1);
                lbl_c1_instant.Content = instant_c1.ToString("F2", ci);
                lbl_c1_moyenne.Content = moyenne_c1.ToString("F2", ci);
                lbl_c1_facteur.Content = CalculateCorrectionFactor(moyenne_c1).ToString("F4", ci);

                // C2
                double instant_c2 = MW.salinityData.saliniteC2;
                double moyenne_c2 = CalculateAverage(samples_c2);
                lbl_c2_instant.Content = instant_c2.ToString("F2", ci);
                lbl_c2_moyenne.Content = moyenne_c2.ToString("F2", ci);
                lbl_c2_facteur.Content = CalculateCorrectionFactor(moyenne_c2).ToString("F4", ci);

                // C3
                double instant_c3 = MW.salinityData.saliniteC3;
                double moyenne_c3 = CalculateAverage(samples_c3);
                lbl_c3_instant.Content = instant_c3.ToString("F2", ci);
                lbl_c3_moyenne.Content = moyenne_c3.ToString("F2", ci);
                lbl_c3_facteur.Content = CalculateCorrectionFactor(moyenne_c3).ToString("F4", ci);

                // Desalinator
                double instant_desalinator = MW.desalinatorData.salinity;
                double moyenne_desalinator = CalculateAverage(samples_desalinator);
                lbl_desalinator_instant.Content = instant_desalinator.ToString("F2", ci);
                lbl_desalinator_moyenne.Content = moyenne_desalinator.ToString("F2", ci);
                lbl_desalinator_facteur.Content = CalculateCorrectionFactor(moyenne_desalinator).ToString("F4", ci);

                // Mettre à jour la référence CTD
                ctd_reference = CalculateAverage(samples_ctd);

                // Afficher les facteurs actuels depuis MainWindow
                lbl_control_actuel.Content = MW.salinityFactors.factorControl.ToString("F4", ci);
                lbl_c0_actuel.Content = MW.salinityFactors.factorC0.ToString("F4", ci);
                lbl_c1_actuel.Content = MW.salinityFactors.factorC1.ToString("F4", ci);
                lbl_c2_actuel.Content = MW.salinityFactors.factorC2.ToString("F4", ci);
                lbl_c3_actuel.Content = MW.salinityFactors.factorC3.ToString("F4", ci);
                lbl_desalinator_actuel.Content = MW.salinityFactors.factorDesalinator.ToString("F4", ci);
            });
        }

        private void AddSample(List<double> samples, double value)
        {
            samples.Add(value);
            if (samples.Count > maxSamples)
            {
                samples.RemoveAt(0); // Retirer le plus ancien échantillon
            }
        }

        private double CalculateAverage(List<double> samples)
        {
            if (samples.Count == 0)
                return 0;
            return samples.Average();
        }

        private double CalculateCorrectionFactor(double moyenne_sonde)
        {
            if (moyenne_sonde == 0 || ctd_reference == 0)
                return 0;
            return ctd_reference / moyenne_sonde;
        }

        private async void btn_Demarrer_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Démarrer l'acquisition de référence CTD ?\n\n" +
                "Cette action va :\n" +
                "- Forcer les vannes de régulation de salinité C1, C2 et C3 à 0% (automate Salinity)\n" +
                "- Forcer les vannes de température et pH de C1, C2 et C3 à 0%\n" +
                "- Forcer la vanne de rebouclage du Desalinator à 0%\n" +
                "- Commencer l'acquisition des données pour calculer les moyennes\n\n" +
                "Les conditions seront stabilisées pour une mesure précise.\n\nAvez vous bien pensé bypasser les mesocosmes manuellement?",
                "Démarrer acquisition",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Forcer les vannes (salinité, température et pH) de C1, C2, C3 à 0%
                await ForceTemperatureValves();

                // Forcer la vanne de rebouclage du Desalinator à 0%
                await ForceDesalinatorRebouclage();

                MessageBox.Show(
                    "Les vannes ont été forcées avec succès !\n\n" +
                    "Vannes de salinité C1, C2, C3 (automate Salinity) : 0%\n" +
                    "Vannes de température et pH C1, C2, C3 : 0%\n" +
                    "Vanne rebouclage Desalinator : 0%\n\n" +
                    "L'acquisition des données est en cours.\n" +
                    "Attendez que les moyennes se stabilisent avant de valider la référence.",
                    "Acquisition démarrée",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async Task ForceTemperatureValves()
        {
            // Forcer les vannes de salinité C1, C2, C3 à 0% (automate Salinity cID = 8)
            MW.salinityRegulParams.regulSaliniteC1.autorisationForcage = true;
            MW.salinityRegulParams.regulSaliniteC1.consigneForcage = 0;

            MW.salinityRegulParams.regulSaliniteC2.autorisationForcage = true;
            MW.salinityRegulParams.regulSaliniteC2.consigneForcage = 100;

            MW.salinityRegulParams.regulSaliniteC3.autorisationForcage = true;
            MW.salinityRegulParams.regulSaliniteC3.consigneForcage = 100;

            // Envoyer les paramètres à l'automate Salinity (cID = 8)
            string msgC1 = "{\"cmd\":18,\"cID\":8,\"sID\":4," +
                          "\"regulC1\":{\"cons\":" + MW.salinityRegulParams.regulSaliniteC1.consigne + "," +
                          "\"Kp\":" + MW.salinityRegulParams.regulSaliniteC1.Kp + "," +
                          "\"Ki\":" + MW.salinityRegulParams.regulSaliniteC1.Ki + "," +
                          "\"Kd\":" + MW.salinityRegulParams.regulSaliniteC1.Kd + "," +
                          "\"consForcage\":" + MW.salinityRegulParams.regulSaliniteC1.consigneForcage + "," +
                          "\"aForcage\":\"true\"}}";

            string msgC2 = "{\"cmd\":18,\"cID\":8,\"sID\":4," +
                          "\"regulC2\":{\"cons\":" + MW.salinityRegulParams.regulSaliniteC2.consigne + "," +
                          "\"Kp\":" + MW.salinityRegulParams.regulSaliniteC2.Kp + "," +
                          "\"Ki\":" + MW.salinityRegulParams.regulSaliniteC2.Ki + "," +
                          "\"Kd\":" + MW.salinityRegulParams.regulSaliniteC2.Kd + "," +
                          "\"consForcage\":" + MW.salinityRegulParams.regulSaliniteC2.consigneForcage + "," +
                          "\"aForcage\":\"true\"}}";

            string msgC3 = "{\"cmd\":18,\"cID\":8,\"sID\":4," +
                          "\"regulC3\":{\"cons\":" + MW.salinityRegulParams.regulSaliniteC3.consigne + "," +
                          "\"Kp\":" + MW.salinityRegulParams.regulSaliniteC3.Kp + "," +
                          "\"Ki\":" + MW.salinityRegulParams.regulSaliniteC3.Ki + "," +
                          "\"Kd\":" + MW.salinityRegulParams.regulSaliniteC3.Kd + "," +
                          "\"consForcage\":" + MW.salinityRegulParams.regulSaliniteC3.consigneForcage + "," +
                          "\"aForcage\":\"true\"}}";

            // Forcer les vannes de température et pH des automates C1, C2, C3 (cID = 1, 2, 3) à 0%
            MW.conditions[1].rpH.autorisationForcage = true;
            MW.conditions[1].rpH.consigneForcage = 0;
            MW.conditions[1].rTemp.autorisationForcage = true;
            MW.conditions[1].rTemp.consigneForcage = 0;

            MW.conditions[2].rpH.autorisationForcage = true;
            MW.conditions[2].rpH.consigneForcage = 0;
            MW.conditions[2].rTemp.autorisationForcage = true;
            MW.conditions[2].rTemp.consigneForcage = 0;

            MW.conditions[3].rpH.autorisationForcage = true;
            MW.conditions[3].rpH.consigneForcage = 0;
            MW.conditions[3].rTemp.autorisationForcage = true;
            MW.conditions[3].rTemp.consigneForcage = 0;

            // Créer les messages JSON pour forcer les vannes de température et pH (cmd:2, cID: 1,2,3)
            string msgTempC1 = "{\"cmd\":2,\"cID\":1,\"sID\":4," +
                              "\"rpH\":{\"cons\":" + MW.conditions[1].rpH.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[1].rpH.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[1].rpH.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[1].rpH.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[1].rpH.consigneForcage + "," +
                              "\"aForcage\":true}," +
                              "\"rTemp\":{\"cons\":" + MW.conditions[1].rTemp.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[1].rTemp.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[1].rTemp.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[1].rTemp.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[1].rTemp.consigneForcage + "," +
                              "\"aForcage\":true}}";

            string msgTempC2 = "{\"cmd\":2,\"cID\":2,\"sID\":4," +
                              "\"rpH\":{\"cons\":" + MW.conditions[2].rpH.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[2].rpH.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[2].rpH.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[2].rpH.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[2].rpH.consigneForcage + "," +
                              "\"aForcage\":true}," +
                              "\"rTemp\":{\"cons\":" + MW.conditions[2].rTemp.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[2].rTemp.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[2].rTemp.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[2].rTemp.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[2].rTemp.consigneForcage + "," +
                              "\"aForcage\":true}}";

            string msgTempC3 = "{\"cmd\":2,\"cID\":3,\"sID\":4," +
                              "\"rpH\":{\"cons\":" + MW.conditions[3].rpH.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[3].rpH.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[3].rpH.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[3].rpH.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[3].rpH.consigneForcage + "," +
                              "\"aForcage\":true}," +
                              "\"rTemp\":{\"cons\":" + MW.conditions[3].rTemp.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[3].rTemp.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[3].rTemp.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[3].rTemp.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[3].rTemp.consigneForcage + "," +
                              "\"aForcage\":true}}";

            MW.comDebugWindow.tb1.Text = msgC1;
            foreach (var ws in MW._sockets)
            {
                if (ws.IsAvailable)
                {
                    // Envoyer les commandes pour l'automate Salinity (cID = 8)
                    await ws.Send(msgC1);
                    await Task.Delay(100);
                    await ws.Send(msgC2);
                    await Task.Delay(100);
                    await ws.Send(msgC3);
                    await Task.Delay(100);

                    // Envoyer les commandes pour les automates C1, C2, C3 (cID = 1, 2, 3)
                    await ws.Send(msgTempC1);
                    await Task.Delay(100);
                    await ws.Send(msgTempC2);
                    await Task.Delay(100);
                    await ws.Send(msgTempC3);
                }
            }
        }

        private async Task ForceDesalinatorRebouclage()
        {
            // Forcer la vanne de rebouclage à 0%
            MW.desalinatorParams.regulRebouclage.autorisationForcage = true;
            MW.desalinatorParams.regulRebouclage.consigneForcage = 0;

            // Envoyer les paramètres à l'automate Desalinator (cID = 7)
            string msg = "{\"cmd\":14,\"cID\":7,\"sID\":4," +
                        "\"regulRebouclage\":{\"cons\":" + MW.desalinatorParams.regulRebouclage.consigne + "," +
                        "\"Kp\":" + MW.desalinatorParams.regulRebouclage.Kp + "," +
                        "\"Ki\":" + MW.desalinatorParams.regulRebouclage.Ki + "," +
                        "\"Kd\":" + MW.desalinatorParams.regulRebouclage.Kd + "," +
                        "\"consForcage\":" + MW.desalinatorParams.regulRebouclage.consigneForcage + "," +
                        "\"aForcage\":\"true\"}}";

            MW.comDebugWindow.tb1.Text = msg;
            foreach (var ws in MW._sockets)
            {
                if (ws.IsAvailable)
                {
                    await ws.Send(msg);
                }
            }
        }

        private void btn_ValiderReference_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement reference validation logic
            // This will save the current CTD values as reference
            MessageBoxResult result = MessageBox.Show(
                $"Valider la référence CTD actuelle ?\n\n" +
                $"CTD Salinité moyenne: {ctd_reference:F2} PSU\n\n" +
                $"Les facteurs correctifs calculés seront appliqués à toutes les sondes:\n" +
                $"  Control: {CalculateCorrectionFactor(CalculateAverage(samples_control)):F4}\n" +
                $"  C0: {CalculateCorrectionFactor(CalculateAverage(samples_c0)):F4}\n" +
                $"  C1: {CalculateCorrectionFactor(CalculateAverage(samples_c1)):F4}\n" +
                $"  C2: {CalculateCorrectionFactor(CalculateAverage(samples_c2)):F4}\n" +
                $"  C3: {CalculateCorrectionFactor(CalculateAverage(samples_c3)):F4}\n" +
                $"  Desalinator: {CalculateCorrectionFactor(CalculateAverage(samples_desalinator)):F4}",
                "Validation référence CTD",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Calculer les facteurs correctifs
                double factorControl = CalculateCorrectionFactor(CalculateAverage(samples_control));
                double factorC0 = CalculateCorrectionFactor(CalculateAverage(samples_c0));
                double factorC1 = CalculateCorrectionFactor(CalculateAverage(samples_c1));
                double factorC2 = CalculateCorrectionFactor(CalculateAverage(samples_c2));
                double factorC3 = CalculateCorrectionFactor(CalculateAverage(samples_c3));
                double factorDesalinator = CalculateCorrectionFactor(CalculateAverage(samples_desalinator));

                // Enregistrer dans MainWindow
                MW.salinityFactors.factorControl = factorControl;
                MW.salinityFactors.factorC0 = factorC0;
                MW.salinityFactors.factorC1 = factorC1;
                MW.salinityFactors.factorC2 = factorC2;
                MW.salinityFactors.factorC3 = factorC3;
                MW.salinityFactors.factorDesalinator = factorDesalinator;

                // Envoyer les facteurs à l'automate Salinity (cID = 8)
                SendSalinityFactorsAsync(8, factorControl, factorC0, factorC1, factorC2, factorC3);

                // Envoyer le facteur Desalinator à l'automate Desalinator (cID = 7)
                SendDesalinatorFactorAsync(7, factorDesalinator);

                MessageBox.Show("Référence CTD validée avec succès !\n\nLes facteurs correctifs ont été envoyés aux automates.",
                    "Validation référence", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task SendSalinityFactorsAsync(int cID, double factorControl, double factorC0, double factorC1, double factorC2, double factorC3)
        {
            // Créer le message JSON avec les facteurs correctifs pour Salinity
            string msg = "{\"cmd\":22,\"cID\":" + cID + ",\"sID\":4," +
                         "\"factorControl\":" + factorControl.ToString("F4", ci) + "," +
                         "\"factorC0\":" + factorC0.ToString("F4", ci) + "," +
                         "\"factorC1\":" + factorC1.ToString("F4", ci) + "," +
                         "\"factorC2\":" + factorC2.ToString("F4", ci) + "," +
                         "\"factorC3\":" + factorC3.ToString("F4", ci) + "}";

            MW.comDebugWindow.tb1.Text = msg;

            foreach (var ws in MW._sockets)
            {
                if (ws.IsAvailable)
                {
                    await ws.Send(msg);
                }
            }
        }

        private async Task SendDesalinatorFactorAsync(int cID, double factorDesalinator)
        {
            // Créer le message JSON avec le facteur correctif pour Desalinator
            string msg = "{\"cmd\":22,\"cID\":" + cID + ",\"sID\":4," +
                         "\"factorDesalinator\":" + factorDesalinator.ToString("F4", ci) + "}";

            MW.comDebugWindow.tb1.Text = msg;

            foreach (var ws in MW._sockets)
            {
                if (ws.IsAvailable)
                {
                    await ws.Send(msg);
                }
            }
        }

        private async void btn_Arreter_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Arrêter l'acquisition et déforcer toutes les vannes ?\n\n" +
                "Cette action va :\n" +
                "- Désactiver le forçage des vannes de salinité C1, C2, C3 (automate Salinity)\n" +
                "- Désactiver le forçage des vannes de température et pH de C1, C2, C3\n" +
                "- Désactiver le forçage de la vanne de rebouclage du Desalinator\n\n" +
                "Les régulations reprendront leur fonctionnement normal.",
                "Arrêter acquisition",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Déforcer toutes les vannes
                await UnforceAllValves();

                MessageBox.Show(
                    "Le forçage des vannes a été désactivé avec succès !\n\n" +
                    "Toutes les régulations ont repris leur fonctionnement normal.\n" +
                    "Les vannes sont maintenant contrôlées par les PID.",
                    "Forçage désactivé",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private async Task UnforceAllValves()
        {
            // Déforcer les vannes de salinité C1, C2, C3 (automate Salinity cID = 8)
            MW.salinityRegulParams.regulSaliniteC1.autorisationForcage = false;
            MW.salinityRegulParams.regulSaliniteC2.autorisationForcage = false;
            MW.salinityRegulParams.regulSaliniteC3.autorisationForcage = false;

            // Envoyer les paramètres à l'automate Salinity (cID = 8)
            string msgC1 = "{\"cmd\":18,\"cID\":8,\"sID\":4," +
                          "\"regulC1\":{\"cons\":" + MW.salinityRegulParams.regulSaliniteC1.consigne + "," +
                          "\"Kp\":" + MW.salinityRegulParams.regulSaliniteC1.Kp + "," +
                          "\"Ki\":" + MW.salinityRegulParams.regulSaliniteC1.Ki + "," +
                          "\"Kd\":" + MW.salinityRegulParams.regulSaliniteC1.Kd + "," +
                          "\"consForcage\":" + MW.salinityRegulParams.regulSaliniteC1.consigneForcage + "," +
                          "\"aForcage\":false}}";

            string msgC2 = "{\"cmd\":18,\"cID\":8,\"sID\":4," +
                          "\"regulC2\":{\"cons\":" + MW.salinityRegulParams.regulSaliniteC2.consigne + "," +
                          "\"Kp\":" + MW.salinityRegulParams.regulSaliniteC2.Kp + "," +
                          "\"Ki\":" + MW.salinityRegulParams.regulSaliniteC2.Ki + "," +
                          "\"Kd\":" + MW.salinityRegulParams.regulSaliniteC2.Kd + "," +
                          "\"consForcage\":" + MW.salinityRegulParams.regulSaliniteC2.consigneForcage + "," +
                          "\"aForcage\":false}}";

            string msgC3 = "{\"cmd\":18,\"cID\":8,\"sID\":4," +
                          "\"regulC3\":{\"cons\":" + MW.salinityRegulParams.regulSaliniteC3.consigne + "," +
                          "\"Kp\":" + MW.salinityRegulParams.regulSaliniteC3.Kp + "," +
                          "\"Ki\":" + MW.salinityRegulParams.regulSaliniteC3.Ki + "," +
                          "\"Kd\":" + MW.salinityRegulParams.regulSaliniteC3.Kd + "," +
                          "\"consForcage\":" + MW.salinityRegulParams.regulSaliniteC3.consigneForcage + "," +
                          "\"aForcage\":false}}";

            // Déforcer les vannes de température et pH des automates C1, C2, C3 (cID = 1, 2, 3)
            MW.conditions[1].rpH.autorisationForcage = false;
            MW.conditions[1].rTemp.autorisationForcage = false;
            MW.conditions[2].rpH.autorisationForcage = false;
            MW.conditions[2].rTemp.autorisationForcage = false;
            MW.conditions[3].rpH.autorisationForcage = false;
            MW.conditions[3].rTemp.autorisationForcage = false;

            // Créer les messages JSON pour déforcer les vannes de température et pH (cmd:2, cID: 1,2,3)
            string msgTempC1 = "{\"cmd\":2,\"cID\":1,\"sID\":4," +
                              "\"rpH\":{\"cons\":" + MW.conditions[1].rpH.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[1].rpH.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[1].rpH.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[1].rpH.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[1].rpH.consigneForcage + "," +
                              "\"aForcage\":false}," +
                              "\"rTemp\":{\"cons\":" + MW.conditions[1].rTemp.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[1].rTemp.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[1].rTemp.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[1].rTemp.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[1].rTemp.consigneForcage + "," +
                              "\"aForcage\":false}}";

            string msgTempC2 = "{\"cmd\":2,\"cID\":2,\"sID\":4," +
                              "\"rpH\":{\"cons\":" + MW.conditions[2].rpH.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[2].rpH.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[2].rpH.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[2].rpH.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[2].rpH.consigneForcage + "," +
                              "\"aForcage\":false}," +
                              "\"rTemp\":{\"cons\":" + MW.conditions[2].rTemp.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[2].rTemp.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[2].rTemp.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[2].rTemp.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[2].rTemp.consigneForcage + "," +
                              "\"aForcage\":false}}";

            string msgTempC3 = "{\"cmd\":2,\"cID\":3,\"sID\":4," +
                              "\"rpH\":{\"cons\":" + MW.conditions[3].rpH.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[3].rpH.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[3].rpH.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[3].rpH.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[3].rpH.consigneForcage + "," +
                              "\"aForcage\":false}," +
                              "\"rTemp\":{\"cons\":" + MW.conditions[3].rTemp.consigne.ToString(ci) + "," +
                              "\"Kp\":" + MW.conditions[3].rTemp.Kp.ToString(ci) + "," +
                              "\"Ki\":" + MW.conditions[3].rTemp.Ki.ToString(ci) + "," +
                              "\"Kd\":" + MW.conditions[3].rTemp.Kd.ToString(ci) + "," +
                              "\"consForcage\":" + MW.conditions[3].rTemp.consigneForcage + "," +
                              "\"aForcage\":false}}";

            // Déforcer la vanne de rebouclage du Desalinator
            MW.desalinatorParams.regulRebouclage.autorisationForcage = false;

            string msgDesalinator = "{\"cmd\":14,\"cID\":7,\"sID\":4," +
                                   "\"regulRebouclage\":{\"cons\":" + MW.desalinatorParams.regulRebouclage.consigne + "," +
                                   "\"Kp\":" + MW.desalinatorParams.regulRebouclage.Kp + "," +
                                   "\"Ki\":" + MW.desalinatorParams.regulRebouclage.Ki + "," +
                                   "\"Kd\":" + MW.desalinatorParams.regulRebouclage.Kd + "," +
                                   "\"consForcage\":" + MW.desalinatorParams.regulRebouclage.consigneForcage + "," +
                                   "\"aForcage\":false}}";

            MW.comDebugWindow.tb1.Text = msgC1;
            foreach (var ws in MW._sockets)
            {
                if (ws.IsAvailable)
                {
                    // Envoyer les commandes pour l'automate Salinity (cID = 8)
                    await ws.Send(msgC1);
                    await Task.Delay(100);
                    await ws.Send(msgC2);
                    await Task.Delay(100);
                    await ws.Send(msgC3);
                    await Task.Delay(100);

                    // Envoyer les commandes pour les automates C1, C2, C3 (cID = 1, 2, 3)
                    await ws.Send(msgTempC1);
                    await Task.Delay(100);
                    await ws.Send(msgTempC2);
                    await Task.Delay(100);
                    await ws.Send(msgTempC3);
                    await Task.Delay(100);

                    // Envoyer la commande pour le Desalinator (cID = 7)
                    await ws.Send(msgDesalinator);
                }
            }
        }

        private void btn_ResetFactors_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "Réinitialiser tous les facteurs correctifs à 1.0 ?\n\n" +
                "Cette action va :\n" +
                "- Remettre tous les facteurs à 1.0 dans l'application\n" +
                "- Envoyer les facteurs aux automates Salinity et Desalinator\n" +
                "- Les mesures ne seront plus corrigées",
                "Réinitialisation des facteurs",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Réinitialiser tous les facteurs à 1.0
                MW.salinityFactors.factorControl = 1.0;
                MW.salinityFactors.factorC0 = 1.0;
                MW.salinityFactors.factorC1 = 1.0;
                MW.salinityFactors.factorC2 = 1.0;
                MW.salinityFactors.factorC3 = 1.0;
                MW.salinityFactors.factorDesalinator = 1.0;

                // Envoyer les facteurs à l'automate Salinity (cID = 8)
                SendSalinityFactorsAsync(8, 1.0, 1.0, 1.0, 1.0, 1.0);

                // Envoyer le facteur Desalinator à l'automate Desalinator (cID = 7)
                SendDesalinatorFactorAsync(7, 1.0);

                MessageBox.Show("Tous les facteurs correctifs ont été réinitialisés à 1.0 et envoyés aux automates.",
                    "Réinitialisation terminée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btn_ApplyAveragingTime_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int minutes = int.Parse(txt_averaging_time.Text);

                if (minutes < 1 || minutes > 60)
                {
                    MessageBox.Show("Le temps de moyenne doit être entre 1 et 60 minutes.",
                        "Valeur invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                averagingTimeMinutes = minutes;
                // 1 échantillon par seconde, donc 60 échantillons par minute
                maxSamples = minutes * 60;

                // Mettre à jour l'affichage
                lbl_samples_info.Content = $"({maxSamples} échantillons sur {minutes} min)";

                MessageBox.Show($"Temps de moyenne configuré à {minutes} minute(s).\n" +
                    $"Les moyennes seront calculées sur les {maxSamples} derniers échantillons.",
                    "Configuration appliquée", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (FormatException)
            {
                MessageBox.Show("Veuillez entrer un nombre valide.",
                    "Erreur de format", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing_1(object sender, CancelEventArgs e)
        {
            // Cancel periodic refresh
            cts.Cancel();

            // Hide window instead of closing to preserve instance
            this.Hide();
            e.Cancel = true;
        }
    }
}
