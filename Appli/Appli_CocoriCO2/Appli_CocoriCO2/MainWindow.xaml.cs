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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using LiveCharts;
using LiveCharts.Configurations;
using System.Diagnostics;
using SlackAPI;
using System.Net;
using System.Collections.Concurrent;
using JsonConverter = Newtonsoft.Json.JsonConverter;
using Newtonsoft.Json.Linq;
using System.Reflection;

using Fleck;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Appli_CocoriCO2
{
    public class PreserveExistingPropertiesConverter<T> : Newtonsoft.Json.JsonConverter<T> where T : class, new()
    {
        public override T ReadJson(JsonReader reader, Type objectType, T existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            if (!hasExistingValue)
                existingValue = new T();

            foreach (var property in typeof(T).GetProperties())
            {
                var attribute = property.GetCustomAttribute<JsonPropertyAttribute>();
                if (attribute != null)
                {
                    var jsonPropertyName = attribute.PropertyName;
                    if (jObject.ContainsKey(jsonPropertyName))
                    {
                        var value = jObject[jsonPropertyName].ToObject(property.PropertyType, serializer);
                        property.SetValue(existingValue, value);
                    }
                }
                else if (jObject.ContainsKey(property.Name))
                {
                    var value = jObject[property.Name].ToObject(property.PropertyType, serializer);
                    property.SetValue(existingValue, value);
                }
            }

            return existingValue;
        }

        public override void WriteJson(JsonWriter writer, T value, Newtonsoft.Json.JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }


    public static class JsonHelper
    {
        public static T DeserializePreservingExisting<T>(string json, T existingObject = null) where T : class, new()
        {
            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new PreserveExistingPropertiesConverter<T>() }
            };

            var serializer = Newtonsoft.Json.JsonSerializer.Create(settings);
            var jObject = JObject.Parse(json);

            if (existingObject == null)
                existingObject = new T();

            serializer.Populate(jObject.CreateReader(), existingObject);

            return existingObject;
        }
    }
    public class TrameJson
    {
        [JsonProperty("cmd", Required = Required.Default)]
        public int cmd { get; set; }
        [JsonProperty("cID", Required = Required.Default)]
        public int cID { get; set; }
        [JsonProperty("sID", Required = Required.Default)]
        public int sID { get; set; }
    }
    public class Ambiant
    {

        [JsonProperty("sun", Required = Required.Default)]
        public bool sun { get; set; }
        [JsonProperty("tide", Required = Required.Default)]
        public bool tide { get; set; }
        [JsonProperty("oxy", Required = Required.Default)]
        public double oxy { get; set; }
        [JsonProperty("cond", Required = Required.Default)]
        public double cond { get; set; }
        [JsonProperty("turb", Required = Required.Default)]
        public double turb { get; set; }
        [JsonProperty("fluo", Required = Required.Default)]
        public double fluo { get; set; }
        [JsonProperty("temp", Required = Required.Default)]
        public double temperature { get; set; }
        [JsonProperty("sal", Required = Required.Default)]
        public double salinite { get; set; }
        [JsonProperty("pH", Required = Required.Default)]
        public double pH { get; set; }
        [JsonProperty("C0_pH", Required = Required.Default)]
        public double C0_pH { get; set; }
        [JsonProperty("C0_temp", Required = Required.Default)]
        public double C0_temp { get; set; }
        [JsonProperty("sPID_EA", Required = Required.Default)]
        public double sortiePID_EA { get; set; }
        [JsonProperty("sPID_EC", Required = Required.Default)]
        public double sortiePID_EC { get; set; }
        [JsonProperty("sPID_TEC", Required = Required.Default)]
        public double sortiePID_TEC { get; set; }
        [JsonProperty("tempPAC", Required = Required.Default)]
        public double tempPAC { get; set; }
        [JsonProperty("pressionEA", Required = Required.Default)]
        public double pressionEA { get; set; }
        [JsonProperty("pressionEC", Required = Required.Default)]
        public double pressionEC { get; set; }

        [JsonProperty("nextSunUp", Required = Required.Default)]
        public long nextSunUp { get; set; }
        [JsonProperty("nextSunDown", Required = Required.Default)]
        public long nextSunDown { get; set; }

        public long time { get; set; }
        public DateTime lastUpdated { get; set; }
    }
    public class Mesocosme
    {
        [JsonProperty("MesoID", Required = Required.Default)]
        public int mesocosmeID { get; set; }
        [JsonProperty("LevelH", Required = Required.Default)]
        public bool alarmeNiveauHaut { get; set; }
        [JsonProperty("LevelL", Required = Required.Default)]
        public bool alarmeNiveauBas { get; set; }
        [JsonProperty("LevelLL", Required = Required.Default)]
        public bool alarmeNiveauTresBas { get; set; }
        [JsonProperty("debit", Required = Required.Default)]
        public double debit { get; set; }
        [JsonProperty("temp", Required = Required.Default)]
        public double temperature { get; set; }
        [JsonProperty("pH", Required = Required.Default)]
        public double pH { get; set; }
    }
    public class Regul
    {
        [JsonProperty("sPID", Required = Required.Default)]
        public double sortiePID { get; set; }
        [JsonProperty("cons", Required = Required.Default)]
        public double consigne { get; set; }
        [JsonProperty("Kp", Required = Required.Default)]
        public double Kp { get; set; }
        [JsonProperty("Ki", Required = Required.Default)]
        public double Ki { get; set; }
        [JsonProperty("Kd", Required = Required.Default)]
        public double Kd { get; set; }
        [JsonProperty("sPID_pc", Required = Required.Default)]
        public double sortiePID_pc { get; set; }
        [JsonProperty("aForcage", Required = Required.Default)]
        public bool autorisationForcage { get; set; }
        [JsonProperty("consForcage", Required = Required.Default)]
        public int consigneForcage { get; set; }
        [JsonProperty("offset", Required = Required.Default)]
        public double offset { get; set; }

    }
    public class Condition
    {
        [JsonProperty("cmd", Required = Required.Default)]
        public int command { get; set; }
        [JsonProperty("temp", Required = Required.Default)]
        public double temperature { get; set; }
        [JsonProperty("pH", Required = Required.Default)]
        public double pH { get; set; }
        [JsonProperty("cID", Required = Required.Default)]
        public int condID { get; set; }
        [JsonProperty("data", Required = Required.Default)]
        public Mesocosme[] Meso { get; set; }
        [JsonProperty("rTemp", Required = Required.Default)]
        public Regul rTemp { get; set; }
        [JsonProperty("rpH", Required = Required.Default)]
        public Regul rpH { get; set; }
        public long time { get; set; }
        public DateTime lastUpdated { get; set; }

    }

    public class MasterParams
    {
        [JsonProperty("rPressionEA", Required = Required.Default)]
        public Regul regulPressionEA;
        [JsonProperty("rPressionEC", Required = Required.Default)]
        public Regul regulPressionEC;
        public MasterParams()
        {
            regulPressionEA = new Regul();
            regulPressionEC = new Regul();
        }
    }

    public class PACParams
    {

        [JsonProperty("rTempEC", Required = Required.Default)]
        public Regul rTempEC;
        public PACParams()
        {
            rTempEC = new Regul();
        }
    }

    // Classes pour Desalinator (PLCID = 7)
    public class DesalinatorData
    {
        [JsonProperty("pressionHP", Required = Required.Default)]
        public double pressionHP { get; set; }
        [JsonProperty("pressionEntree", Required = Required.Default)]
        public double pressionEntree { get; set; }
        [JsonProperty("pressionSaumure", Required = Required.Default)]
        public double pressionSaumure { get; set; }
        [JsonProperty("pressionFresh", Required = Required.Default)]
        public double pressionFresh { get; set; }

        [JsonProperty("debitEntree", Required = Required.Default)]
        public double debitEntree { get; set; }
        [JsonProperty("debitMOI", Required = Required.Default)]
        public double debitMOI { get; set; }
        [JsonProperty("debitSaumure", Required = Required.Default)]
        public double debitSaumure { get; set; }
        [JsonProperty("debitFresh", Required = Required.Default)]
        public double debitFresh { get; set; }

        [JsonProperty("vanneEntree", Required = Required.Default)]
        public double vanneEntree { get; set; }
        [JsonProperty("vanneSaumure", Required = Required.Default)]
        public double vanneSaumure { get; set; }
        [JsonProperty("vanneFresh", Required = Required.Default)]
        public double vanneFresh { get; set; }
        [JsonProperty("v3VMOI", Required = Required.Default)]
        public double v3VMOI { get; set; }

        [JsonProperty("pompeHP", Required = Required.Default)]
        public bool pompeHP { get; set; }
        [JsonProperty("conductivity", Required = Required.Default)]
        public double conductivity { get; set; }
        [JsonProperty("salinity", Required = Required.Default)]
        public double salinity { get; set; }
        [JsonProperty("temperature", Required = Required.Default)]
        public double temperature { get; set; }

        public long time { get; set; }
        public DateTime lastUpdated { get; set; }
    }

    // Classes pour MITIC_RegulSalinite (PLCID = 8)
    public class SalinityData
    {
        [JsonProperty("conductiviteC0", Required = Required.Default)]
        public double conductiviteC0 { get; set; }
        [JsonProperty("conductiviteControl", Required = Required.Default)]
        public double conductiviteControl { get; set; }
        [JsonProperty("conductiviteC1", Required = Required.Default)]
        public double conductiviteC1 { get; set; }
        [JsonProperty("conductiviteC2", Required = Required.Default)]
        public double conductiviteC2 { get; set; }
        [JsonProperty("conductiviteC3", Required = Required.Default)]
        public double conductiviteC3 { get; set; }

        [JsonProperty("saliniteC0", Required = Required.Default)]
        public double saliniteC0 { get; set; }
        [JsonProperty("saliniteControl", Required = Required.Default)]
        public double saliniteControl { get; set; }
        [JsonProperty("saliniteC1", Required = Required.Default)]
        public double saliniteC1 { get; set; }
        [JsonProperty("saliniteC2", Required = Required.Default)]
        public double saliniteC2 { get; set; }
        [JsonProperty("saliniteC3", Required = Required.Default)]
        public double saliniteC3 { get; set; }

        [JsonProperty("temperatureC0", Required = Required.Default)]
        public double temperatureC0 { get; set; }
        [JsonProperty("temperatureControl", Required = Required.Default)]
        public double temperatureControl { get; set; }
        [JsonProperty("temperatureC1", Required = Required.Default)]
        public double temperatureC1 { get; set; }
        [JsonProperty("temperatureC2", Required = Required.Default)]
        public double temperatureC2 { get; set; }
        [JsonProperty("temperatureC3", Required = Required.Default)]
        public double temperatureC3 { get; set; }

        [JsonProperty("debitC0", Required = Required.Default)]
        public double debitC0 { get; set; }
        [JsonProperty("debitC1", Required = Required.Default)]
        public double debitC1 { get; set; }
        [JsonProperty("debitC2", Required = Required.Default)]
        public double debitC2 { get; set; }
        [JsonProperty("debitC3", Required = Required.Default)]
        public double debitC3 { get; set; }

        [JsonProperty("vanneC0", Required = Required.Default)]
        public double vanneC0 { get; set; }
        [JsonProperty("vanneC1", Required = Required.Default)]
        public double vanneC1 { get; set; }
        [JsonProperty("vanneC2", Required = Required.Default)]
        public double vanneC2 { get; set; }
        [JsonProperty("vanneC3", Required = Required.Default)]
        public double vanneC3 { get; set; }
        [JsonProperty("vanneC2_filtre", Required = Required.Default)]
        public double vanneC2_filtre { get; set; }

        // CTD Data integrated
        [JsonProperty("CTD_Temperature", Required = Required.Default)]
        public double CTD_Temperature { get; set; }
        [JsonProperty("CTD_Conductivity", Required = Required.Default)]
        public double CTD_Conductivity { get; set; }
        [JsonProperty("CTD_Oxygen", Required = Required.Default)]
        public double CTD_Oxygen { get; set; }
        [JsonProperty("CTD_PSU", Required = Required.Default)]
        public double CTD_PSU { get; set; }
        [JsonProperty("CTD_CalculatedPSU", Required = Required.Default)]
        public double CTD_CalculatedPSU { get; set; }
        [JsonProperty("CTD_Date", Required = Required.Default)]
        public string CTD_Date { get; set; }
        [JsonProperty("CTD_Time", Required = Required.Default)]
        public string CTD_Time { get; set; }
        [JsonProperty("CTD_RawData", Required = Required.Default)]
        public string CTD_RawData { get; set; }

        public long time { get; set; }
        public DateTime lastUpdated { get; set; }
    }

    public class SalinityRegulParams
    {
        [JsonProperty("regulC0", Required = Required.Default)]
        public Regul regulSaliniteC0;
        [JsonProperty("regulC1", Required = Required.Default)]
        public Regul regulSaliniteC1;
        [JsonProperty("regulC2", Required = Required.Default)]
        public Regul regulSaliniteC2;
        [JsonProperty("regulC3", Required = Required.Default)]
        public Regul regulSaliniteC3;
        [JsonProperty("regulC2_filtre", Required = Required.Default)]
        public Regul regulC2_filtre;

        public SalinityRegulParams()
        {
            regulSaliniteC0 = new Regul();
            regulSaliniteC1 = new Regul();
            regulSaliniteC2 = new Regul();
            regulSaliniteC3 = new Regul();
            regulC2_filtre = new Regul();
        }
    }

    // Classe pour stocker les facteurs correctifs de salinité
    public class SalinityFactors
    {
        [JsonProperty("factorControl", Required = Required.Default)]
        public double factorControl { get; set; }
        [JsonProperty("factorC0", Required = Required.Default)]
        public double factorC0 { get; set; }
        [JsonProperty("factorC1", Required = Required.Default)]
        public double factorC1 { get; set; }
        [JsonProperty("factorC2", Required = Required.Default)]
        public double factorC2 { get; set; }
        [JsonProperty("factorC3", Required = Required.Default)]
        public double factorC3 { get; set; }
        [JsonProperty("factorDesalinator", Required = Required.Default)]
        public double factorDesalinator { get; set; }

        public SalinityFactors()
        {
            factorControl = 1.0;
            factorC0 = 1.0;
            factorC1 = 1.0;
            factorC2 = 1.0;
            factorC3 = 1.0;
            factorDesalinator = 1.0;
        }
    }


    // Classes pour les paramètres Desalinator (PLCID = 7)
    public class DesalinatorParams
    {
        [JsonProperty("HP_threshold", Required = Required.Default)]
        public double HP_threshold;
        [JsonProperty("pompeHP", Required = Required.Default)]
        public bool pompeHP;
        [JsonProperty("pressionEntreeThreshold", Required = Required.Default)]
        public double pressionEntreeThreshold;
        [JsonProperty("debitMOIThreshold", Required = Required.Default)]
        public double debitMOIThreshold;
        [JsonProperty("regulRebouclage", Required = Required.Default)]
        public Regul regulRebouclage;
        [JsonProperty("regulPressionEntree", Required = Required.Default)]
        public Regul regulPressionEntree;
        [JsonProperty("regulPressionSaumure", Required = Required.Default)]
        public Regul regulPressionSaumure;
        [JsonProperty("regulPressionFresh", Required = Required.Default)]
        public Regul regulPressionFresh;

        public DesalinatorParams()
        {
            HP_threshold = -1;
            pompeHP = false;
            pressionEntreeThreshold = 0.2;
            debitMOIThreshold = 11.0;
            regulRebouclage = new Regul();
            regulPressionEntree = new Regul();
            regulPressionSaumure = new Regul();
            regulPressionFresh = new Regul();
        }
    }

    public unsafe class Alarme
    {
        public string libelle { get; set; }
        public DateTime dtTriggered { get; set; }
        public DateTime dtRaised { get; set; }
        public DateTime dtAcknowledged { get; set; }
        public TimeSpan delay { get; set; }
        public double threshold { get; set; }
        public double delta { get; set; }
        public double value { get; set; }
        public bool enabled { get; set; }
        public bool triggered { get; set; }
        public bool raised { get; set; }
        public bool acknowledged { get; set; }
        public int comparaison { get; set; }


        private void MakePostRequest(string RequestUrl, string Content)
        {
            HttpClient httpClient = new HttpClient();
            HttpContent httpContent = new StringContent(Content);

            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            try
            {
                httpClient.PostAsync(RequestUrl, httpContent).ConfigureAwait(false);
            }
            catch (HttpRequestException hre)
            {
                Console.WriteLine("hre.Message");
            }
        }
        public bool checkAndRaise(double val) // raise alarm if value is upperThan threshold
        {
            value = val;
            if (!enabled) return false;
            bool upperThan, lowerThan;
            switch (comparaison)
            {
                case 0:
                    upperThan = true;
                    lowerThan = false;
                    break;
                case 1:
                    upperThan = false;
                    lowerThan = true;
                    break;
                case 2:
                    upperThan = true;
                    lowerThan = true;
                    break;
                default:
                    upperThan = false;
                    lowerThan = false;
                    break;
            }

            bool a = false;

            if (upperThan && value >= (threshold + delta)) a = true;
            if (lowerThan && value <= (threshold - delta)) a = true;

            if (!raised && triggered && a && dtTriggered.Add(delay) < DateTime.Now)
            {
                raised = true;
                dtRaised = DateTime.Now;
                sendSlackMessage(this.libelle + ": Measure = " + value.ToString() + ", Set point = " + threshold.ToString() + ", triggered at:" + dtTriggered.ToString()); ;
                return true;
            }

            if (!triggered && upperThan && a)
            {
                dtTriggered = DateTime.Now;
                triggered = true;
            }
            if (!triggered && lowerThan && a)
            {
                dtTriggered = DateTime.Now;
                triggered = true;
            }
            if (!a) triggered = false;

            return false;
        }

        public void sendSlackMessage(String msg)
        {
            string TOKEN = Properties.Settings.Default["SlackToken"].ToString();  // token from last step in section above
            var slackClient = new SlackTaskClient(TOKEN);

            slackClient.PostMessageAsync(Properties.Settings.Default["SlackChannelID"].ToString(), msg);
            MakePostRequest(TOKEN, "{ \"text\":\"" + msg + "\"}");


        }

        public bool checkAndRaise(bool val, bool th) // raise alarm if value is upperThan threshold
        {
            if (!enabled) return false;

            bool v = val;
            if (!triggered && v != th)
            {
                triggered = true;
                dtTriggered = DateTime.Now;
            }
            else triggered = false;

            if (!raised && triggered && dtTriggered.Add(delay) > DateTime.Now)
            {
                raised = true;
                dtRaised = DateTime.Now;
                sendSlackMessage(this.libelle + " triggered at:" + dtTriggered.ToString());

            }
            if (raised) return true;
            return false;
        }


        public unsafe void set(string l, bool ena, int comp, double d, TimeSpan del)
        {
            libelle = l;
            enabled = ena;
            comparaison = comp;
            delta = d;
            delay = del;
        }
    }



    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WebSocketServer _server;
        public List<IWebSocketConnection> _sockets;

        public bool automateSalFreezed = false;


        //public List<Condition> conditions;
        public ObservableCollection<Condition> conditions;
        public ObservableCollection<Alarme> alarms;
        public Ambiant ambiantConditions = new Ambiant();

        //public List<Condition> conditionData;
        public ExpSettingsWindow expSettingsWindow;
        public AlarmsListWindow alarmsListWindow;
        public ComDebugWindow comDebugWindow;
        public Calibration calibrationWindow;
        public DesalinatorWindow desalinatorWindow;
        public SalinityWindow salinityWindow;
        public ReferenceCTDWindow referenceCTDWindow;
        public CultureInfo ci;

        public int step;

        public bool cleanupMode;

        public MasterParams masterParams;
        public PACParams pacParams;

        // Données des nouveaux contrôleurs MITIC
        public DesalinatorData desalinatorData;
        public SalinityData salinityData;
        public SalinityRegulParams salinityRegulParams;
        public DesalinatorParams desalinatorParams;
        public SalinityFactors salinityFactors;

        // Ratios de régulation de salinité
        public double ratioC0 = 0;
        public double ratioC1 = 0;
        public double ratioC2 = 0;
        public double ratioC3 = 0;

        public string[] Labels = new[] { "0" };

        public DateTime lastDataReceived;

        private void startServer()
        {
            _sockets = new List<IWebSocketConnection>();
            //_server = new WebSocketServer("ws://127.0.0.1:81");
            _server = new WebSocketServer("ws://192.168.1.10:81");
            _server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    _sockets.Add(socket);
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Close!");
                    _sockets.Remove(socket);
                };
                socket.OnMessage = message =>
                {
                    Console.WriteLine(message);
                    // Handle incoming messages here


                    string response = ReadData(message); // Call your existing logic

                    if (response.Length > 1) socket.Send(response);
                    /*foreach (var sock in _sockets)
                    {
                        sock.Send(response);
                    }*/
                };
            });

        }

        public void SendWebSocketCommand(string command)
        {
            try
            {
                if (_sockets != null && _sockets.Count > 0)
                {
                    foreach (var socket in _sockets)
                    {
                        if (socket.ConnectionInfo.ClientIpAddress != null)
                        {
                            socket.Send(command);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending WebSocket command: {ex.Message}");
            }
        }

        public MainWindow()
        {
            if (Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                MessageBox.Show("CocoriCO2 Application is already running. Only one instance of this application is allowed");
                System.Windows.Application.Current.Shutdown();
            }
            else
            {
                InitializeComponent();


                startServer();



                var cts = new CancellationTokenSource();
                cleanupMode = false;



                conditions = new ObservableCollection<Condition>();

                alarms = new ObservableCollection<Alarme>();
                for (int i = 0; i < 4; i++)
                {
                    Condition c = new Condition();
                    c.condID = i;
                    c.rpH = new Regul();
                    c.rTemp = new Regul();
                    c.Meso = new Mesocosme[3];
                    for (int j = 0; j < 3; j++) c.Meso[j] = new Mesocosme();
                    c.temperature = i + 29.99;
                    conditions.Add(c);
                }



                expSettingsWindow = new ExpSettingsWindow();
                comDebugWindow = new ComDebugWindow();
                calibrationWindow = new Calibration();
                desalinatorWindow = new DesalinatorWindow();


                InitializeAsyncAlarms();

                masterParams = new MasterParams();
                pacParams = new PACParams();

                // Initialisation des nouveaux contrôleurs MITIC
                desalinatorData = new DesalinatorData();
                salinityData = new SalinityData();
                salinityRegulParams = new SalinityRegulParams();
                desalinatorParams = new DesalinatorParams();
                salinityFactors = new SalinityFactors();

                ci = new CultureInfo("en-US");
                ci.NumberFormat.NumberDecimalDigits = 2;
                ci.NumberFormat.NumberDecimalSeparator = ".";
                ci.NumberFormat.NumberGroupSeparator = " ";
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
                CultureInfo.DefaultThreadCurrentCulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;

                setAlarms();

                alarmsListWindow = new AlarmsListWindow();
                //StartWSServer();
            }


        }

        private void checkAlarme(string libelle, double value, double t)
        {
            try
            {
                Alarme a = alarms.Single(alarm => alarm.libelle == libelle);
                a.threshold = t;
                a.checkAndRaise(value);
            }
            catch (Exception e)
            {

            }

        }

        private void checkAlarme(string libelle, bool value, bool threshold)
        {


            try
            {
                Alarme a = alarms.Single(alarm => alarm.libelle == libelle);
                a.checkAndRaise(value, threshold);
            }
            catch (Exception e)
            {

            }
        }

        private void checkAlarms()
        {
            if (cleanupMode) return;
            double d;

            string cond, meso;



            if (salinityData.lastUpdated < DateTime.Now - TimeSpan.FromSeconds(60)) automateSalFreezed = true;
            else automateSalFreezed = false;

            checkAlarme("Alarm Automate Salinity Freezed", automateSalFreezed, false);
            checkAlarme("Alarm Desalinator Pump Off", desalinatorData.pompeHP, true);

            checkAlarme("Alarm Pressure Desalinator Inlet", desalinatorData.pressionEntree, desalinatorParams.regulPressionEntree.consigne);
            checkAlarme("Alarm Pressure Desalinator Brine", desalinatorData.pressionSaumure, desalinatorParams.regulPressionSaumure.consigne);
            checkAlarme("Alarm Pressure Desalinator Fresh", desalinatorData.pressionFresh, desalinatorParams.regulPressionFresh.consigne);
            checkAlarme("Alarm Pressure Desalinator HP", desalinatorData.pressionHP, desalinatorParams.HP_threshold);

            checkAlarme("Alarm Salinity C0", salinityData.saliniteC0, ambiantConditions.salinite);
            checkAlarme("Alarm Salinity C1", salinityData.saliniteC1, salinityRegulParams.regulSaliniteC1.consigne);
            checkAlarme("Alarm Salinity C2", salinityData.saliniteC2, salinityRegulParams.regulC2_filtre.consigne);
            checkAlarme("Alarm Salinity C3", salinityData.saliniteC3, ambiantConditions.salinite);
            checkAlarme("Alarm Salinity MOI", desalinatorData.salinity, desalinatorParams.regulRebouclage.consigne);


            checkAlarme("Alarm Pressure Ambient water", ambiantConditions.pressionEA, masterParams.regulPressionEA.consigne);
            checkAlarme("AlarmPressure Hot Water", ambiantConditions.pressionEC, masterParams.regulPressionEC.consigne);


            cond = "C0";

            checkAlarme(cond + " Mixing Tank: Alarm pH", conditions[0].pH, conditions[0].rpH.consigne);




            for (int j = 0; j < 3; j++)//mesocosmes
            {
                meso = "M" + j;


                if (ambiantConditions.tide)//vanne exondation ouverte
                    checkAlarme(cond + meso + ": Alarm Low level", conditions[0].Meso[j].alarmeNiveauBas, false);
                else checkAlarme(cond + meso + ": Alarm Low Level", conditions[0].Meso[j].alarmeNiveauBas, true);



                checkAlarme(cond + meso + ": Alarm Very Low Level", conditions[0].Meso[j].alarmeNiveauTresBas, false);

                Double.TryParse(Properties.Settings.Default["FlowrateSetpoint"].ToString(), out d);

                checkAlarme(cond + meso + ": Alarm Flowrate", conditions[0].Meso[j].debit, d);
                checkAlarme(cond + meso + ": Alarm pH", conditions[0].Meso[j].pH, ambiantConditions.pH);
                checkAlarme(cond + meso + ": Alarm Temperature", conditions[0].Meso[j].temperature, ambiantConditions.temperature);

            }


            for (int i = 1; i < 4; i++) //conditions
            {
                cond = "C" + i;

                checkAlarme(cond + " Mixing Tank: Alarm pH", conditions[i].pH, conditions[i].rpH.consigne);
                checkAlarme(cond + " Mixing Tank: Alarm Temperature", conditions[i].temperature, conditions[i].rTemp.consigne);




                for (int j = 0; j < 3; j++)//mesocosmes
                {
                    meso = "M" + j;

                    checkAlarme(cond + meso + ": Alarm Overflood", conditions[i].Meso[j].alarmeNiveauHaut, false);

                    if (ambiantConditions.tide)//vanne exondation ouverte
                        checkAlarme(cond + meso + ": Alarm Low Level", conditions[i].Meso[j].alarmeNiveauBas, false);
                    else checkAlarme(cond + meso + ": Alarm Low Level", conditions[i].Meso[j].alarmeNiveauBas, true);



                    checkAlarme(cond + meso + ": Alarm Very Low Level", conditions[i].Meso[j].alarmeNiveauTresBas, false);

                    Double.TryParse(Properties.Settings.Default["FlowrateSetpoint"].ToString(), out d);

                    checkAlarme(cond + meso + ": Alarm Flowrate", conditions[i].Meso[j].debit, d);
                    checkAlarme(cond + meso + ": Alarm pH", conditions[i].Meso[j].pH, conditions[i].rpH.consigne);
                    checkAlarme(cond + meso + ": Alarm Temperature", conditions[i].Meso[j].temperature, conditions[i].rTemp.consigne);

                }
            }

            if (alarmsListWindow._lastHeaderClicked != null) alarmsListWindow.Sort(alarmsListWindow._lastHeaderClicked.Tag as string, alarmsListWindow._lastDirection);
            else alarmsListWindow.Sort("raised", alarmsListWindow._lastDirection);
        }

        public unsafe void setAlarms()
        {
            alarms.Clear();


            string cond;
            string meso;
            bool e;
            double d;

            Alarme freeze = new Alarme();
            freeze.set("Alarm Automate Salinity Freezed", true, 0, 0, TimeSpan.FromSeconds(30));
            alarms.Add(freeze);
            cond = "C0";
            Boolean.TryParse(Properties.Settings.Default["AlarmPressure"].ToString(), out e);
            Double.TryParse(Properties.Settings.Default["PressureDelta"].ToString(), out d);
            Alarme a = new Alarme();
            a.set("Alarm Pressure Ambient water", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(a);
            Alarme b = new Alarme();
            b.set("Alarm Pressure Hot water", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(b);



            Boolean.TryParse(Properties.Settings.Default["AlarmPressureSalinity"].ToString(), out e);
            Double.TryParse(Properties.Settings.Default["PressureSalinity"].ToString(), out d);

            Alarme pump = new Alarme();
            pump.set("Alarm Desalinator Pump Off", e, 0, 0, TimeSpan.FromSeconds(30));
            alarms.Add(pump);

            Alarme ps1 = new Alarme();
            ps1.set("Alarm Pressure Desalinator Inlet", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(ps1); 
            Alarme ps2 = new Alarme();
            ps2.set("Alarm Pressure Desalinator Brine", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(ps2);
            Alarme ps3 = new Alarme();
            ps3.set("Alarm Pressure Desalinator Fresh", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(ps3);

            Alarme ps4 = new Alarme();
            ps4.set("Alarm Pressure Desalinator HP", e, 0, -5, TimeSpan.FromSeconds(30));
            alarms.Add(ps4);

            Boolean.TryParse(Properties.Settings.Default["AlarmSalinity"].ToString(), out e);
            Double.TryParse(Properties.Settings.Default["SalinityAlarmValue"].ToString(), out d);

            Alarme s0 = new Alarme();
            s0.set("Alarm Salinity C0", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(s0);
            Alarme s1 = new Alarme();
            s1.set("Alarm Salinity C1", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(s1);
            Alarme s2 = new Alarme();
            s2.set("Alarm Salinity C2", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(s2);
            Alarme s3 = new Alarme();
            s3.set("Alarm Salinity C3", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(s3);
            Alarme s4 = new Alarme();
            s4.set("Alarm Salinity MOI", e, 2, d, TimeSpan.FromSeconds(30));
            alarms.Add(s4);

            for (int i = 0; i < 4; i++) //conditions
            {
                cond = "C" + i;


                Double.TryParse(Properties.Settings.Default["ConditionpHDelta"].ToString(), out d);
                Boolean.TryParse(Properties.Settings.Default["AlarmpHCondition"].ToString(), out e);
                Alarme c = new Alarme();
                c.set(cond + " Mixing Tank: Alarm pH", e, 2, d, TimeSpan.FromSeconds(30));
                alarms.Add(c);

                if (i > 0)
                {
                    Double.TryParse(Properties.Settings.Default["ConditionTempDelta"].ToString(), out d);
                    Boolean.TryParse(Properties.Settings.Default["AlarmTempCondition"].ToString(), out e);
                    Alarme f = new Alarme();
                    f.set(cond + " Mixing Tank: Alarm Temperature", e, 2, d, TimeSpan.FromSeconds(30));
                    alarms.Add(f);
                }

                for (int j = 0; j < 3; j++)//mesocosmes
                {
                    meso = "M" + j;
                    Boolean.TryParse(Properties.Settings.Default["AlarmLevelH"].ToString(), out e);
                    Alarme g = new Alarme();
                    g.set(cond + meso + ": Alarm Overflood", e, 0, 0, TimeSpan.FromSeconds(30));
                    alarms.Add(g);
                    Boolean.TryParse(Properties.Settings.Default["AlarmLevelL"].ToString(), out e);
                    Alarme h = new Alarme();
                    h.set(cond + meso + ": Alarm Low Level", e, 0, 0, TimeSpan.FromMinutes(90));
                    alarms.Add(h);
                    Boolean.TryParse(Properties.Settings.Default["AlarmLevelLL"].ToString(), out e);
                    Alarme k = new Alarme();
                    k.set(cond + meso + ": Alarm Very Low Level", e, 0, 0, TimeSpan.FromSeconds(30));
                    alarms.Add(k);

                    Double.TryParse(Properties.Settings.Default["FlowrateDelta"].ToString(), out d);
                    Boolean.TryParse(Properties.Settings.Default["AlarmFlowrate"].ToString(), out e);

                    Alarme l = new Alarme();
                    l.set(cond + meso + ": Alarm Flowrate", e, 2, d, TimeSpan.FromSeconds(30));
                    alarms.Add(l);

                    Double.TryParse(Properties.Settings.Default["MesocosmpHDelta"].ToString(), out d);
                    Boolean.TryParse(Properties.Settings.Default["AlarmpHMesocosm"].ToString(), out e);
                    Alarme m = new Alarme();
                    m.set(cond + meso + ": Alarm pH", e, 2, d, TimeSpan.FromSeconds(30));
                    alarms.Add(m);
                    Double.TryParse(Properties.Settings.Default["MesocosmTempDelta"].ToString(), out d);
                    Boolean.TryParse(Properties.Settings.Default["AlarmtempMesocosm"].ToString(), out e);
                    Alarme n = new Alarme();
                    n.set(cond + meso + ": Alarm Temperature", e, 2, d, TimeSpan.FromSeconds(30));
                    alarms.Add(n);

                }
            }
        }


        /*private async Task HandleWebSocketAsync(HttpListenerContext context)
        {

            var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            var webSocket = webSocketContext.WebSocket;
            var id = Guid.NewGuid();

            _webSockets.TryAdd(id, webSocket); // Ajouter le WebSocket à la collection

            var buffer = new byte[1024];
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        comDebugWindow.tb2.Text = message;
                        await ReadData(message, webSocket);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    }
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            finally
            {
                _webSockets.TryRemove(id, out _); // Retirer le WebSocket de la collection
            }
        }
        public async Task BroadcastMessageAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var tasks = _webSockets.Values.Select(async webSocket =>
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    Task sendTask = webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    //webSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    sendTask.GetAwaiter().GetResult();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }



*/
        public String ReadData(string data)
        {
            String s = "";
            byte[] buffer;
            if (!data.Contains("Connected"))
            {
                try
                {
                    TrameJson t = JsonConvert.DeserializeObject<TrameJson>(data);
                    if (t.sID > 5)
                    {

                    }


                    switch (t.cmd)
                    {
                        case 0://REQ PARAMS ==> send params to PLC


                            conditions[t.cID].rTemp.consigne = ambiantConditions.C0_temp + conditions[t.cID].rTemp.offset;
                            if (t.cID > 0) conditions[t.cID].rpH.consigne = ambiantConditions.C0_pH + conditions[t.cID].rpH.offset;
                            var response = new
                            {
                                cmd = 2,
                                cID = t.cID,
                                sID = 4,//Server
                                rpH = conditions[t.cID].rpH,
                                rTemp = conditions[t.cID].rTemp,
                            };

                            s = JsonConvert.SerializeObject(response);
                            break;
                        case 1://REQ DATA ==> irrelevant
                            break;
                        case 2://SEND PARAMS ==> receive params from aqua

                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    conditions[t.cID] = JsonHelper.DeserializePreservingExisting<Condition>(data, conditions[t.cID]);


                                }
                                catch (Exception ex) { }
                            });

                            var response2 = new
                            {
                                cmd = 99,
                                cID = t.cID,
                                sID = 4,//Server
                                rpH = conditions[t.cID].rpH,
                                rTemp = conditions[t.cID].rTemp,
                            };
                            s = JsonConvert.SerializeObject(response2);



                            break;
                        case 3://SEND DATA ==> receive data from aqua
                               //Aquarium a = JsonConvert.DeserializeObject<Aquarium>(data);


                            Condition c = JsonHelper.DeserializePreservingExisting<Condition>(data, new Condition());



                            Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    conditions[t.cID] = JsonHelper.DeserializePreservingExisting(data, conditions[t.cID]);
                                    lastDataReceived = DateTime.Now;

                                }
                                catch (Exception ex) { }
                            });
                            if (t.cID > 0 && t.cID <= 3)
                            {
                                double moyennepHC0 = (conditions[0].Meso[0].pH + conditions[0].Meso[1].pH + conditions[0].Meso[2].pH) / 3;
                                conditions[t.cID].rTemp.consigne = ambiantConditions.C0_temp + conditions[t.cID].rTemp.offset;
                                conditions[t.cID].rpH.consigne = moyennepHC0 + conditions[t.cID].rpH.offset;
                                var respons = new
                                {
                                    cmd = 2,
                                    cID = t.cID,
                                    sID = 4,//Server
                                    rpH = conditions[t.cID].rpH,
                                    rTemp = conditions[t.cID].rTemp,
                                };

                                s = JsonConvert.SerializeObject(respons);
                            }
                            //calibrationWindow.RefreshMeasure();


                            break;
                        case 4://CALIBRATE SENSOR  ==> irrelevant
                            break;

                        case 5://REQ_MASTER_DATA

                            break;
                        case 6://SEND_MASTER_DATA
                            ambiantConditions = JsonHelper.DeserializePreservingExisting<Ambiant>(data, ambiantConditions);
                            double slope, offset;
                            Double.TryParse(Properties.Settings.Default["FluoOffset"].ToString(), out offset);
                            Double.TryParse(Properties.Settings.Default["FluoSlope"].ToString(), out slope);
                            ambiantConditions.fluo = ambiantConditions.fluo * slope + offset;
                            lastDataReceived = DateTime.Now;

                            break;
                        case 7://REQ_MASTER_PARAMS
                            var response3 = new
                            {
                                cmd = 8,
                                cID = t.cID,
                                sID = 4,//Server
                                masterParams
                            };

                            s = JsonConvert.SerializeObject(response3);

                            break;
                        case 8://SEND_MASTER_PARAMS
                            masterParams = JsonHelper.DeserializePreservingExisting<MasterParams>(data, masterParams);
                            break;
                        case 9://SEND_PAC_PARAMS

                            pacParams = JsonHelper.DeserializePreservingExisting<PACParams>(data, pacParams);
                            break;
                        case 10://REQ_PAC_PARAMS
                            var response4 = new
                            {
                                cmd = 10,
                                cID = t.cID,
                                sID = 4,//Server
                                pacParams
                            };

                            s = JsonConvert.SerializeObject(response4);

                            break;

                        // ========== NOUVEAUX PROTOCOLES MITIC ==========

                        case 11: // REQ_DESALINATOR_DATA
                            // Pas de réponse nécessaire, Desalinator envoie les données périodiquement
                            break;

                        case 12: // SEND_DESALINATOR_DATA
                            bool prevPompeHPStatus = desalinatorData.pompeHP;
                            desalinatorData = JsonHelper.DeserializePreservingExisting<DesalinatorData>(data, desalinatorData);
                            desalinatorData.lastUpdated = DateTime.Now;
                            desalinatorParams.regulRebouclage.sortiePID_pc = desalinatorData.v3VMOI;
                            desalinatorParams.regulPressionSaumure.sortiePID_pc = desalinatorData.vanneSaumure;
                            desalinatorParams.regulPressionFresh.sortiePID_pc = desalinatorData.vanneFresh;
                            desalinatorParams.regulPressionEntree.sortiePID_pc = desalinatorData.vanneEntree;
                            lastDataReceived = DateTime.Now;

                            //if pump was on and it is now off, force regulSalinite Valves
                            if(prevPompeHPStatus && prevPompeHPStatus != desalinatorData.pompeHP)
                            {
                                salinityRegulParams.regulSaliniteC0.autorisationForcage = true;
                                salinityRegulParams.regulSaliniteC1.autorisationForcage = true;
                                salinityRegulParams.regulSaliniteC2.autorisationForcage = true;
                                salinityRegulParams.regulSaliniteC3.autorisationForcage = true;
                                salinityRegulParams.regulC2_filtre.autorisationForcage = true;

                                salinityRegulParams.regulSaliniteC0.consigneForcage = 100;
                                salinityRegulParams.regulSaliniteC1.consigneForcage = 0;
                                salinityRegulParams.regulSaliniteC2.consigneForcage = 100;
                                salinityRegulParams.regulSaliniteC3.consigneForcage = 100;
                                salinityRegulParams.regulC2_filtre.consigneForcage = 100;
                                var responseForceVannes = new
                                {
                                    cmd = 18,
                                    cID = t.cID,
                                    sID = 4,//Server
                                    regulRebouclage = desalinatorParams.regulRebouclage,
                                    regulPressionEntree = desalinatorParams.regulPressionEntree,
                                    regulPressionSaumure = desalinatorParams.regulPressionSaumure,
                                    regulPressionFresh = desalinatorParams.regulPressionFresh
                                };
                                s = JsonConvert.SerializeObject(responseForceVannes);
                            }
                            break;

                        case 13: // REQ_DESALINATOR_PARAMS
                            var response6 = new
                            {
                                cmd = 14,
                                cID = t.cID,
                                sID = 4,//Server
                                regulRebouclage = desalinatorParams.regulRebouclage,
                                regulPressionEntree = desalinatorParams.regulPressionEntree,
                                regulPressionSaumure = desalinatorParams.regulPressionSaumure,
                                regulPressionFresh = desalinatorParams.regulPressionFresh
                            };
                            s = JsonConvert.SerializeObject(response6);
                            break;

                        case 14: // SEND_DESALINATOR_PARAMS
                            desalinatorParams = JsonHelper.DeserializePreservingExisting<DesalinatorParams>(data, desalinatorParams);

                            // Mise à jour fenêtre Desalinator si ouverte
                            if (desalinatorWindow.IsVisible)
                            {
                                desalinatorWindow.RefreshData();
                            }
                            break;

                        case 15: // REQ_SALINITY_DATA
                            // Pas de réponse nécessaire, MITIC_RegulSalinite envoie les données périodiquement
                            break;

                        case 16: // SEND_SALINITY_DATA
                            salinityData = JsonHelper.DeserializePreservingExisting<SalinityData>(data, salinityData);
                            salinityData.lastUpdated = DateTime.Now;
                            lastDataReceived = DateTime.Now;

                            // Update Salinity window if open
                            if (salinityWindow != null && salinityWindow.IsVisible)
                            {
                                salinityWindow.RefreshData();
                            }
                            break;

                        case 17: // REQ_SALINITY_PARAMS
                            var response5 = new
                            {
                                cmd = 18,
                                cID = t.cID,
                                sID = 4,//Server
                                time = DateTime.Now,
                                regulC0 = salinityRegulParams?.regulSaliniteC0,
                                regulC1 = salinityRegulParams?.regulSaliniteC1,
                                regulC2 = salinityRegulParams?.regulSaliniteC2,
                                regulC3 = salinityRegulParams?.regulSaliniteC3,
                                regulC2_filtre = salinityRegulParams?.regulC2_filtre
                            };
                            s = JsonConvert.SerializeObject(response5);
                            break;

                        case 18: // SEND_SALINITY_PARAMS
                            salinityRegulParams = JsonHelper.DeserializePreservingExisting<SalinityRegulParams>(data, salinityRegulParams);

                            // Update Salinity window if open
                            if (salinityWindow != null && salinityWindow.IsVisible)
                            {
                                salinityWindow.RefreshData();
                            }
                            break;

                        case 19: // SEND_SALINITY_RATIOS
                            // Réception des ratios (pas utilisé pour l'instant)
                            break;

                        case 20: // SEND_CALIB_ACK
                            calibrationWindow.calibOK = true;
                            break;

                        case 21: // REQ_SALINITY_FACTORS
                            // Demande des facteurs correctifs de salinité
                            var response7 = new
                            {
                                cmd = 22,
                                cID = t.cID,
                                sID = 4, // Server
                                factorControl = salinityFactors.factorControl,
                                factorC0 = salinityFactors.factorC0,
                                factorC1 = salinityFactors.factorC1,
                                factorC2 = salinityFactors.factorC2,
                                factorC3 = salinityFactors.factorC3,
                                factorDesalinator = salinityFactors.factorDesalinator
                            };
                            s = JsonConvert.SerializeObject(response7);
                            break;

                        case 22: // SEND_SALINITY_FACTORS
                            // Réception des facteurs correctifs de salinité
                            salinityFactors = JsonHelper.DeserializePreservingExisting<SalinityFactors>(data, salinityFactors);

                            // Mise à jour fenêtre ReferenceCTD si ouverte
                            if (referenceCTDWindow != null && referenceCTDWindow.IsVisible)
                            {
                                // La fenêtre se rafraîchit automatiquement
                            }
                            break;
                    }
                    DisplayData(t.cmd);
                    return s;
                }
                catch (Exception e)
                {

                }
            }
            else //"Connected"
            {
                Dispatcher.Invoke(() =>
                {
                    for (int i = 0; i < 11; i++) expSettingsWindow.load(i);
                    expSettingsWindow.refreshParams();
                });
                //string message = "{\"cmd\":0}";
                return "";
            }
            return "";

        }

        /* private void sendRequest()
         {
             string msg = "";

             var Timestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
             if (conditions[0].rpH.consigne == 0)
             {
                 for (int i = 0; i < 8; i++) expSettingsWindow.load(i);
             }
             else
             {
                 switch (step)
                 {
                     case 0:
                         msg = "{\"cmd\":1,\"cID\":0,\"sID\":4}";
                         break;
                     case 1:
                         msg = "{\"cmd\":1,\"cID\":1,\"sID\":4}";
                         break;
                     case 2:
                         msg = "{\"cmd\":1,\"cID\":2,\"sID\":4}";
                         break;
                     case 3:
                         msg = "{\"cmd\":1,\"cID\":3,\"sID\":4}";
                         break;
                     case 4:
                         //msg = "{\"cmd\":5,\"cID\":0,\"sID\":4,\"time\":" + Timestamp + "}";
                         msg = "{\"cmd\":5,\"cID\":0,\"sID\":4}";
                         break;
                 }
                 if (step < 4) step++; else step = 0;

                 comDebugWindow.tb1.Text = msg;

                 Task<string> t2 = Send(ws, msg, comDebugWindow.tb2);
                 t2.Wait(20);
             }

         }*/




        /*
        private static async Task<string> Send(ClientWebSocket ws, string msg, TextBox tb)
        {

            var timeOut = new CancellationTokenSource(500).Token;
            if (ws.State == WebSocketState.Open)
            {
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(
                    Encoding.UTF8.GetBytes(msg));
                await ws.SendAsync(
                    bytesToSend, WebSocketMessageType.Text,
                    true, timeOut);

                ArraySegment<byte> bytesReceived = new ArraySegment<byte>(new byte[1024]);
                WebSocketReceiveResult result = ws.ReceiveAsync(
                    bytesReceived, timeOut).Result;
                string data = Encoding.UTF8.GetString(bytesReceived.Array, 0, result.Count);l
                tb.Text = data;
                return data;
            }
            return null;

        }*/



        public void DisplayData(int command)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {

                    statusLabel1.Text = "Last updated: " + DateTime.Now + " UTC";
                    label_time.Content = lastDataReceived.ToUniversalTime();
                    Cleanup_btn.Header = cleanupMode ? "Exit Cleanup Mode" : "Start Cleanup Mode";

                    statusLabel2.Text = cleanupMode ? "Cleanup Mode Active" : "";
                    statusLabel2.Foreground = cleanupMode ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Black);


                    if (expSettingsWindow.ShowActivated)
                    {
                        int selctedcondID = expSettingsWindow.comboBox_Condition.SelectedIndex;
                        if (selctedcondID == 4)
                        {
                            expSettingsWindow.tb_pH_measure.Text = ambiantConditions.pressionEA.ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = ambiantConditions.sortiePID_EA.ToString("F2");
                            expSettingsWindow.tb_Temp_measure.Text = ambiantConditions.pressionEC.ToString("F2");
                            expSettingsWindow.tb_Temp_PIDoutput.Text = ambiantConditions.sortiePID_EC.ToString("F2");
                        }
                        else
                        if (selctedcondID == 5)
                        {
                            expSettingsWindow.tb_pH_measure.Text = ambiantConditions.tempPAC.ToString("F2");
                            expSettingsWindow.tb_pH_setPoint.Text = (ambiantConditions.temperature + pacParams.rTempEC.offset).ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = ambiantConditions.sortiePID_TEC.ToString("F2");
                        }
                        else if (selctedcondID == 6)
                        {
                            expSettingsWindow.tb_pH_measure.Text = desalinatorData.pressionEntree.ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = desalinatorParams.regulPressionEntree.sortiePID_pc.ToString("F2");
                            expSettingsWindow.tb_Temp_measure.Text = desalinatorData.salinity.ToString("F2");
                            expSettingsWindow.tb_Temp_PIDoutput.Text = desalinatorParams.regulRebouclage.sortiePID_pc.ToString("F2");
                        }
                        else if (selctedcondID == 7)
                        {
                            expSettingsWindow.tb_pH_measure.Text = desalinatorData.pressionFresh.ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = desalinatorParams.regulPressionFresh.sortiePID_pc.ToString("F2");
                            expSettingsWindow.tb_Temp_measure.Text = desalinatorData.pressionSaumure.ToString("F2");
                            expSettingsWindow.tb_Temp_PIDoutput.Text = desalinatorParams.regulPressionSaumure.sortiePID_pc.ToString("F2");
                        }
                        else if (selctedcondID == 8)
                        {
                            expSettingsWindow.tb_pH_setPoint.Text = (ratioC1*100).ToString("F2");

                            expSettingsWindow.tb_pH_measure.Text = (ratioC0*100).ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = salinityData.vanneC0.ToString("F2");
                            expSettingsWindow.tb_Temp_measure.Text = salinityData.saliniteC1.ToString("F2");
                            expSettingsWindow.tb_Temp_PIDoutput.Text = salinityData.vanneC1.ToString("F2");
                        }
                        else if (selctedcondID == 9)
                        {
                            expSettingsWindow.tb_pH_setPoint.Text = (ratioC1 * 100).ToString("F2");

                            expSettingsWindow.tb_pH_measure.Text = (ratioC2 * 100).ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = salinityData.vanneC2.ToString("F2");
                            expSettingsWindow.tb_Temp_measure.Text = salinityData.saliniteC2.ToString("F2");
                            expSettingsWindow.tb_Temp_PIDoutput.Text = salinityData.vanneC2_filtre.ToString("F2");
                        }
                        else if (selctedcondID == 10)
                        {
                            expSettingsWindow.tb_pH_setPoint.Text = (ratioC1 * 100).ToString("F2");

                            expSettingsWindow.tb_pH_measure.Text = (ratioC3 * 100).ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = salinityData.vanneC3.ToString("F2");
                           }
                        else
                        {
                            if (selctedcondID < 0 || selctedcondID > 10) selctedcondID = 0;
                            if (selctedcondID == 0) expSettingsWindow.tb_pH_measure.Text = conditions[0].pH.ToString("F2");
                            else expSettingsWindow.tb_pH_measure.Text = ((conditions[selctedcondID].Meso[0].pH + conditions[selctedcondID].Meso[1].pH + conditions[selctedcondID].Meso[2].pH) / 3).ToString("F2");
                            expSettingsWindow.tb_pH_PIDoutput.Text = conditions[selctedcondID].rpH.sortiePID_pc.ToString("F2");
                            expSettingsWindow.tb_Temp_measure.Text = conditions[selctedcondID].temperature.ToString("F2");
                            expSettingsWindow.tb_Temp_PIDoutput.Text = conditions[selctedcondID].rTemp.sortiePID_pc.ToString("F2");
                        }
                    }


                    //expSettingsWindow.tb_pH_setPoint.Text = conditions[0].rpH.consigne.ToString();
                    /*if (command == 2)//PARAMS
                    {

                    }
                    else */
                    if (command == 3 || command == 2)//DATA
                    {
                        label_C0M1_Alarm.Content = "";
                        label_C0M2_Alarm.Content = "";
                        label_C0M3_Alarm.Content = "";
                        label_C1M1_Alarm.Content = "";
                        label_C1M2_Alarm.Content = "";
                        label_C1M3_Alarm.Content = "";
                        label_C2M1_Alarm.Content = "";
                        label_C2M2_Alarm.Content = "";
                        label_C2M3_Alarm.Content = "";
                        label_C3M1_Alarm.Content = "";
                        label_C3M2_Alarm.Content = "";
                        label_C3M3_Alarm.Content = "";
                        if (conditions[0].Meso[0].alarmeNiveauBas) label_C0M1_Alarm.Content = "Alarm: Low level";
                        else
                        {
                            if (conditions[0].Meso[0].alarmeNiveauTresBas) label_C0M1_Alarm.Content = "Alarm: Very Low level";
                            else label_C0M1_Alarm.Content = "";
                        }


                        if (conditions[0].Meso[1].alarmeNiveauBas) label_C0M2_Alarm.Content = "Alarm: Low level";
                        else label_C0M2_Alarm.Content = !conditions[0].Meso[1].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";


                        if (conditions[0].Meso[2].alarmeNiveauBas) label_C0M3_Alarm.Content = "Alarm: Low level";
                        else label_C0M3_Alarm.Content = !conditions[0].Meso[2].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";


                        if (conditions[1].Meso[0].alarmeNiveauHaut)
                        {
                            label_C1M1_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (conditions[1].Meso[0].alarmeNiveauBas) label_C1M1_Alarm.Content = "Alarm: Low level";
                            else label_C1M1_Alarm.Content = !conditions[1].Meso[0].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }

                        if (conditions[1].Meso[1].alarmeNiveauHaut)
                        {
                            label_C1M2_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[1].Meso[1].alarmeNiveauBas) label_C1M2_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[1].Meso[1].alarmeNiveauBas) label_C1M2_Alarm.Content = "Alarm: Low level";
                            else label_C1M2_Alarm.Content = !conditions[1].Meso[1].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }
                        if (conditions[1].Meso[2].alarmeNiveauHaut)
                        {
                            label_C1M3_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[1].Meso[2].alarmeNiveauBas) label_C1M3_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[1].Meso[2].alarmeNiveauBas) label_C1M3_Alarm.Content = "Alarm: Low level";
                            else label_C1M3_Alarm.Content = !conditions[1].Meso[2].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }
                        if (conditions[2].Meso[0].alarmeNiveauHaut)
                        {
                            label_C2M1_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[2].Meso[0].alarmeNiveauBas) label_C2M1_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[2].Meso[0].alarmeNiveauBas) label_C2M1_Alarm.Content = "Alarm: Low level";
                            else label_C2M1_Alarm.Content = !conditions[2].Meso[0].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }
                        if (conditions[2].Meso[1].alarmeNiveauHaut)
                        {
                            label_C2M2_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[2].Meso[1].alarmeNiveauBas) label_C2M2_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[2].Meso[1].alarmeNiveauBas) label_C2M2_Alarm.Content = "Alarm: Low level";
                            else
                                label_C2M2_Alarm.Content = !conditions[2].Meso[1].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }
                        if (conditions[2].Meso[2].alarmeNiveauHaut)
                        {
                            label_C2M3_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[2].Meso[2].alarmeNiveauBas) label_C2M3_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[2].Meso[2].alarmeNiveauBas) label_C2M3_Alarm.Content = "Alarm: Low level";
                            else label_C2M3_Alarm.Content = !conditions[2].Meso[2].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }

                        if (conditions[3].Meso[0].alarmeNiveauHaut)
                        {
                            label_C3M1_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[3].Meso[0].alarmeNiveauBas) label_C3M1_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[3].Meso[0].alarmeNiveauBas) label_C3M1_Alarm.Content = "Alarm: Low level";
                            else label_C3M1_Alarm.Content = !conditions[3].Meso[0].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }
                        if (conditions[3].Meso[1].alarmeNiveauHaut)
                        {
                            label_C3M2_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[3].Meso[1].alarmeNiveauBas) label_C3M2_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[3].Meso[1].alarmeNiveauBas) label_C3M2_Alarm.Content = "Alarm: Low level";
                            else label_C3M2_Alarm.Content = !conditions[3].Meso[1].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }
                        if (conditions[3].Meso[2].alarmeNiveauHaut)
                        {
                            label_C3M3_Alarm.Content = "Alarm: Overflow";
                        }
                        else
                        {
                            if (ambiantConditions.tide)//vanne exondation ouverte
                            {
                                if (conditions[3].Meso[2].alarmeNiveauBas) label_C3M3_Alarm.Content = "Alarm: Exondation not effective";
                            }
                            else if (conditions[3].Meso[2].alarmeNiveauBas) label_C3M3_Alarm.Content = "Alarm: Low level";
                            else label_C3M3_Alarm.Content = !conditions[3].Meso[2].alarmeNiveauTresBas ? "" : "Alarm: Very Low level";
                        }

                        label_EC_temperature_setpoint.Content = string.Format(ci, "Temperature setpoint: \t{0:0.00}°C", pacParams.rTempEC.consigne);

                        label_EA_pressure_measure.Content = string.Format(ci, "Pressure measure: {0:0.00} bars", ambiantConditions.pressionEA);
                        label_EA_pressure_setpoint.Content = string.Format(ci, "Pressure setpoint: {0:0.00} bars", masterParams.regulPressionEA.consigne);
                        label_EA_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", ambiantConditions.sortiePID_EA);
                        label_EC_pressure_measure.Content = string.Format(ci, "Pressure measure: {0:0.00} bars", ambiantConditions.pressionEC);
                        label_EC_pressure_setpoint.Content = string.Format(ci, "Pressure setpoint: {0:0.00} bars", masterParams.regulPressionEC.consigne);
                        label_EC_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", ambiantConditions.sortiePID_EC);

                        label_C0_pH_setpoint.Content = string.Format(ci, "pH setpoint: {0:0.00}", conditions[0].rpH.consigne);
                        label_C0_pH_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[0].rpH.sortiePID_pc);

                        label_C1_pH_setpoint.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[1].rpH.consigne);
                        label_C1_pH_sortiePID.Content = string.Format(ci, "Pump: \t{0:0}%", conditions[1].rpH.sortiePID_pc);
                        label_C1_Temp_setpoint.Content = string.Format(ci, "T°C: \t{0:0.00}", conditions[1].rTemp.consigne);
                        label_C1_Temp_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[1].rTemp.sortiePID_pc);

                        label_C2_pH_setpoint.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[2].rpH.consigne);
                        label_C2_pH_sortiePID.Content = string.Format(ci, "Pump: \t{0:0}%", conditions[2].rpH.sortiePID_pc);
                        label_C2_Temp_setpoint.Content = string.Format(ci, "T°C: \t{0:0.00}", conditions[2].rTemp.consigne);
                        label_C2_Temp_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[2].rTemp.sortiePID_pc);

                        label_C3_pH_setpoint.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[3].rpH.consigne);
                        label_C3_pH_sortiePID.Content = string.Format(ci, "Pump: \t{0:0}%", conditions[3].rpH.sortiePID_pc);
                        label_C3_Temp_setpoint.Content = string.Format(ci, "T°C: \t{0:0.00}", conditions[3].rTemp.consigne);
                        label_C3_Temp_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[3].rTemp.sortiePID_pc);

                        if (conditions[0].rpH.autorisationForcage)
                        {
                            label_C0_pH_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[0].rpH.consigneForcage);
                            label_C0_pH_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C0_pH_sortiePID.Foreground = Brushes.Black;
                        if (masterParams.regulPressionEA.autorisationForcage)
                        {
                            label_EA_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", masterParams.regulPressionEA.consigneForcage);
                            label_EA_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_EA_sortiePID.Foreground = Brushes.Black;

                        if (masterParams.regulPressionEC.autorisationForcage)
                        {
                            label_EC_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", masterParams.regulPressionEC.consigneForcage);
                            label_EC_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_EC_sortiePID.Foreground = Brushes.Black;

                        if (conditions[1].rpH.autorisationForcage)
                        {
                            label_C1_pH_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[1].rpH.consigneForcage);
                            label_C1_pH_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C1_pH_sortiePID.Foreground = Brushes.Black;
                        if (conditions[1].rTemp.autorisationForcage)
                        {
                            label_C1_Temp_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[1].rTemp.consigneForcage);
                            label_C1_Temp_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C1_Temp_sortiePID.Foreground = Brushes.Black;

                        if (conditions[2].rpH.autorisationForcage)
                        {
                            label_C2_pH_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[2].rpH.consigneForcage);
                            label_C2_pH_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C2_pH_sortiePID.Foreground = Brushes.Black;
                        if (conditions[2].rTemp.autorisationForcage)
                        {
                            label_C2_Temp_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[2].rTemp.consigneForcage);
                            label_C2_Temp_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C2_Temp_sortiePID.Foreground = Brushes.Black;

                        if (conditions[3].rpH.autorisationForcage)
                        {
                            label_C3_pH_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[3].rpH.consigneForcage);
                            label_C3_pH_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C3_pH_sortiePID.Foreground = Brushes.Black;
                        if (conditions[3].rTemp.autorisationForcage)
                        {
                            label_C3_Temp_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", conditions[3].rTemp.consigneForcage);
                            label_C3_Temp_sortiePID.Foreground = Brushes.Red;
                        }
                        else label_C3_Temp_sortiePID.Foreground = Brushes.Black;


                        label_C0_pH_CO2.Content = string.Format(ci, "pH measure: {0:0.00}", conditions[0].pH);
                        //label_C0_pH_CO2.Content = string.Format(ci, "pH measure: {0:0.00}", expSettingsWindow.l);
                        // label_C0_Temp.Content = string.Format(ci, "T°C: {0:0.00}°C", conditions[0].temperature);
                        label_C0_Salinity_Tank.Content = string.Format(ci, "Salinity: {0:0.00}", salinityData.saliniteC0);
                        label_C1_pH.Content = string.Format(ci, "pH: {0:0.00}", conditions[1].pH);
                        label_C1_Temp.Content = string.Format(ci, "T°C: {0:0.00}°C", conditions[1].temperature);
                        label_C1_Salinity.Content = string.Format(ci, "Salinity: {0:0.00}", salinityData.saliniteC1);
                        label_C2_pH.Content = string.Format(ci, "pH: {0:0.00}", conditions[2].pH);
                        label_C2_Temp.Content = string.Format(ci, "T°C: {0:0.00}°C", conditions[2].temperature);
                        label_C2_Salinity.Content = string.Format(ci, "Salinity: {0:0.00}", salinityData.saliniteC2);
                        label_C3_pH.Content = string.Format(ci, "pH: {0:0.00}", conditions[3].pH);
                        label_C3_Temp.Content = string.Format(ci, "T°C: {0:0.00}°C", conditions[3].temperature);
                        label_C3_Salinity.Content = string.Format(ci, "Salinity: {0:0.00}", salinityData.saliniteC3);

                        label_C0M1_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[0].Meso[0].debit);
                        label_C0M2_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[0].Meso[1].debit);
                        label_C0M3_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[0].Meso[2].debit);

                        // Affichage salinité des mésocosmes C0 (utilisation CTD pour référence)
                        label_C0M1_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[0].Meso[0].pH);
                        label_C0M2_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[0].Meso[1].pH);
                        label_C0M3_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[0].Meso[2].pH);
                        label_C0M1_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[0].Meso[0].temperature);
                        label_C0M2_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[0].Meso[1].temperature);
                        label_C0M3_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[0].Meso[2].temperature);

                        label_C1M1_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[1].Meso[0].debit);
                        label_C1M2_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[1].Meso[1].debit);
                        label_C1M3_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[1].Meso[2].debit);

                        // Affichage salinité des mésocosmes C1 (utilisation conductivité C0)
                        label_C1M1_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[1].Meso[0].pH);
                        label_C1M2_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[1].Meso[1].pH);
                        label_C1M3_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[1].Meso[2].pH);
                        label_C1M1_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[1].Meso[0].temperature);
                        label_C1M2_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[1].Meso[1].temperature);
                        label_C1M3_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[1].Meso[2].temperature);

                        label_C2M1_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[2].Meso[0].debit);
                        label_C2M2_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[2].Meso[1].debit);
                        label_C2M3_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[2].Meso[2].debit);

                        // Affichage salinité des mésocosmes C2 (utilisation conductivité C1)
                        label_C2M1_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[2].Meso[0].pH);
                        label_C2M2_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[2].Meso[1].pH);
                        label_C2M3_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[2].Meso[2].pH);
                        label_C2M1_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[2].Meso[0].temperature);
                        label_C2M2_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[2].Meso[1].temperature);
                        label_C2M3_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[2].Meso[2].temperature);

                        label_C3M1_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[3].Meso[0].debit);
                        label_C3M2_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[3].Meso[1].debit);
                        label_C3M3_Flowrate.Content = string.Format(ci, "Flowrate: {0:0.00}l/mn", conditions[3].Meso[2].debit);

                        // Affichage salinité des mésocosmes C3 (utilisation conductivité C2)
                        label_C3M1_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[3].Meso[0].pH);
                        label_C3M2_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[3].Meso[1].pH);
                        label_C3M3_pH.Content = string.Format(ci, "pH: \t{0:0.00}", conditions[3].Meso[2].pH);
                        label_C3M1_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[3].Meso[0].temperature);
                        label_C3M2_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[3].Meso[1].temperature);
                        label_C3M3_Temp.Content = string.Format(ci, "T°C: \t{0:0.00}°C", conditions[3].Meso[2].temperature);
                    }
                    else if (command == 6)//MASTER DATA
                    {
                        label_C0_Salinity.Content = string.Format(ci, "Salinity:\t{0:0.00}", salinityData.saliniteControl);
                        label_C0_CTD_Salinity.Content = string.Format(ci, "CTD Salinity:\t{0:0.00}", salinityData.CTD_PSU);
                        label_C0_CTD_Temperature.Content = string.Format(ci, "CTD Temperature:\t{0:0.00} °C", salinityData.CTD_Temperature);
                        label_C0_Temp.Content = string.Format(ci, "Temp.:\t{0:0.00}°C", ambiantConditions.temperature);
                        label_C0_pH.Content = string.Format(ci, "pH:\t{0:0.00}", ambiantConditions.pH);
                        label_C0_mixing_tank_Temp.Content = string.Format(ci, "T°C: {0:0.00}°C", ambiantConditions.C0_temp);
                        label_C0_mixing_tank_pH.Content = string.Format(ci, "pH: {0:0.00}", ambiantConditions.C0_pH);
                        label_CTD_O2.Content = string.Format(ci, "CTD O2:\t\t{0:0.00}", salinityData.CTD_Oxygen);
                        label_CTD_PSU.Content = string.Format(ci, "CTD calc PSU:\t{0:0.00}", salinityData.CTD_CalculatedPSU);
                        if (ambiantConditions.sun) label_ledstate.Content = string.Format(ci, "LED state: ON (Day)");
                        else label_ledstate.Content = string.Format(ci, "LED state: OFF (Night)");

                        label_EC_temperature_measure.Content = string.Format(ci, "Temperature measure: \t{0:0.00}°C", ambiantConditions.tempPAC);
                        if (pacParams.rTempEC.autorisationForcage)
                        {
                            label_TEC_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", pacParams.rTempEC.consigneForcage);
                            label_TEC_sortiePID.Foreground = Brushes.Red;
                        }
                        else
                        {
                            label_TEC_sortiePID.Content = string.Format(ci, "Valve: \t{0:0}%", ambiantConditions.sortiePID_TEC);
                            label_TEC_sortiePID.Foreground = Brushes.Black;
                        }

                        DateTime dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ambiantConditions.nextSunDown).ToUniversalTime();
                        label_nextSunDown.Content = "Sunset time: " + dt.ToString();
                        dt = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc).AddSeconds(ambiantConditions.nextSunUp).ToUniversalTime();
                        label_nextSunUp.Content = "Sunrise time: " + dt.ToString();
                    }
                    else if (command == 12) // DESALINATOR DATA
                    {
                        // Mise à jour debug console si ouverte
                        if (comDebugWindow.ShowActivated)
                        {
                            comDebugWindow.tb2.Text += "\n" + DateTime.Now.ToString() + " - Desalinator Data Received:";
                            comDebugWindow.tb2.Text += "\n  Pression HP: " + desalinatorData.pressionHP.ToString("F2") + " bars";
                            comDebugWindow.tb2.Text += "\n  Pompe HP: " + (desalinatorData.pompeHP ? "ON" : "OFF");
                            comDebugWindow.tb2.Text += "\n  Débit Fresh: " + desalinatorData.debitFresh.ToString("F2") + " L/min";
                        }

                        // Mise à jour fenêtre Desalinator si ouverte
                        if (desalinatorWindow.IsVisible)
                        {
                            desalinatorWindow.RefreshData();
                        }
                    }
                    else if (command == 16) // SALINITY DATA
                    {

                        // Calcul du débit total C1 (salinité) : somme des débits des 3 mésocosmes C1
                        double totalFlowrateC0Meso = conditions[0].Meso[0].debit + conditions[0].Meso[1].debit + conditions[0].Meso[2].debit;
                        double totalFlowrateC1Meso = conditions[1].Meso[0].debit + conditions[1].Meso[1].debit + conditions[1].Meso[2].debit;
                        double totalFlowrateC2Meso = conditions[2].Meso[0].debit + conditions[2].Meso[1].debit + conditions[2].Meso[2].debit;
                        double totalFlowrateC3Meso = conditions[3].Meso[0].debit + conditions[3].Meso[1].debit + conditions[3].Meso[2].debit;

                        // Calcul des ratios : débit salinité / somme des débits des mésocosmes
                        ratioC0 = 0;
                        ratioC1 = 0;
                        ratioC2 = 0;
                        ratioC3 = 0;

                        if (totalFlowrateC1Meso > 0)
                        {
                            ratioC0 = salinityData.debitC0 / totalFlowrateC0Meso;
                            ratioC1 = salinityData.debitC1 / totalFlowrateC1Meso;
                            ratioC2 = salinityData.debitC2 / totalFlowrateC2Meso;
                            ratioC3 = salinityData.debitC3 / totalFlowrateC3Meso;
                        }

                        // Mise à jour des labels V3V de salinité
                        label_Salinity_V3V_C0.Content = string.Format(ci, "V3V: {0:0}%", salinityData.vanneC0);
                        label_Salinity_Setpoint_C0.Content = string.Format(ci, "Setpoint: {0:0.00} %", ratioC1 * 100);
                        label_Salinity_Flowrate_C0.Content = string.Format(ci, "Flowrate: {0:0.00} L/min", salinityData.debitC0);
                        label_Salinity_Ratio_C0.Content = string.Format(ci, "Ratio: {0:0.00}%", ratioC0 * 100);

                        label_Salinity_V3V_C1.Content = string.Format(ci, "V3V: {0:0}%", salinityData.vanneC1);
                        label_Salinity_Setpoint_C1.Content = string.Format(ci, "Setpoint: {0:0.00}", salinityRegulParams.regulSaliniteC1.consigne);
                        label_Salinity_Flowrate_C1.Content = string.Format(ci, "Flowrate: {0:0.00} L/min", salinityData.debitC1);
                        label_Salinity_Ratio_C1.Content = string.Format(ci, "Ratio: {0:0.00}%", ratioC1 * 100);

                        label_Salinity_V3V_C2.Content = string.Format(ci, "V3V: {0:0}%", salinityData.vanneC2);
                        label_Salinity_Setpoint_C2.Content = string.Format(ci, "Setpoint: {0:0.00} %", ratioC1 * 100);
                        label_Salinity_Flowrate_C2.Content = string.Format(ci, "Flowrate: {0:0.00} L/min", salinityData.debitC2);
                        label_Salinity_Ratio_C2.Content = string.Format(ci, "Ratio: {0:0.00}%", ratioC2 * 100);

                        label_Salinity_V3V_C2_filtre.Content = string.Format(ci, "V3V filtre: {0:0}%", salinityData.vanneC2_filtre);
                        label_Salinity_Setpoint_C2_filtre.Content = string.Format(ci, "Setpoint: {0:0.00}", salinityRegulParams.regulC2_filtre.consigne);

                        label_Salinity_V3V_C3.Content = string.Format(ci, "V3V: {0:0}%", salinityData.vanneC3);
                        label_Salinity_Setpoint_C3.Content = string.Format(ci, "Setpoint: {0:0.00} %", ratioC1 * 100);
                        label_Salinity_Flowrate_C3.Content = string.Format(ci, "Flowrate: {0:0.00} L/min", salinityData.debitC3);
                        label_Salinity_Ratio_C3.Content = string.Format(ci, "Ratio: {0:0.00}%", ratioC3 * 100);

                        // Envoi des ratios à l'automate MITIC_RegulSalinite
                        var ratiosResponse = new
                        {
                            cmd = 19, // SEND_SALINITY_RATIOS
                            cID = 8,  // PLCID de MITIC_RegulSalinite
                            sID = 4,  // Server
                            ratioC0 = Math.Round(ratioC0,2),
                            ratioC1 = Math.Round(ratioC1,2),
                            ratioC2 = Math.Round(ratioC2,2),
                            ratioC3 = Math.Round(ratioC3,2)
                        };
                        string ratiosJson = JsonConvert.SerializeObject(ratiosResponse);
                        SendWebSocketCommand(ratiosJson);

                        // Mise à jour debug console si ouverte
                        if (comDebugWindow.ShowActivated)
                        {
                            comDebugWindow.tb2.Text += "\n" + DateTime.Now.ToString() + " - Salinity Data Received:";
                            comDebugWindow.tb2.Text += "\n  Conductivité C0: " + salinityData.conductiviteC0.ToString("F2") + " mS/cm";
                            comDebugWindow.tb2.Text += "\n  Conductivité C1: " + salinityData.conductiviteC1.ToString("F2") + " mS/cm";
                            comDebugWindow.tb2.Text += "\n  Conductivité C2: " + salinityData.conductiviteC2.ToString("F2") + " mS/cm";
                            comDebugWindow.tb2.Text += "\n  Conductivité C3: " + salinityData.conductiviteC3.ToString("F2") + " mS/cm";
                            comDebugWindow.tb2.Text += "\n  CTD Température: " + salinityData.CTD_Temperature.ToString("F2") + " °C";
                            comDebugWindow.tb2.Text += "\n  CTD Salinité: " + salinityData.CTD_PSU.ToString("F2");
                            comDebugWindow.tb2.Text += "\n  Consignes: ";

                            // Afficher les données CTD brutes si disponibles
                            if (!string.IsNullOrEmpty(salinityData.CTD_RawData))
                            {
                                comDebugWindow.tb2.Text += "\n  CTD Raw Data: " + salinityData.CTD_RawData;
                            }
                        }
                    }
                }catch(Exception e)
                {
                    MessageBox.Show("There was a problem updating the interface: " + e.Message);
                }
            });
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
            while (true)
            {
                // Call our onTick function.
                onTick?.Invoke();

                // Wait to repeat again.
                if (interval > TimeSpan.Zero)
                    await Task.Delay(interval, CancellationToken.None);
            }
        }


        private async Task InitializeAsyncAlarms()
        {

            var dueTime = TimeSpan.FromSeconds(10);
            var interval = TimeSpan.FromSeconds(5);

            var cancel = new CancellationTokenSource();
            cancel.Token.ThrowIfCancellationRequested();

            // TODO: Add a CancellationTokenSource and supply the token here instead of None.
            try
            {

                await RunPeriodicAsync(checkAlarms, dueTime, interval, cancel.Token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                await InitializeAsyncAlarms();
            }
        }




        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            CancelEventArgs ce = new CancelEventArgs();
            Window_Closing(sender, ce);
        }

        private void AppSettings_Click(object sender, RoutedEventArgs e)
        {
            AppSettingsWindow appSettingsWindow = new AppSettingsWindow();
            appSettingsWindow.Show();
        }

        private void ExpSettings_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 8; i++) expSettingsWindow.load(i);
            expSettingsWindow.Show();

            expSettingsWindow.refreshParams();
            expSettingsWindow.Focus();
        }

        private void Calibrate_btn_Click(object sender, RoutedEventArgs e)
        {
            calibrationWindow.Show();
            calibrationWindow.Focus();
        }



        private void ComDebug_Click(object sender, RoutedEventArgs e)
        {
            comDebugWindow.Show();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.Save();
            Alarme a = new Alarme();
            a.sendSlackMessage("APPLICATION CLOSED");
            Task.Delay(500).ContinueWith(_ =>
            {

                comDebugWindow.Close();
                expSettingsWindow.Close();
                alarmsListWindow.Close();
                desalinatorWindow.Close();
                referenceCTDWindow.Close();
                System.Windows.Application.Current.Shutdown();
            });
        }

        private void Ellipse_MouseDown_1(object sender, MouseButtonEventArgs e)
        {

        }

        private void Ellipse_MouseDown_2(object sender, MouseButtonEventArgs e)
        {

        }

        private void Monitoring_btn_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Properties.Settings.Default["InfluxDBWebpage"].ToString());
        }

        private void RData_btn_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(Properties.Settings.Default["RDataWebpage"].ToString());
        }

        private void AlarmsSettings_Click(object sender, RoutedEventArgs e)
        {
            Alarms alarmsWindow = new Alarms();
            alarmsWindow.Show();
        }

        private void ReferenceCTD_Click(object sender, RoutedEventArgs e)
        {
            if (referenceCTDWindow == null || !referenceCTDWindow.IsVisible)
            {
                referenceCTDWindow = new ReferenceCTDWindow();
            }
            referenceCTDWindow.Show();
            referenceCTDWindow.Focus();
        }

        private void AlarmsList_Click(object sender, RoutedEventArgs e)
        {
            alarmsListWindow.Show();
            alarmsListWindow.Focus();
        }

        private void Desalinator_btn_Click(object sender, RoutedEventArgs e)
        {
            desalinatorWindow.RefreshData();
            desalinatorWindow.Show();
            desalinatorWindow.Focus();
        }

        private void Salinity_btn_Click(object sender, RoutedEventArgs e)
        {
            if (salinityWindow == null || !salinityWindow.IsVisible)
            {
                salinityWindow = new SalinityWindow();
            }
            salinityWindow.RefreshData();
            salinityWindow.Show();
            salinityWindow.Focus();
        }

        private void CleanUp_Click(object sender, RoutedEventArgs e)
        {
            if (!cleanupMode)
            {
                if (MessageBox.Show("Are you sure you want to switch to Cleanup mode?", "Cleanup Mode", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    cleanupMode = !cleanupMode;
                }
            }
            else
            {
                if (MessageBox.Show("Are you sure you want to exit Cleanup mode?", "Cleanup Mode", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    cleanupMode = !cleanupMode;
                }
            }

        }
    }
}
