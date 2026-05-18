/*
 Name:		MITIC_RegulSalinite.ino
 Created:	18/09/2025 16:13:00
 Author:	pierr
 Description: Regulation de salinite pour 4 conditions (C0-C3) avec communication WebSocket

 =========================================================================
 === PATCHES 2026-04-23 (to address random freezes / data-loss symptom) ==
 =========================================================================
  [1] CTD buffer: replaced unbounded `String CTDString` with a fixed-size
      char buffer. The previous code appended to a String inside a loop and
      only reset on '\n'; when the CTD misses its terminator (confirmed to
      happen occasionally) the String grew indefinitely and fragmented the
      heap — the most probable root cause of the days-to-weeks random freeze.
  [2] WebSocket reconnect: previously WStype_DISCONNECTED only printed to
      serial; any network blip left the PLC permanently silent, matching
      the observed "last recorded value" symptom on the central app.
      The installed WebSocketsClient library on this PLC does NOT expose
      setReconnectInterval(), so we track connection state from the
      CONNECTED/DISCONNECTED events and manually call begin() again every
      WS_RECONNECT_MS while disconnected. Works on any library version.
  [3] Watchdog timer (wdt 8s): auto-reboot on any hang. Includes wdt_disable
      at start of setup() to break any reboot loop caused by a bad saved
      state.
  [4] Modbus per-sensor timeout: if a single sensor read cycle exceeds 3 s
      the state machine advances anyway. One bad probe no longer stalls
      the whole loop (which in turn was starving webSocket.loop()).
  [5] JSON document + char buffer size bumped (600->1024, 800->1200). Field
      count (33) and serialized length (~890 chars) were right at/over the
      previous margins, silently truncating on occasion.
  [6] Free-memory diagnostic added to the 1 Hz serial printout so a
      downward trend (= leak) is visible at a glance.
  [7] Optional SD logging (ENABLE_SD_LOGGING). [Superseded by [11] on
      2026-04-30 - SD logging is now always-on with CS pin 53.]
  [8] (Desalinator.h) Fixed broken sscanf in setRtcTimeFromCompileTime.

 =========================================================================
 === PATCHES 2026-05-18 (post-crash forensics + W5100 watchdog) ==========
 =========================================================================
  Crash recurred on 2026-04-27 at 08:00. Watchdog did not auto-recover -
  required power-cycle. Symptoms identical: valves stuck at last value,
  no data sent. Hypothesis: TCP connection appears open at the W5100
  layer but application traffic is dead - neither WStype_DISCONNECTED
  fires nor does the loop hang, so neither the manual reconnect nor the
  watchdog can recover. Below addresses that specific failure mode plus
  collects forensic data for next time.

  [9] Heartbeat-ack with escalating recovery. PLC tracks ms since last
      inbound message (any type). Levels:
       - >HEARTBEAT_SOFT_MS (30s)  -> webSocket.disconnect() + begin()
       - >HEARTBEAT_HARD_MS (60s)  -> Ethernet.begin(mac, ip) + WS begin
       - >HEARTBEAT_REBOOT_MS (5m) -> deliberate WDT timeout (clean reset)
      This catches the "TCP open, application dead" mode that the manual
      reconnect from patch [2] cannot detect.
  [10] EEPROM writes deferred out of WS message path. receiveParams()
      and receiveSalinityFactors() now set a *Dirty flag; actual EEPROM
      writes happen at end of loop() AFTER webSocket.loop() returns.
      Eliminates the ~130 ms blocking window during which the W5100 RX
      buffer could overflow.
  [11] SD logging ENABLED (CS pin 53, CocoriCO2 style). Two files:
       - data/MM_YYYY.csv : every 60 s, all measurements + diagnostic
         counters (freeRAM, wsReconnectCount, ethReinitCount,
         mbTimeoutCount, lastInboundMs, loopMaxDurationMs).
       - events.csv       : one line per significant event (WS up/down,
         reconnect, hard ethernet reset, MB timeout, boot reason).
      Note: pin 53 is SPI SS on the Mega and is free here (verified -
      none of the valve/flow pins overlap).
  [12] Boot reason logged from MCUSR. Tells us at next failure whether
      WDT actually fired vs. external reset (power-cycle).
  [13] Serial verbosity reduced. Per-second 30-line dump replaced by
      one-line summary every 10 s. Frees ~3 ms/s of CPU previously
      spent on Serial.print blocking.
 =========================================================================
*/

#include <avr/wdt.h>          // [3] 2026-04-23: watchdog
#include <EEPROMex.h>
#include <ArduinoJson.h>
#include <Ethernet.h>
#include <WebSocketsClient.h>
#include <SD.h>               // [11] 2026-04-30: SD logging enabled

#include <C:\Users\Max\Desktop\Code MITIC\MITIC-Meze_MITIC_v2\Desalinator\Desalinator\Desalinator.h>
//#include "C:/Users/pierr/Dropbox/Pierre/CNRS/repos/MITIC/Desalinator/Desalinator/Desalinator.h"

// [11] 2026-04-30: SD logging always-on (CocoriCO2 style, CS pin 53)
#define SD_CS_PIN 53
bool sdAvailable = false;
tempo tempoSDLog;

// [11] 2026-04-30: forward declarations. Arduino's auto-prototyping
// usually handles this, but webSocketEvent() (early in the file) calls
// logEvent() (defined later), so we declare it explicitly to be safe.
void logEvent(const char* tag);
void ensureDataDir();
void logToSD();
void heartbeatCheck();
void flushDirtyEEPROM();

typedef struct Calibration {
    int sensorID;
    int calibParam;
    float value;
    bool calibEnCours;
    bool calibRequested;
}Calibration;

Calibration calib;

const uint8_t PLCID = 8;

/***** PIN ASSIGNMENTS *****/
// Debits
uint8_t pinDebitC0 = 56;// I0.9 - 4-20mA
uint8_t pinDebitC1 = 57;// I0.10 - 4-20mA
uint8_t pinDebitC2 = 58;// I0.11 - 4-20mA
uint8_t pinDebitC3 = 59;// I0.12 - 4-20mA

// Vannes 3 voies pour regulation salinite
uint8_t pinV3VC0 = 4;// A0.5 - 0-10V
uint8_t pinV3VC1 = 5;// A0.6 - 0-10V
uint8_t pinV3VC2 = 6;// A0.7 - 0-10V
uint8_t pinV3VC2_filtre = 8;// A1.5 - 0-10V
uint8_t pinV3VC3 = 9;// A1.6 - 0-10V

/***** ETHERNET COMMUNICATION *****/
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xBB, PLCID };
IPAddress ip(192, 168, 1, 160 + PLCID);
WebSocketsClient webSocket;

// [2] 2026-04-23 (revised 2026-04-23): manual reconnect.
// The installed WebSocketsClient library does not expose
// setReconnectInterval(), so we track the connection state from the
// WStype_CONNECTED / WStype_DISCONNECTED events and call begin() again
// every WS_RECONNECT_MS while disconnected. Works on any library version.
const char* WS_HOST = "192.168.1.10";
const uint16_t WS_PORT = 81;
const char* WS_PATH = "/";
const unsigned long WS_RECONNECT_MS = 5000;
bool wsConnected = false;
unsigned long wsLastReconnectAttempt = 0;

// [9] 2026-04-30: heartbeat-ack escalation thresholds. lastInboundMs is
// updated on ANY inbound WebSocket message (text, connect, etc).
const unsigned long HEARTBEAT_SOFT_MS   = 30000UL;   // 30 s -> WS soft reconnect
const unsigned long HEARTBEAT_HARD_MS   = 60000UL;   // 60 s -> Ethernet hard reset
const unsigned long HEARTBEAT_REBOOT_MS = 300000UL;  // 5 min -> deliberate WDT
unsigned long lastInboundMs = 0;
unsigned long lastSoftRecoveryMs = 0;
unsigned long lastHardRecoveryMs = 0;

// [10] 2026-04-30: EEPROM dirty flags. WS message handlers set these;
// the actual blocking EEPROM writes happen in flushDirtyEEPROM() at the
// end of loop(), AFTER webSocket.loop() has returned.
volatile bool paramsDirty = false;
volatile bool factorsDirty = false;

// [11] 2026-04-30: diagnostic counters logged to SD
unsigned long wsReconnectCount = 0;
unsigned long ethReinitCount = 0;
unsigned long mbTimeoutCount = 0;
unsigned long loopMaxDurationMs = 0;
unsigned long loopStartMs = 0;

// [12] 2026-04-30: captured at boot from MCUSR before being cleared.
// Bit meanings: 0=PORF (power-on), 1=EXTRF (external reset),
//               2=BORF (brown-out), 3=WDRF (watchdog reset).
uint8_t bootReason = 0;

/***** RS485 *****/
ModbusRtu master(0, 3, 46);
tempo tempoMBSensorsRead;

// 5 sondes Modbus (adresses 1 à 5)
ModbusSensor mbSensor[5] = {
    ModbusSensor(2),
    ModbusSensor(3),
    ModbusSensor(4),
    ModbusSensor(5),

    ModbusSensor(6)
};

bool readSensors = true;
int currentSensor = 0;
bool readingCond = false;  // false = temp, true = cond

// [4] 2026-04-23: per-sensor read-cycle timeout to avoid one bad probe
// stalling the whole loop. If the state machine does not complete within
// MB_SENSOR_TIMEOUT_MS it is force-advanced to the next sensor.
unsigned long mbSensorCycleStart = 0;
const unsigned long MB_SENSOR_TIMEOUT_MS = 3000;

/***** RS232 CTD *****/
// [1] 2026-04-23: fixed-size buffer replaces dynamic String to prevent
// heap fragmentation when CTD drops '\n' terminators.
#define CTD_BUF_SIZE 128
char ctdBuf[CTD_BUF_SIZE];
int ctdIdx = 0;
unsigned long prevtempoRS232 = 0;
const unsigned long tempoRS232 = 10000;

/***** TIMING *****/
tempo tempoSendData;

/***** REGULATION *****/
Regul regulC0, regulC1, regulC2, regulC3, regulC2_filtre;
int EEPROMStartAddress = 10;
int EEPROMSalinityFactorsAddress = 500;

// Facteurs correctifs de salinité
double factorControl = 1.0;
double factorC0 = 1.0;
double factorC1 = 1.0;
double factorC2 = 1.0;
double factorC3 = 1.0;

/***** DATA STRUCTURES *****/
struct CTDData {
    double Temperature = 0.0;
    double Conductivity = 0.0;  // S/m
    double Oxygen = 0.0;
    double PSU = 0.0;
    String Date = "";
    String Time = "";
    double CalculatedPSU = 0.0;
    String RawData = "";
};

struct SalinityData {
    double saliniteControl = 35.0;
    double saliniteC0 = 35.0;
    double saliniteC1 = 35.0;
    double saliniteC2 = 35.0;
    double saliniteC3 = 35.0;

    double temperatureControl = 20.0;
    double temperatureC0 = 20.0;
    double temperatureC1 = 20.0;
    double temperatureC2 = 20.0;
    double temperatureC3 = 20.0;

    double conductiviteControl = 0;
    double conductiviteC0 = 0;
    double conductiviteC1 = 0;
    double conductiviteC2 = 0;
    double conductiviteC3 = 0;

    double debitC0 = 0;
    double debitC1 = 0;
    double debitC2 = 0;
    double debitC3 = 0;

    double vanneC0 = 0;
    double vanneC1 = 0;
    double vanneC2 = 0;
    double vanneC3 = 0;
    double vanneC2_filtre = 0;
};

SalinityData salinityData;
CTDData ctdData;
// [5] 2026-04-23: bumped from 800 -> 1200; serialized JSON was ~890 chars,
// previously within truncation risk.
char buffer[1200];

// Ratios de débit pour régulation
double ratioC0 = 0.0;
double ratioC1 = 0.0;
double ratioC2 = 0.0;
double ratioC3 = 0.0;

enum {
    REQ_PARAMS = 0,
    REQ_DATA = 1,
    SEND_PARAMS = 2,
    SEND_DATA = 3,
    CALIBRATE_SENSOR = 4,
    REQ_MASTER_DATA = 5,
    SEND_MASTER_DATA = 6,
    REQ_MASTER_PARAMS = 7,
    SEND_MASTER_PARAMS = 8,
    SEND_PAC_PARAMS = 9,
    REQ_PAC_PARAMS = 10,
    REQ_DESALINATOR_DATA = 11,
    SEND_DESALINATOR_DATA = 12,
    REQ_DESALINATOR_PARAMS = 13,
    SEND_DESALINATOR_PARAMS = 14,
    REQ_SALINITY_DATA = 15,
    SEND_SALINITY_DATA = 16,
    REQ_SALINITY_PARAMS = 17,
    SEND_SALINITY_PARAMS = 18,
    SEND_SALINITY_RATIOS = 19,
    SEND_CALIB_ACK = 20,
    REQ_SALINITY_FACTORS = 21,
    SEND_SALINITY_FACTORS = 22
};

/***** FUNCTIONS *****/

// [6] 2026-04-23: free-RAM probe for leak diagnostics (AVR only).
extern unsigned int __heap_start;
extern void *__brkval;
int freeMemory() {
    int free_memory;
    if ((int)__brkval == 0)
        free_memory = ((int)&free_memory) - ((int)&__heap_start);
    else
        free_memory = ((int)&free_memory) - ((int)__brkval);
    return free_memory;
}

float readFlow(uint8_t pin) {
    int ana = analogRead(pin);
    int mA = map(ana, 0, 1023, 0, 2000);
    int flow = map(mA, 400, 2000, 0, 1000); //0...32L/mn
    float debit = flow / 100.0;
    if (debit < 0) debit = 0;
    return debit;
}

double calculateSalinity(double temperature, double conductivity, double correctionFactor = 1.0) {
    double a[] = { 0.0080, -0.1692, 25.3851, 14.0941, -7.0261, 2.7081 };
    double b[] = { 0.0005, -0.0056, -0.0066, -0.0375, 0.0636, -0.0144 };
    double c[] = { 0.6766097, 2.00564e-2, 1.104259e-4, -6.9698e-7, 1.0031e-9 };
    double k = 0.0162;
    double C_ref = 42914.0;
    double R = conductivity / C_ref;
    double r_t = c[0] + c[1] * temperature + c[2] * pow(temperature, 2) +
        c[3] * pow(temperature, 3) + c[4] * pow(temperature, 4);
    double R_t = R / r_t;
    double Salinity = (
        a[0] + a[1] * pow(R_t, 0.5) + a[2] * R_t + a[3] * pow(R_t, 1.5) +
        a[4] * pow(R_t, 2) + a[5] * pow(R_t, 2.5) +
        ((temperature - 15.0) / (1.0 + k * (temperature - 15.0))) *
        (b[0] + b[1] * pow(R_t, 0.5) + b[2] * R_t + b[3] * pow(R_t, 1.5) +
            b[4] * pow(R_t, 2) + b[5] * pow(R_t, 2.5))
        );

    return Salinity;
}

int HamiltonCalibStep = 0;

int state = 0;

void calibrateSensor() {
    Serial.println("CALIBRATE PH");
    Serial.print("calib.value:"); Serial.println(calib.value);

    Serial.print("HamiltonCalibStep:"); Serial.println(HamiltonCalibStep);
    mbSensor[calib.sensorID].query.u8id = calib.sensorID + 1;
    Serial.print("Hamilton.query.u8id:"); Serial.println(mbSensor[calib.sensorID].query.u8id);
    if (calib.calibParam == 99) {
        if (state == 0) {
            if (mbSensor[calib.sensorID].factoryReset(&master)) state = 1;
        }
        else {
            calib.calibEnCours = false;
            state = 0;
        }
    }
    else {
        HamiltonCalibStep = mbSensor[calib.sensorID].calibrate(calib.value, HamiltonCalibStep, &master);
        if (HamiltonCalibStep == 4) {
            HamiltonCalibStep = 0;
            calib.calibEnCours = false;

            sendCalibOK();
        }
    }

}

void sendCalibOK() {
    String s = "{\"cmd\":20, \"cID\":" + String(PLCID) + String(",\"sID\":") + String(PLCID) + String("}");
    webSocket.sendTXT(s);
}

void readMBSensors() {
    if (calib.calibRequested) {
        calib.calibRequested = false;
        calib.calibEnCours = true;
    }
    if (calib.calibEnCours) {
        calibrateSensor();
    }
    else {
        // [4] 2026-04-23: start timer on first entry of a sensor's cycle,
        // force-advance if the cycle takes too long.
        if (mbSensorCycleStart == 0) mbSensorCycleStart = millis();
        if ((millis() - mbSensorCycleStart) > MB_SENSOR_TIMEOUT_MS) {
            Serial.print(F("[WDG] MB sensor "));
            Serial.print(currentSensor + 1);
            Serial.println(F(" timeout - skipping"));
            // [11] 2026-04-30: forensic counter + event log
            mbTimeoutCount++;
            {
                char ev[24];
                snprintf(ev, sizeof(ev), "MB_TIMEOUT_S%d", currentSensor + 1);
                logEvent(ev);
            }
            readingCond = false;
            currentSensor++;
            if (currentSensor >= 5) currentSensor = 0;
            mbSensorCycleStart = 0;
            readSensors = false;
            return;
        }

        if (!readingCond) {
            // Lecture température
            if (mbSensor[currentSensor].readTemp(&master)) {
                Serial.print("Sensor "); Serial.print(mbSensor[currentSensor].query.u8id);
                Serial.print(": Temperature: ");
                Serial.println(mbSensor[currentSensor].temp_sensorValue);

                // Stocker la température selon le capteur
                switch (currentSensor) {
                case 0: salinityData.temperatureControl = mbSensor[currentSensor].temp_sensorValue; break;
                case 1: salinityData.temperatureC3 = mbSensor[currentSensor].temp_sensorValue; break;
                case 2: salinityData.temperatureC2 = mbSensor[currentSensor].temp_sensorValue; break;
                case 3: salinityData.temperatureC1 = mbSensor[currentSensor].temp_sensorValue; break;
                case 4: salinityData.temperatureC0 = mbSensor[currentSensor].temp_sensorValue; break;
                }
                readingCond = true;
                readSensors = false;
            }
        }
        else {
            // Lecture conductivité
            if (mbSensor[currentSensor].readCond(&master)) {
                Serial.print("Sensor "); Serial.print(currentSensor + 1);
                Serial.print(": Conductivity: ");
                Serial.println(mbSensor[currentSensor].cond_sensorValue);

                // Stocker conductivité et calculer salinité selon le capteur avec facteur correctif
                switch (currentSensor) {
                case 0:
                    salinityData.conductiviteControl = mbSensor[currentSensor].cond_sensorValue*factorControl;

                    salinityData.saliniteControl = calculateSalinity(salinityData.temperatureControl, salinityData.conductiviteControl, factorControl);

                    break;
                case 1:
                    salinityData.conductiviteC3 = mbSensor[currentSensor].cond_sensorValue*factorC3;
                    salinityData.saliniteC3 = calculateSalinity(salinityData.temperatureC3, salinityData.conductiviteC3, factorC3);
                    break;
                case 2:
                    salinityData.conductiviteC2 = mbSensor[currentSensor].cond_sensorValue*factorC2;
                    salinityData.saliniteC2 = calculateSalinity(salinityData.temperatureC2, salinityData.conductiviteC2, factorC2);
                    regulC2.mesure = salinityData.saliniteC2;
                    regulC2_filtre.mesure = salinityData.saliniteC2;
                    break;
                case 3:
                    salinityData.conductiviteC1 = mbSensor[currentSensor].cond_sensorValue*factorC1;
                    salinityData.saliniteC1 = calculateSalinity(salinityData.temperatureC1, salinityData.conductiviteC1, factorC1);
                    regulC1.mesure = salinityData.saliniteC1;
                    break;
                case 4:
                    salinityData.conductiviteC0 = mbSensor[currentSensor].cond_sensorValue*factorC0;
                    salinityData.saliniteC0 = calculateSalinity(salinityData.temperatureC0, salinityData.conductiviteC0, factorC0);
                    break;
                }

                readingCond = false;
                currentSensor++;
                if (currentSensor >= 5) {
                    currentSensor = 0;
                }
                mbSensorCycleStart = 0;   // [4] 2026-04-23: cycle complete
                readSensors = false;
            }
        }
    }

}

void readAnaSensors() {
    salinityData.debitC0 = readFlow(pinDebitC0);
    salinityData.debitC1 = readFlow(pinDebitC1);
    salinityData.debitC2 = readFlow(pinDebitC2);
    salinityData.debitC3 = readFlow(pinDebitC3);

}

void parseCTDData(String data) {
    // Format: 19.9328;  0.00010;  6.271;    0.0099;29 Sep 2025; 11:57:52
    // Temperature; Conductivity (S/m); Oxygen; PSU; Date; Time

    ctdData.RawData = data;

    int index = 0;
    int startPos = 0;
    int endPos = 0;

    // Extract Temperature
    endPos = data.indexOf(',', startPos);
    if (endPos > 0) {
        ctdData.Temperature = data.substring(startPos, endPos).toDouble();
        startPos = endPos + 1;
    }

    // Extract Conductivity (S/m)
    endPos = data.indexOf(',', startPos);
    if (endPos > 0) {
        ctdData.Conductivity = data.substring(startPos, endPos).toDouble();
        startPos = endPos + 1;
    }

    // Extract Oxygen
    endPos = data.indexOf(',', startPos);
    if (endPos > 0) {
        ctdData.Oxygen = data.substring(startPos, endPos).toDouble();
        startPos = endPos + 1;
    }

    // Extract PSU
    endPos = data.indexOf(',', startPos);
    if (endPos > 0) {
        ctdData.PSU = data.substring(startPos, endPos).toDouble();
        startPos = endPos + 1;
    }

    // Extract Date
    endPos = data.indexOf(',', startPos);
    if (endPos > 0) {
        ctdData.Date = data.substring(startPos, endPos);
        ctdData.Date.trim();
        startPos = endPos + 1;
    }

    // Extract Time (rest of string)
    ctdData.Time = data.substring(startPos);
    ctdData.Time.trim();

    // Calculate PSU from Temperature and Conductivity
    // Convert Conductivity from S/m to µS/cm: 1 S/m = 10000 µS/cm
    double conductivity_uS_cm = ctdData.Conductivity * 10000.0;
    ctdData.CalculatedPSU = calculateSalinity(ctdData.Temperature, conductivity_uS_cm);

    Serial.println("CTD Data parsed:");
    Serial.print("  Temperature: "); Serial.print(ctdData.Temperature); Serial.println(" °C");
    Serial.print("  Conductivity: "); Serial.print(ctdData.Conductivity); Serial.println(" S/m");
    Serial.print("  Oxygen: "); Serial.println(ctdData.Oxygen);
    Serial.print("  PSU (CTD): "); Serial.println(ctdData.PSU);
    Serial.print("  Calculated PSU: "); Serial.println(ctdData.CalculatedPSU);
    Serial.print("  Date: "); Serial.println(ctdData.Date);
    Serial.print("  Time: "); Serial.println(ctdData.Time);
}

// [1] 2026-04-23: rewritten with fixed char buffer. If the CTD drops its
// '\n' terminator, the buffer still gets flushed when full (no more
// unbounded heap growth).
void readRS232() {
    // Send command periodically
    if (millis() - prevtempoRS232 > tempoRS232) {
        Serial2.write("TS\n");
        prevtempoRS232 = millis();
    }

    // Read incoming data non-blocking
    while (Serial2.available() > 0) {
        char c = (char)Serial2.read();
        if (c == '\n' || ctdIdx >= CTD_BUF_SIZE - 1) {
            // End of message or buffer full -> process and reset
            ctdBuf[ctdIdx] = '\0';
            if (ctdIdx > 0) {
                // Build a single, bounded String for the existing parser
                String s(ctdBuf);
                s.replace('\r', ' ');
                s.replace('#', ' ');
                s.trim();
                Serial.print("RS232 received: ");
                Serial.println(s);
                parseCTDData(s);
            }
            ctdIdx = 0;
        }
        else {
            ctdBuf[ctdIdx++] = c;
        }
    }
}

void Regulations() {
    int out;

    // Utilisation du ratio C1 comme consigne pour les régulations de débit C0, C2, C3
    regulC0.consigne = ratioC1;
    regulC2.consigne = ratioC1;
    regulC3.consigne = ratioC1;



    // Les mesures sont les ratios calculés par l'application
    regulC0.mesure = ratioC0;
    regulC2.mesure = ratioC2;
    regulC3.mesure = ratioC3;

    // Pour C1, la mesure est la salinité
    regulC1.consigne = salinityData.saliniteC0 + regulC1.offset;
    regulC1.mesure = salinityData.saliniteC1;

    // Pour C2_filtre, la mesure est la salinité C2
    regulC2_filtre.consigne = salinityData.saliniteC0 + regulC2_filtre.offset;
    regulC2_filtre.mesure = salinityData.saliniteC2;

    if (regulC0.autorisationForcage) out = (int)(regulC0.consigneForcage * 2.55);
    else
        out = (int)regulC0.compute();

    analogWrite(pinV3VC0, out);
    salinityData.vanneC0 = map(out, 0, 255, 0, 100);
    regulC0.sortiePID_pc = salinityData.vanneC0;

    if (regulC1.autorisationForcage) out = (int)(regulC1.consigneForcage * 2.55);
    else
        out = (int)regulC1.compute();
    analogWrite(pinV3VC1, out);
    salinityData.vanneC1 = map(out, 0, 255, 0, 100);
    regulC1.sortiePID_pc = salinityData.vanneC1;

    if (regulC2.autorisationForcage) out = (int)(regulC2.consigneForcage * 2.55);
    else
        out = (int)regulC2.compute();
    analogWrite(pinV3VC2, out);
    salinityData.vanneC2 = map(out, 0, 255, 0, 100);
    regulC2.sortiePID_pc = salinityData.vanneC2;

    if (regulC3.autorisationForcage) out = (int)(regulC3.consigneForcage * 2.55);
    else
        out = (int)regulC3.compute();
    analogWrite(pinV3VC3, out);
    salinityData.vanneC3 = map(out, 0, 255, 0, 100);
    regulC3.sortiePID_pc = salinityData.vanneC3;

    if (regulC2_filtre.autorisationForcage) out = (int)(regulC2_filtre.consigneForcage * 2.55);
    else
        out = (int)regulC2_filtre.compute();
    analogWrite(pinV3VC2_filtre, out);
    salinityData.vanneC2_filtre = map(out, 0, 255, 0, 100);
    regulC2_filtre.sortiePID_pc = salinityData.vanneC2_filtre;

}

void initRegul() {
    Serial.println("INIT REGUL");
    int address = EEPROMStartAddress;

    regulC0 = Regul();
    regulC1 = Regul();
    regulC2 = Regul();
    regulC3 = Regul();
    regulC2_filtre = Regul();

    regulC1.useOffset = true;
    regulC2_filtre.useOffset = true;

    Serial.println("LOAD");
    address = regulC0.load(address);
    address = regulC1.load(address);
    address = regulC2.load(address);
    address = regulC3.load(address);
    address = regulC2_filtre.load(address);

    // Load Salinity Correction Factors from EEPROM
    int factorAddress = EEPROMSalinityFactorsAddress;
    double savedFactorControl = EEPROM.readDouble(factorAddress);
    factorAddress += sizeof(double);
    double savedFactorC0 = EEPROM.readDouble(factorAddress);
    factorAddress += sizeof(double);
    double savedFactorC1 = EEPROM.readDouble(factorAddress);
    factorAddress += sizeof(double);
    double savedFactorC2 = EEPROM.readDouble(factorAddress);
    factorAddress += sizeof(double);
    double savedFactorC3 = EEPROM.readDouble(factorAddress);

    if (!isnan(savedFactorControl) && savedFactorControl > 0.5 && savedFactorControl < 2.0) {
        factorControl = savedFactorControl;
        Serial.print("Control correction factor loaded: ");
        Serial.println(factorControl, 4);
    }
    if (!isnan(savedFactorC0) && savedFactorC0 > 0.5 && savedFactorC0 < 2.0) {
        factorC0 = savedFactorC0;
        Serial.print("C0 correction factor loaded: ");
        Serial.println(factorC0, 4);
    }
    if (!isnan(savedFactorC1) && savedFactorC1 > 0.5 && savedFactorC1 < 2.0) {
        factorC1 = savedFactorC1;
        Serial.print("C1 correction factor loaded: ");
        Serial.println(factorC1, 4);
    }
    if (!isnan(savedFactorC2) && savedFactorC2 > 0.5 && savedFactorC2 < 2.0) {
        factorC2 = savedFactorC2;
        Serial.print("C2 correction factor loaded: ");
        Serial.println(factorC2, 4);
    }
    if (!isnan(savedFactorC3) && savedFactorC3 > 0.5 && savedFactorC3 < 2.0) {
        factorC3 = savedFactorC3;
        Serial.print("C3 correction factor loaded: ");
        Serial.println(factorC3, 4);
    }

    regulC0.setPID(regulC0.Kp, regulC0.Ki, regulC0.Kd, 0, 255, REVERSE);
    regulC1.setPID(regulC1.Kp, regulC1.Ki, regulC1.Kd, 0, 255, DIRECT);
    regulC2.setPID(regulC2.Kp, regulC2.Ki, regulC2.Kd, 0, 255, REVERSE);
    regulC3.setPID(regulC3.Kp, regulC3.Ki, regulC3.Kd, 0, 255, REVERSE);
    regulC2_filtre.setPID(regulC2_filtre.Kp, regulC2_filtre.Ki, regulC2_filtre.Kd, 0, 255, DIRECT);
}

// [17] 2026-05-13: helper used by sendData() to emit one of two halves.
// part=0 -> conductivities + salinities + temperatures (15 floats)
// part=1 -> debits + valves + CTD (14 floats)
// Each call produces a ~400-byte JSON document that fits comfortably
// in any plausible WebSocket frame buffer on this AVR/W5100 stack.
void sendSalinityPart(int part) {
    size_t pos = 0;
    char tmp[16];   // scratch for dtostrf

    // Header. Each part is a valid cmd=16 SEND_SALINITY_DATA message;
    // the central app's JsonHelper.DeserializePreservingExisting merges
    // the two halves into the same SalinityData object on its end.
    pos += snprintf(buffer + pos, sizeof(buffer) - pos,
                    "{\"cmd\":%d,\"cID\":%d,\"sID\":%d,\"time\":%lu",
                    (int)SEND_SALINITY_DATA, (int)PLCID, (int)PLCID,
                    (unsigned long)RTC.getTime());

    // EMIT_F:
    //  - clamps NaN/Inf to 0.0 (avoids "nan"/"inf" which are NOT valid JSON)
    //  - bounds-checks each write, clamping pos if anything would overflow
    #define EMIT_F(KEY, VAL) do { \
        double _v = (double)(VAL); \
        if (isnan(_v) || isinf(_v)) _v = 0.0; \
        if (pos + 32 < sizeof(buffer)) { \
            dtostrf(_v, 0, 2, tmp); \
            int _n = snprintf(buffer + pos, sizeof(buffer) - pos, \
                              ",\"" KEY "\":%s", tmp); \
            if (_n > 0) pos += (size_t)_n; \
            if (pos >= sizeof(buffer)) pos = sizeof(buffer) - 1; \
        } \
    } while (0)

    if (part == 0) {
        EMIT_F("conductiviteC0",      salinityData.conductiviteC0);
        EMIT_F("conductiviteC1",      salinityData.conductiviteC1);
        EMIT_F("conductiviteC2",      salinityData.conductiviteC2);
        EMIT_F("conductiviteC3",      salinityData.conductiviteC3);
        EMIT_F("conductiviteControl", salinityData.conductiviteControl);

        EMIT_F("saliniteC0",      salinityData.saliniteC0);
        EMIT_F("saliniteC1",      salinityData.saliniteC1);
        EMIT_F("saliniteC2",      salinityData.saliniteC2);
        EMIT_F("saliniteC3",      salinityData.saliniteC3);
        EMIT_F("saliniteControl", salinityData.saliniteControl);

        EMIT_F("temperatureC0",      salinityData.temperatureC0);
        EMIT_F("temperatureC1",      salinityData.temperatureC1);
        EMIT_F("temperatureC2",      salinityData.temperatureC2);
        EMIT_F("temperatureC3",      salinityData.temperatureC3);
        EMIT_F("temperatureControl", salinityData.temperatureControl);
    } else {
        EMIT_F("debitC0", salinityData.debitC0);
        EMIT_F("debitC1", salinityData.debitC1);
        EMIT_F("debitC2", salinityData.debitC2);
        EMIT_F("debitC3", salinityData.debitC3);

        EMIT_F("vanneC0",        salinityData.vanneC0);
        EMIT_F("vanneC1",        salinityData.vanneC1);
        EMIT_F("vanneC2",        salinityData.vanneC2);
        EMIT_F("vanneC3",        salinityData.vanneC3);
        EMIT_F("vanneC2_filtre", salinityData.vanneC2_filtre);

        EMIT_F("CTD_Temperature",    ctdData.Temperature);
        EMIT_F("CTD_Conductivity",   ctdData.Conductivity);
        EMIT_F("CTD_Oxygen",         ctdData.Oxygen);
        EMIT_F("CTD_PSU",            ctdData.PSU);
        EMIT_F("CTD_CalculatedPSU",  ctdData.CalculatedPSU);
    }

    #undef EMIT_F

    // Close JSON. Safe at edge of buffer because we always reserve
    // at least 1 byte for the null terminator.
    if (pos < sizeof(buffer) - 1) buffer[pos++] = '}';
    if (pos >= sizeof(buffer)) pos = sizeof(buffer) - 1;
    buffer[pos] = '\0';

    Serial.print(F("[JSON Part"));
    Serial.print(part);
    Serial.print(F("] bytes="));
    Serial.print(pos);
    Serial.print(F(" freeRAM="));
    Serial.println(freeMemory());

    webSocket.sendTXT(buffer);
    Serial.print(F("Data sent (part "));
    Serial.print(part);
    Serial.println(F("):"));
    Serial.println(buffer);
}

void sendData() {
    if (elapsed(&tempoSendData)) {
        // [16] 2026-05-13: manual snprintf/dtostrf (no ArduinoJson) to
        // avoid stack/heap collision with the SD library installed.
        // [17] 2026-05-13: split into TWO ~400-byte messages.
        //
        // Why the split: a single 915-byte message was rejected by the
        // central app although the serial monitor showed the JSON was
        // syntactically complete. Strong evidence of frame truncation
        // somewhere in the AVR WebSocketsClient/W5100 send path
        // (the CocoriCO2 PLC sketch uses char buf[600] - presumably
        // chosen because larger frames don't survive the trip on this
        // hardware). Each half-message stays well under that limit.
        //
        // The central app already handles partial messages: case 16's
        // call to JsonHelper.DeserializePreservingExisting merges any
        // present fields into the existing SalinityData while leaving
        // missing fields untouched.
        sendSalinityPart(0);
        // webSocket.loop() will be called from the main loop() between
        // sendData() calls; sendTXT is queued, not blocking, so the two
        // sends below do not interfere with each other on AVR.
        sendSalinityPart(1);
    }
}

void sendParams() {
    // [5] 2026-04-23: bumped 600 -> 1024 for safety with nested objects
    StaticJsonDocument<1024> doc;
    doc["cmd"] = SEND_SALINITY_PARAMS;
    doc["cID"] = PLCID;
    doc["sID"] = PLCID;
    doc["time"] = RTC.getTime();

    JsonObject regulC0Obj = doc.createNestedObject("regulC0");
    regulC0Obj["cons"] = round(regulC0.consigne * 100) / 100.0;
    regulC0Obj["Kp"] = regulC0.Kp;
    regulC0Obj["Ki"] = regulC0.Ki;
    regulC0Obj["Kd"] = regulC0.Kd;
    regulC0Obj["aForcage"] = regulC0.autorisationForcage ? "true" : "false";
    regulC0Obj["consForcage"] = round(regulC0.consigneForcage * 100) / 100.0;
    regulC0Obj["offset"] = round(regulC0.offset * 100) / 100.0;

    JsonObject regulC1Obj = doc.createNestedObject("regulC1");
    regulC1Obj["cons"] = round(regulC1.consigne * 100) / 100.0;
    regulC1Obj["Kp"] = regulC1.Kp;
    regulC1Obj["Ki"] = regulC1.Ki;
    regulC1Obj["Kd"] = regulC1.Kd;
    regulC1Obj["aForcage"] = regulC1.autorisationForcage ? "true" : "false";
    regulC1Obj["consForcage"] = round(regulC1.consigneForcage * 100) / 100.0;
    regulC1Obj["offset"] = round(regulC1.offset * 100) / 100.0;

    JsonObject regulC2Obj = doc.createNestedObject("regulC2");
    regulC2Obj["cons"] = round(regulC2.consigne * 100) / 100.0;
    regulC2Obj["Kp"] = regulC2.Kp;
    regulC2Obj["Ki"] = regulC2.Ki;
    regulC2Obj["Kd"] = regulC2.Kd;
    regulC2Obj["aForcage"] = regulC2.autorisationForcage ? "true" : "false";
    regulC2Obj["consForcage"] = round(regulC2.consigneForcage * 100) / 100.0;
    regulC2Obj["offset"] = round(regulC2.offset * 100) / 100.0;

    JsonObject regulC3Obj = doc.createNestedObject("regulC3");
    regulC3Obj["cons"] = round(regulC3.consigne * 100) / 100.0;
    regulC3Obj["Kp"] = regulC3.Kp;
    regulC3Obj["Ki"] = regulC3.Ki;
    regulC3Obj["Kd"] = regulC3.Kd;
    regulC3Obj["aForcage"] = regulC3.autorisationForcage ? "true" : "false";
    regulC3Obj["consForcage"] = round(regulC3.consigneForcage * 100) / 100.0;
    regulC3Obj["offset"] = round(regulC3.offset * 100) / 100.0;

    JsonObject regulC2FObj = doc.createNestedObject("regulC2_filtre");
    regulC2FObj["cons"] = round(regulC2_filtre.consigne * 100) / 100.0;
    regulC2FObj["Kp"] = regulC2_filtre.Kp;
    regulC2FObj["Ki"] = regulC2_filtre.Ki;
    regulC2FObj["Kd"] = regulC2_filtre.Kd;
    regulC2FObj["aForcage"] = regulC2_filtre.autorisationForcage ? "true" : "false";
    regulC2FObj["consForcage"] = round(regulC2_filtre.consigneForcage * 100) / 100.0;
    regulC2FObj["offset"] = round(regulC2_filtre.offset * 100) / 100.0;

    serializeJson(doc, buffer, sizeof(buffer));
    Serial.println(buffer);
    webSocket.sendTXT(buffer);
}

void receiveParams(StaticJsonDocument<1024>& doc) {

    JsonObject regul = doc["regulC0"];
    if (doc.containsKey("regulC0")) {
        JsonObject regul = doc["regulC0"];
        regulC0.consigne = regul["cons"];
        regulC0.Kp = regul["Kp"];
        regulC0.Ki = regul["Ki"];
        regulC0.Kd = regul["Kd"];

        String aForcage = regul["aForcage"];
        if (aForcage.compareTo("true") == 0) regulC0.autorisationForcage = true;
        else regulC0.autorisationForcage = false;
        regulC0.consigneForcage = regul["consForcage"];
        regulC0.offset = regul["offset"];
    }


    if (doc.containsKey("regulC1")) {
        regul = doc["regulC1"];
        //regulC1.consigne = regul["cons"];
        regulC1.Kp = regul["Kp"];
        regulC1.Ki = regul["Ki"];
        regulC1.Kd = regul["Kd"];
        String aForcage1 = regul["aForcage"];


        if (aForcage1.compareTo("true") == 0) regulC1.autorisationForcage = true;
        else regulC1.autorisationForcage = false;
        regulC1.consigneForcage = regul["consForcage"];
        regulC1.offset = regul["offset"];
        regulC1.consigne = regulC1.offset + salinityData.saliniteC0;
    }

    if (doc.containsKey("regulC2")) {
        regul = doc["regulC2"];
        regulC2.consigne = regul["cons"];
        regulC2.Kp = regul["Kp"];
        regulC2.Ki = regul["Ki"];
        regulC2.Kd = regul["Kd"];
        String aForcage2 = regul["aForcage"];
        if (aForcage2.compareTo("true") == 0) regulC2.autorisationForcage = true;
        else regulC2.autorisationForcage = false;
        regulC2.consigneForcage = regul["consForcage"];
        regulC2.offset = regul["offset"];
    }
    if (doc.containsKey("regulC3")) {
        regul = doc["regulC3"];
        regulC3.consigne = regul["cons"];
        regulC3.Kp = regul["Kp"];
        regulC3.Ki = regul["Ki"];
        regulC3.Kd = regul["Kd"];
        String aForcage3 = regul["aForcage"];


        if (aForcage3.compareTo("true") == 0) regulC3.autorisationForcage = true;
        else regulC3.autorisationForcage = false;
        regulC3.consigneForcage = regul["consForcage"];
        regulC3.offset = regul["offset"];
    }
    if (doc.containsKey("regulC2_filtre")) {
        regul = doc["regulC2_filtre"];
        //regulC2_filtre.consigne = regul["cons"];
        regulC2_filtre.Kp = regul["Kp"];
        regulC2_filtre.Ki = regul["Ki"];
        regulC2_filtre.Kd = regul["Kd"];
        String aForcage4 = regul["aForcage"];


        if (aForcage4.compareTo("true") == 0) regulC2_filtre.autorisationForcage = true;
        else regulC2_filtre.autorisationForcage = false;
        regulC2_filtre.consigneForcage = regul["consForcage"];
        regulC2_filtre.offset = regul["offset"];

        regulC2_filtre.consigne = regulC2_filtre.offset + salinityData.saliniteC0;
    }
    // [10] 2026-04-30: defer EEPROM writes out of WS message path.
    // EEPROM.updateDouble takes ~3.3 ms per call; 5 reguls * 8 fields was
    // ~130 ms of blocking inside webSocket.loop(). Now flagged for the
    // next loop() iteration after webSocket.loop() returns.
    paramsDirty = true;

    Serial.println("aForcage:" + String(regulC0.autorisationForcage));

    Serial.println("aForcage:" + String(regulC1.autorisationForcage));
    Serial.println("aForcage:" + String(regulC2.autorisationForcage));
    Serial.println("aForcage:" + String(regulC3.autorisationForcage));

    Serial.println("Parameters updated (EEPROM write deferred)");
}


void webSocketEvent(WStype_t type, uint8_t* payload, size_t length) {
    // [9] 2026-04-30: update heartbeat on ANY event from the WS layer.
    // Even a CONNECTED event proves bidirectional traffic is alive.
    lastInboundMs = millis();

    switch (type) {
    case WStype_DISCONNECTED:
        // [2] 2026-04-23: track state for manual reconnect
        wsConnected = false;
        Serial.println("WebSocket Disconnected!");
        logEvent("WS_DISCONNECTED");
        break;

    case WStype_CONNECTED:
        // [2] 2026-04-23: track state for manual reconnect
        wsConnected = true;
        Serial.println("WebSocket Connected!");
        webSocket.sendTXT("Connected");
        logEvent("WS_CONNECTED");
        break;

    case WStype_TEXT:
        Serial.print("Received: ");
        Serial.println((char*)payload);
        readJSON((char*)payload);
        break;

    case WStype_ERROR:
        Serial.println("WebSocket Error!");
        logEvent("WS_ERROR");
        break;
    }
}

void receiveRatios(StaticJsonDocument<1024>& doc) {
    ratioC0 = doc["ratioC0"];
    ratioC1 = doc["ratioC1"];
    ratioC2 = doc["ratioC2"];
    ratioC3 = doc["ratioC3"];

    Serial.println("Ratios received:");
    Serial.print("  ratioC0: "); Serial.println(ratioC0);
    Serial.print("  ratioC1: "); Serial.println(ratioC1);
    Serial.print("  ratioC2: "); Serial.println(ratioC2);
    Serial.print("  ratioC3: "); Serial.println(ratioC3);
}

void sendSalinityFactors() {
    // [5] 2026-04-23: bumped 600 -> 1024 for consistency
    StaticJsonDocument<1024> doc;

    doc["cmd"] = (int)SEND_SALINITY_FACTORS;
    doc["cID"] = PLCID;
    doc["sID"] = PLCID;
    doc["time"] = RTC.getTime();
    doc["factorControl"] = factorControl;
    doc["factorC0"] = factorC0;
    doc["factorC1"] = factorC1;
    doc["factorC2"] = factorC2;
    doc["factorC3"] = factorC3;

    serializeJson(doc, buffer, sizeof(buffer));
    Serial.println("Sending salinity factors:");
    Serial.println(buffer);
    webSocket.sendTXT(buffer);
}

void receiveSalinityFactors(StaticJsonDocument<1024>& doc) {
    bool updated = false;
    // [10] 2026-04-30: only update RAM here; EEPROM write deferred via factorsDirty.

    if (doc.containsKey("factorControl")) {
        factorControl = doc["factorControl"];
        Serial.print("Control correction factor updated to: ");
        Serial.println(factorControl, 4);
        updated = true;
    }

    if (doc.containsKey("factorC0")) {
        factorC0 = doc["factorC0"];
        Serial.print("C0 correction factor updated to: ");
        Serial.println(factorC0, 4);
        updated = true;
    }

    if (doc.containsKey("factorC1")) {
        factorC1 = doc["factorC1"];
        Serial.print("C1 correction factor updated to: ");
        Serial.println(factorC1, 4);
        updated = true;
    }

    if (doc.containsKey("factorC2")) {
        factorC2 = doc["factorC2"];
        Serial.print("C2 correction factor updated to: ");
        Serial.println(factorC2, 4);
        updated = true;
    }

    if (doc.containsKey("factorC3")) {
        factorC3 = doc["factorC3"];
        Serial.print("C3 correction factor updated to: ");
        Serial.println(factorC3, 4);
        updated = true;
    }

    if (updated) {
        factorsDirty = true;   // [10] 2026-04-30: flush EEPROM later
        Serial.println("All salinity correction factors updated (EEPROM write deferred)");
    }
}

// [10] 2026-04-30: actually perform the deferred EEPROM writes. Called
// from loop() after webSocket.loop() has returned, so the W5100 has full
// CPU time during heavy bursts of incoming WS messages.
void flushDirtyEEPROM() {
    if (paramsDirty) {
        paramsDirty = false;
        int address = regulC0.save(EEPROMStartAddress);
        address = regulC1.save(address);
        address = regulC2.save(address);
        address = regulC3.save(address);
        address = regulC2_filtre.save(address);
        Serial.println(F("[EEPROM] params flushed"));
    }
    if (factorsDirty) {
        factorsDirty = false;
        int factorAddress = EEPROMSalinityFactorsAddress;
        EEPROM.writeDouble(factorAddress, factorControl); factorAddress += sizeof(double);
        EEPROM.writeDouble(factorAddress, factorC0);      factorAddress += sizeof(double);
        EEPROM.writeDouble(factorAddress, factorC1);      factorAddress += sizeof(double);
        EEPROM.writeDouble(factorAddress, factorC2);      factorAddress += sizeof(double);
        EEPROM.writeDouble(factorAddress, factorC3);
        Serial.println(F("[EEPROM] factors flushed"));
    }
}

void readJSON(char* json) {
    // [5] 2026-04-23: bumped 600 -> 1024
    StaticJsonDocument<1024> doc;
    DeserializationError error = deserializeJson(doc, json);

    if (error) {
        Serial.print("JSON parsing failed: ");
        Serial.println(error.c_str());
        return;
    }

    uint8_t command = doc["cmd"];
    uint8_t destID = doc["cID"];
    uint32_t time = doc["time"];

    if (time > 0) {
        RTC.setTime(time);
        RTC.write();
    }

    switch (command) {
    case REQ_SALINITY_PARAMS:
        sendParams();
        break;

    case SEND_SALINITY_PARAMS:
        receiveParams(doc);
        regulC0.setPID(regulC0.Kp, regulC0.Ki, regulC0.Kd, 0, 255, REVERSE);
        regulC1.setPID(regulC1.Kp, regulC1.Ki, regulC1.Kd, 0, 255, DIRECT);
        regulC2.setPID(regulC2.Kp, regulC2.Ki, regulC2.Kd, 0, 255, REVERSE);
        regulC3.setPID(regulC3.Kp, regulC3.Ki, regulC3.Kd, 0, 255, REVERSE);
        regulC2_filtre.setPID(regulC2_filtre.Kp, regulC2_filtre.Ki, regulC2_filtre.Kd, 0, 255, DIRECT);

        break;

    case REQ_SALINITY_DATA:
        // Les donnees sont envoyees periodiquement
        break;

    case SEND_SALINITY_RATIOS:
        receiveRatios(doc);
        break;

    case REQ_SALINITY_FACTORS:
        // REQ_SALINITY_FACTORS - Envoi des facteurs correctifs
        sendSalinityFactors();
        break;

    case SEND_SALINITY_FACTORS:
        // SEND_SALINITY_FACTORS - Reception des facteurs correctifs
        receiveSalinityFactors(doc);
        break;

    default:
        Serial.print("Unknown command: ");
        Serial.println(command);
        break;
    }
}

// [11] 2026-04-30: SD logging in CocoriCO2 style. Two files:
//   - data/MM_YYYY.csv : measurements + diagnostic counters every 60 s
//   - events.csv       : one line per significant event, written immediately
//
// Note on the "data/" subdirectory: in the CocoriCO2 sketch the path is
// hardcoded as "data/MM_YYYY.csv" but the SD library does NOT auto-create
// directories. The directory must exist on the card BEFORE first run, or
// SD.open() will fail silently. ensureDataDir() handles this at boot.
void ensureDataDir() {
    if (!sdAvailable) return;
    if (!SD.exists("data")) {
        SD.mkdir("data");
    }
}

// [11] 2026-04-30: append a single timestamped line to events.csv.
// Used for connection events, recoveries, timeouts, boot reason. Tiny
// writes, infrequent — safe to do inline.
void logEvent(const char* tag) {
    if (!sdAvailable) return;
    File f = SD.open("events.csv", FILE_WRITE);
    if (!f) return;
    if (f.size() == 0) {
        f.println(F("time,freeRAM,event"));
    }
    f.print(RTC.getTime()); f.print(',');
    f.print(freeMemory()); f.print(',');
    f.println(tag);
    f.close();
}

// [11] 2026-04-30: periodic SD snapshot for post-crash forensics.
// 21 measurement columns + 6 diagnostic counters.
void logToSD() {
    if (!sdAvailable) return;
    if (!elapsed(&tempoSDLog)) return;

    String path = "data/" + String(RTC.getMonth()) + "_" + String(RTC.getYear()) + ".csv";
    bool needHeader = !SD.exists(path);
    File f = SD.open(path, FILE_WRITE);
    if (!f) {
        Serial.println(F("[SD] open failed"));
        return;
    }
    if (needHeader) {
        f.println(F(
            "time,freeRAM,"
            "salC0,salC1,salC2,salC3,salCtrl,"
            "tC0,tC1,tC2,tC3,tCtrl,"
            "dC0,dC1,dC2,dC3,"
            "vC0,vC1,vC2,vC3,vC2f,"
            "wsReconnects,ethReinits,mbTimeouts,lastInboundMs,loopMaxMs,wsConnected"
        ));
    }
    f.print(RTC.getTime());                  f.print(',');
    f.print(freeMemory());                   f.print(',');
    f.print(salinityData.saliniteC0);        f.print(',');
    f.print(salinityData.saliniteC1);        f.print(',');
    f.print(salinityData.saliniteC2);        f.print(',');
    f.print(salinityData.saliniteC3);        f.print(',');
    f.print(salinityData.saliniteControl);   f.print(',');
    f.print(salinityData.temperatureC0);     f.print(',');
    f.print(salinityData.temperatureC1);     f.print(',');
    f.print(salinityData.temperatureC2);     f.print(',');
    f.print(salinityData.temperatureC3);     f.print(',');
    f.print(salinityData.temperatureControl);f.print(',');
    f.print(salinityData.debitC0);           f.print(',');
    f.print(salinityData.debitC1);           f.print(',');
    f.print(salinityData.debitC2);           f.print(',');
    f.print(salinityData.debitC3);           f.print(',');
    f.print(salinityData.vanneC0);           f.print(',');
    f.print(salinityData.vanneC1);           f.print(',');
    f.print(salinityData.vanneC2);           f.print(',');
    f.print(salinityData.vanneC3);           f.print(',');
    f.print(salinityData.vanneC2_filtre);    f.print(',');
    f.print(wsReconnectCount);               f.print(',');
    f.print(ethReinitCount);                 f.print(',');
    f.print(mbTimeoutCount);                 f.print(',');
    f.print(millis() - lastInboundMs);       f.print(',');
    f.print(loopMaxDurationMs);              f.print(',');
    f.println(wsConnected ? 1 : 0);
    f.close();

    // Reset the loop-duration tracker for the next interval window.
    loopMaxDurationMs = 0;
}

// [9] 2026-04-30: heartbeat-ack with escalating recovery.
// Called from loop(). If we have not received ANY inbound message from
// the central app for too long, we don't trust the connection anymore
// (even if WStype_DISCONNECTED hasn't fired). The escalation matches
// the suspected failure mode: TCP open at the W5100 layer, but
// application-level traffic dead. Each level fires at most once until
// either lastInboundMs gets updated (something arrives = recovered) or
// the next level's threshold is reached.
void heartbeatCheck() {
    unsigned long now = millis();
    unsigned long sinceInbound = now - lastInboundMs;

    // Level 3: deliberate WDT reset. If nothing for 5 minutes, the
    // soft and hard recoveries clearly didn't work. Stop resetting the
    // watchdog and let the 8 s WDT fire to reboot cleanly.
    if (sinceInbound > HEARTBEAT_REBOOT_MS) {
        Serial.println(F("[HB] no inbound for 5 min - forcing WDT reboot"));
        logEvent("HB_FORCE_REBOOT");
        // Tight loop without wdt_reset() - watchdog fires within 8 s.
        while (true) { /* deliberately empty */ }
    }

    // Level 2: hard Ethernet re-init. Resets the W5100 chip itself,
    // not just the socket layer. Don't fire more than once per HARD_MS
    // window to avoid thrashing.
    if (sinceInbound > HEARTBEAT_HARD_MS &&
        (now - lastHardRecoveryMs) > HEARTBEAT_HARD_MS) {
        Serial.println(F("[HB] no inbound for 60s - hard Ethernet reset"));
        logEvent("HB_ETH_REINIT");
        ethReinitCount++;
        Ethernet.begin(mac, ip);
        webSocket.disconnect();
        webSocket.begin(WS_HOST, WS_PORT, WS_PATH);
        lastHardRecoveryMs = now;
        wsLastReconnectAttempt = now;
        return;
    }

    // Level 1: WS soft reconnect. Cheap, just nudges the WebSocket
    // library to re-establish the connection.
    if (sinceInbound > HEARTBEAT_SOFT_MS &&
        (now - lastSoftRecoveryMs) > HEARTBEAT_SOFT_MS) {
        Serial.println(F("[HB] no inbound for 30s - WS soft reconnect"));
        logEvent("HB_WS_RECONNECT");
        wsReconnectCount++;
        webSocket.disconnect();
        webSocket.begin(WS_HOST, WS_PORT, WS_PATH);
        lastSoftRecoveryMs = now;
        wsLastReconnectAttempt = now;
    }
}

// [12] 2026-04-30: capture and clear MCUSR very early in setup().
// MCUSR holds the reset cause flags; if we don't read AND clear them
// before any later wdt_disable()/wdt_enable() call, they may be lost
// or misleading on subsequent boots.
const char* bootReasonStr(uint8_t r) {
    if (r & (1 << WDRF))  return "WDT_RESET";
    if (r & (1 << BORF))  return "BROWNOUT";
    if (r & (1 << EXTRF)) return "EXTERNAL_RESET";
    if (r & (1 << PORF))  return "POWER_ON";
    return "UNKNOWN";
}


void setup() {
    // [12] 2026-04-30: capture MCUSR before anything else clears it.
    // wdt_disable() below will not clear MCUSR, but reading it here is
    // the safest place. We mask off and clear the flags afterwards.
    bootReason = MCUSR;
    MCUSR = 0;

    // [3] 2026-04-23: disable watchdog immediately to avoid reboot loop if
    // a previous WDT reset left the prescaler in a short state. Enabled
    // again at the end of setup().
    wdt_disable();

    Serial.begin(115200);
    Serial2.begin(9600);    // RS232 port for CTD
    Serial.println("MITIC Salinity Regulation - Starting...");
    Serial.println(F("[2026-04-30 build] free RAM at boot:"));
    Serial.println(freeMemory());
    Serial.print(F("[BOOT] reason: "));
    Serial.print(bootReason, BIN);
    Serial.print(F(" -> "));
    Serial.println(bootReasonStr(bootReason));

    // Init pins
    pinMode(pinV3VC0, OUTPUT);
    pinMode(pinV3VC1, OUTPUT);
    pinMode(pinV3VC2, OUTPUT);
    pinMode(pinV3VC2_filtre, OUTPUT);
    pinMode(pinV3VC3, OUTPUT);

    pinMode(pinDebitC0, INPUT);
    pinMode(pinDebitC1, INPUT);
    pinMode(pinDebitC2, INPUT);
    pinMode(pinDebitC3, INPUT);

    // Init Modbus
    master.begin(19200);
    master.setTimeOut(1000);

    // Init RS232 CTD
    Serial2.write("TS\n");  // Initial command to CTD

    // Init network
    Ethernet.begin(mac, ip);
    Serial.print("IP: ");
    Serial.println(Ethernet.localIP());

    // Init WebSocket connection
    webSocket.begin(WS_HOST, WS_PORT, WS_PATH);
    webSocket.onEvent(webSocketEvent);
    // [2] 2026-04-23: manual reconnect handled in loop() (see WS_RECONNECT_MS).
    wsLastReconnectAttempt = millis();

    // Init RTC
    if (true) setRtcTimeFromCompileTime();
    RTC.read();

    // Init timing
    tempoMBSensorsRead.debut = 0;
    tempoMBSensorsRead.interval = 1000;
    tempoSendData.debut = 0;
    tempoSendData.interval = 5000;

    // Init Regul
    initRegul();

    // [11] 2026-04-30: SD logging init (CocoriCO2 style). Pass CS pin 53.
    Serial.print(F("[SD] init... "));
    if (SD.begin(SD_CS_PIN)) {
        sdAvailable = true;
        Serial.println(F("OK"));
        ensureDataDir();
    } else {
        sdAvailable = false;
        Serial.println(F("FAILED - logging disabled"));
    }
    tempoSDLog.debut = 0;
    tempoSDLog.interval = 60000UL;   // log every 60 s

    // [12] 2026-04-30: log the boot event with the captured MCUSR reason.
    // This is the first line of forensic data when chasing the next crash.
    {
        char ev[40];
        snprintf(ev, sizeof(ev), "BOOT_%s", bootReasonStr(bootReason));
        logEvent(ev);
    }

    // [9] 2026-04-30: prime the heartbeat to avoid an immediate false alarm
    // before the first inbound message arrives.
    lastInboundMs = millis();

    Serial.println("Setup complete - Ready for communication");

    // [3] 2026-04-23: enable 8 s watchdog AFTER init is done. Any freeze
    // longer than 8 s will now auto-reset the PLC.
    wdt_enable(WDTO_8S);
}

static unsigned long lastTest = 0;
void loop() {
    // [11] 2026-04-30: track this iteration's start so we can record the
    // longest single loop iteration in the SD log. Useful to spot any
    // blocking operation that approaches the 8 s watchdog limit.
    loopStartMs = millis();

    static unsigned long lastWebSocketUpdate = 0;
    if (millis() - lastWebSocketUpdate > 200) {
        webSocket.loop();
        lastWebSocketUpdate = millis();
    }

    // [10] 2026-04-30: deferred EEPROM writes. Done AFTER webSocket.loop()
    // so the W5100 has full priority during incoming WS message bursts.
    flushDirtyEEPROM();

    // [2] 2026-04-23: manual WebSocket reconnect. If disconnected, try
    // begin() again every WS_RECONNECT_MS. Compatible with any version
    // of WebSocketsClient (the library on this PLC does not expose
    // setReconnectInterval()).
    if (!wsConnected && (millis() - wsLastReconnectAttempt) > WS_RECONNECT_MS) {
        Serial.println(F("[WS] reconnect attempt..."));
        webSocket.disconnect();
        webSocket.begin(WS_HOST, WS_PORT, WS_PATH);
        wsLastReconnectAttempt = millis();
    }

    // [9] 2026-04-30: heartbeat-ack escalation. Catches the "TCP open,
    // application dead" failure mode that the WS library cannot detect.
    heartbeatCheck();

    // [13] 2026-04-30: reduced serial verbosity. Was a 30-line dump
    // every second (~3 ms blocking each time). Now a single one-liner
    // every 10 s; the SD log is the canonical record.
    if (millis() - lastTest > 10000) {
        lastTest = millis();
        Serial.print(F("[STATUS] freeRAM="));     Serial.print(freeMemory());
        Serial.print(F(" wsConn="));               Serial.print(wsConnected ? 1 : 0);
        Serial.print(F(" sinceInbound="));         Serial.print(millis() - lastInboundMs);
        Serial.print(F("ms wsRec="));              Serial.print(wsReconnectCount);
        Serial.print(F(" ethRei="));               Serial.print(ethReinitCount);
        Serial.print(F(" mbTo="));                 Serial.print(mbTimeoutCount);
        Serial.print(F(" loopMax="));              Serial.print(loopMaxDurationMs);
        Serial.print(F("ms salC0="));              Serial.print(salinityData.saliniteC0);
        Serial.print(F(" salC1="));                Serial.print(salinityData.saliniteC1);
        Serial.print(F(" salC2="));                Serial.print(salinityData.saliniteC2);
        Serial.print(F(" salC3="));                Serial.println(salinityData.saliniteC3);
    }

    if (elapsed(&tempoMBSensorsRead)) {
        readSensors = true;
    }
    if (readSensors) {
        readMBSensors();
    }

    readAnaSensors();
    readRS232();
    Regulations();
    sendData();

    // [11] 2026-04-30: SD logging is now always-on (no #if guard).
    logToSD();

    // [11] 2026-04-30: track the longest single loop iteration. If this
    // ever approaches 8000 ms we have a serious blocking problem.
    {
        unsigned long dur = millis() - loopStartMs;
        if (dur > loopMaxDurationMs) loopMaxDurationMs = dur;
    }

    // [3] 2026-04-23: reset watchdog at end of every loop iteration.
    // If anything above hangs for >8 s the PLC reboots automatically.
    wdt_reset();
}
