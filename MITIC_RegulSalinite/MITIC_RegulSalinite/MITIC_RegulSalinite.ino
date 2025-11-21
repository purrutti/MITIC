/*
 Name:		MITIC_RegulSalinite.ino
 Created:	18/09/2025 16:13:00
 Author:	pierr
 Description: Regulation de salinite pour 4 conditions (C0-C3) avec communication WebSocket
*/

#include <EEPROMex.h>
#include <ArduinoJson.h>
#include <Ethernet.h>
#include <WebSocketsClient.h>

#include <C:\Users\Max\Desktop\Code MITIC\MITIC-Meze_MITIC_v2\Desalinator\Desalinator\Desalinator.h>
//#include "C:/Users/pierr/Dropbox/Pierre/CNRS/repos/MITIC/Desalinator/Desalinator/Desalinator.h"

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

/***** RS232 CTD *****/
String CTDString = "";
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
char buffer[800];

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
    double rawSalinity = (
        a[0] + a[1] * pow(R_t, 0.5) + a[2] * R_t + a[3] * pow(R_t, 1.5) +
        a[4] * pow(R_t, 2) + a[5] * pow(R_t, 2.5) +
        ((temperature - 15.0) / (1.0 + k * (temperature - 15.0))) *
        (b[0] + b[1] * pow(R_t, 0.5) + b[2] * R_t + b[3] * pow(R_t, 1.5) +
            b[4] * pow(R_t, 2) + b[5] * pow(R_t, 2.5))
        );

    // Appliquer le facteur correctif
    return rawSalinity * correctionFactor;
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
                    salinityData.conductiviteControl = mbSensor[currentSensor].cond_sensorValue;
                    salinityData.saliniteControl = calculateSalinity(salinityData.temperatureControl, salinityData.conductiviteControl, factorControl);

                    break;
                case 1:
                    salinityData.conductiviteC3 = mbSensor[currentSensor].cond_sensorValue;
                    salinityData.saliniteC3 = calculateSalinity(salinityData.temperatureC3, salinityData.conductiviteC3, factorC3);
                    break;
                case 2:
                    salinityData.conductiviteC2 = mbSensor[currentSensor].cond_sensorValue;
                    salinityData.saliniteC2 = calculateSalinity(salinityData.temperatureC2, salinityData.conductiviteC2, factorC2);
                    regulC2.mesure = salinityData.saliniteC2;
                    regulC2_filtre.mesure = salinityData.saliniteC2;
                    break;
                case 3:
                    salinityData.conductiviteC1 = mbSensor[currentSensor].cond_sensorValue;
                    salinityData.saliniteC1 = calculateSalinity(salinityData.temperatureC1, salinityData.conductiviteC1, factorC1);
                    regulC1.mesure = salinityData.saliniteC1;
                    break;
                case 4:
                    salinityData.conductiviteC0 = mbSensor[currentSensor].cond_sensorValue;
                    salinityData.saliniteC0 = calculateSalinity(salinityData.temperatureC0, salinityData.conductiviteC0, factorC0);
                    break;
                }

                readingCond = false;
                currentSensor++;
                if (currentSensor >= 5) {
                    currentSensor = 0;
                }
                readSensors = false;
            }
        }
    }

}
/*
bool salinite = true;
void readMBSensors() {
    mbSensor[4].query.u8id = 1;
    if (salinite) {
        if (mbSensor[4].readCond(&master)) {
            Serial.print("Sensor "); Serial.print(1); Serial.print(": Conductivity: ");
            Serial.println(mbSensor[4].cond_sensorValue);
            salinite = false;
        }
    }
    else {
        if (mbSensor[4].readTemp(&master)) {
            Serial.print("Sensor "); Serial.print(1); Serial.print(": Temperature: ");
            Serial.println(mbSensor[4].temp_sensorValue);
            Serial.println("Salinity:"+String(calculateSalinity(mbSensor[4].temp_sensorValue, mbSensor[4].cond_sensorValue)));

            salinite = true;

            readSensors = false;
        }
    }
}*/

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

void readRS232() {
    // Send command periodically
    if (millis() - prevtempoRS232 > tempoRS232) {
        Serial2.write("TS\n");
        prevtempoRS232 = millis();
    }

    // Read incoming data non-blocking
    while (Serial2.available() > 0) {
        char c = (char)Serial2.read();
        if (c == '\n') {
            // End of message → clean and parse
            CTDString.replace('\r', ' ');
            CTDString.replace('\n', ' ');
            CTDString.replace('#', ' ');
            CTDString.trim();

            Serial.print("RS232 received: ");
            Serial.println(CTDString);

            // Parse CTD data
            parseCTDData(CTDString);

            // Reset for next message
            CTDString = "";
        }
        else {
            CTDString += c;
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
    /*
    Serial.print(regulC0.Kp, regulC0.Ki, regulC0.Kd);
    Serial.print(regulC1.Kp, regulC1.Ki, regulC1.Kd);
    Serial.print(regulC2.Kp, regulC2.Ki, regulC2.Kd);
    Serial.print(regulC3.Kp, regulC3.Ki, regulC3.Kd);
    */
}

void sendData() {
    if (elapsed(&tempoSendData)) {
        StaticJsonDocument<600> doc;

        doc["cmd"] = SEND_SALINITY_DATA;
        doc["cID"] = PLCID;
        doc["sID"] = PLCID;
        doc["time"] = RTC.getTime();

        doc["conductiviteC0"] = salinityData.conductiviteC0;
        doc["conductiviteC1"] = salinityData.conductiviteC1;
        doc["conductiviteC2"] = salinityData.conductiviteC2;
        doc["conductiviteC3"] = salinityData.conductiviteC3;
        doc["conductiviteControl"] = salinityData.conductiviteControl;

        doc["saliniteC0"] = salinityData.saliniteC0;
        doc["saliniteC1"] = salinityData.saliniteC1;
        doc["saliniteC2"] = salinityData.saliniteC2;
        doc["saliniteC3"] = salinityData.saliniteC3;
        doc["saliniteControl"] = salinityData.saliniteControl;

        doc["temperatureC0"] = salinityData.temperatureC0;
        doc["temperatureC1"] = salinityData.temperatureC1;
        doc["temperatureC2"] = salinityData.temperatureC2;
        doc["temperatureC3"] = salinityData.temperatureC3;
        doc["temperatureControl"] = salinityData.temperatureControl;

        doc["debitC0"] = salinityData.debitC0;
        doc["debitC1"] = salinityData.debitC1;
        doc["debitC2"] = salinityData.debitC2;
        doc["debitC3"] = salinityData.debitC3;

        doc["vanneC0"] = salinityData.vanneC0;
        doc["vanneC1"] = salinityData.vanneC1;
        doc["vanneC2"] = salinityData.vanneC2;
        doc["vanneC3"] = salinityData.vanneC3;
        doc["vanneC2_filtre"] = salinityData.vanneC2_filtre;

        // CTD Data
        doc["CTD_Temperature"] = ctdData.Temperature;
        doc["CTD_Conductivity"] = ctdData.Conductivity;
        doc["CTD_Oxygen"] = ctdData.Oxygen;
        doc["CTD_PSU"] = ctdData.PSU;
        doc["CTD_CalculatedPSU"] = ctdData.CalculatedPSU;

        serializeJson(doc, buffer, sizeof(buffer));
        webSocket.sendTXT(buffer);

        Serial.println("Data sent:");
        Serial.println(buffer);
    }
}

void sendParams() {
    StaticJsonDocument<600> doc;

    doc["cmd"] = SEND_SALINITY_PARAMS;
    doc["cID"] = PLCID;
    doc["sID"] = PLCID;
    doc["time"] = RTC.getTime();

    JsonObject regulC0Obj = doc.createNestedObject("regulC0");
    regulC0Obj["cons"] = regulC0.consigne;
    regulC0Obj["Kp"] = regulC0.Kp;
    regulC0Obj["Ki"] = regulC0.Ki;
    regulC0Obj["Kd"] = regulC0.Kd;
    regulC0Obj["aForcage"] = regulC0.autorisationForcage ? "true" : "false";
    regulC0Obj["consForcage"] = regulC0.consigneForcage;
    regulC0Obj["offset"] = regulC0.offset;

    JsonObject regulC1Obj = doc.createNestedObject("regulC1");
    regulC1Obj["cons"] = regulC1.consigne;
    regulC1Obj["Kp"] = regulC1.Kp;
    regulC1Obj["Ki"] = regulC1.Ki;
    regulC1Obj["Kd"] = regulC1.Kd;
    regulC1Obj["aForcage"] = regulC1.autorisationForcage ? "true" : "false";
    regulC1Obj["consForcage"] = regulC1.consigneForcage;
    regulC1Obj["offset"] = regulC1.offset;

    JsonObject regulC2Obj = doc.createNestedObject("regulC2");
    regulC2Obj["cons"] = regulC2.consigne;
    regulC2Obj["Kp"] = regulC2.Kp;
    regulC2Obj["Ki"] = regulC2.Ki;
    regulC2Obj["Kd"] = regulC2.Kd;
    regulC2Obj["aForcage"] = regulC2.autorisationForcage ? "true" : "false";
    regulC2Obj["consForcage"] = regulC2.consigneForcage;
    regulC2Obj["offset"] = regulC2.offset;

    JsonObject regulC3Obj = doc.createNestedObject("regulC3");
    regulC3Obj["cons"] = regulC3.consigne;
    regulC3Obj["Kp"] = regulC3.Kp;
    regulC3Obj["Ki"] = regulC3.Ki;
    regulC3Obj["Kd"] = regulC3.Kd;
    regulC3Obj["aForcage"] = regulC3.autorisationForcage ? "true" : "false";
    regulC3Obj["consForcage"] = regulC3.consigneForcage;
    regulC3Obj["offset"] = regulC3.offset;

    JsonObject regulC2FObj = doc.createNestedObject("regulC2_filtre");
    regulC2FObj["cons"] = regulC2_filtre.consigne;
    regulC2FObj["Kp"] = regulC2_filtre.Kp;
    regulC2FObj["Ki"] = regulC2_filtre.Ki;
    regulC2FObj["Kd"] = regulC2_filtre.Kd;
    regulC2FObj["aForcage"] = regulC2_filtre.autorisationForcage ? "true" : "false";
    regulC2FObj["consForcage"] = regulC2_filtre.consigneForcage;
    regulC2FObj["offset"] = regulC2_filtre.offset;

    serializeJson(doc, buffer, sizeof(buffer));
    Serial.println(buffer);
    webSocket.sendTXT(buffer);
}

void receiveParams(StaticJsonDocument<600>& doc) {

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
    int address = regulC0.save(EEPROMStartAddress);
    address = regulC1.save(address);
    address = regulC2.save(address);
    address = regulC3.save(address);
    address = regulC2_filtre.save(address);

    Serial.println("aForcage:" + String(regulC0.autorisationForcage));

    Serial.println("aForcage:" + String(regulC1.autorisationForcage));
    Serial.println("aForcage:" + String(regulC2.autorisationForcage));
    Serial.println("aForcage:" + String(regulC3.autorisationForcage));

    Serial.println("Parameters updated");
}


void webSocketEvent(WStype_t type, uint8_t* payload, size_t length) {
    switch (type) {
    case WStype_DISCONNECTED:
        Serial.println("WebSocket Disconnected!");
        break;

    case WStype_CONNECTED:
        Serial.println("WebSocket Connected!");
        webSocket.sendTXT("Connected");
        break;

    case WStype_TEXT:
        Serial.print("Received: ");
        Serial.println((char*)payload);
        readJSON((char*)payload);
        break;

    case WStype_ERROR:
        Serial.println("WebSocket Error!");
        break;
    }
}

void receiveRatios(StaticJsonDocument<600>& doc) {
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
    StaticJsonDocument<600> doc;

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

void receiveSalinityFactors(StaticJsonDocument<600>& doc) {
    bool updated = false;
    int factorAddress = EEPROMSalinityFactorsAddress;

    if (doc.containsKey("factorControl")) {
        factorControl = doc["factorControl"];
        EEPROM.writeDouble(factorAddress, factorControl);
        Serial.print("Control correction factor updated to: ");
        Serial.println(factorControl, 4);
        updated = true;
    }
    factorAddress += sizeof(double);

    if (doc.containsKey("factorC0")) {
        factorC0 = doc["factorC0"];
        EEPROM.writeDouble(factorAddress, factorC0);
        Serial.print("C0 correction factor updated to: ");
        Serial.println(factorC0, 4);
        updated = true;
    }
    factorAddress += sizeof(double);

    if (doc.containsKey("factorC1")) {
        factorC1 = doc["factorC1"];
        EEPROM.writeDouble(factorAddress, factorC1);
        Serial.print("C1 correction factor updated to: ");
        Serial.println(factorC1, 4);
        updated = true;
    }
    factorAddress += sizeof(double);

    if (doc.containsKey("factorC2")) {
        factorC2 = doc["factorC2"];
        EEPROM.writeDouble(factorAddress, factorC2);
        Serial.print("C2 correction factor updated to: ");
        Serial.println(factorC2, 4);
        updated = true;
    }
    factorAddress += sizeof(double);

    if (doc.containsKey("factorC3")) {
        factorC3 = doc["factorC3"];
        EEPROM.writeDouble(factorAddress, factorC3);
        Serial.print("C3 correction factor updated to: ");
        Serial.println(factorC3, 4);
        updated = true;
    }

    if (updated) {
        Serial.println("All salinity correction factors updated successfully");
    }
}

void readJSON(char* json) {
    StaticJsonDocument<600> doc;
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


void setup() {
    Serial.begin(115200);
    Serial2.begin(9600);    // RS232 port for CTD
    Serial.println("MITIC Salinity Regulation - Starting...");

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
    webSocket.begin("192.168.1.10", 81, "/");
    webSocket.onEvent(webSocketEvent);

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

    Serial.println("Setup complete - Ready for communication");
}

static unsigned long lastTest = 0;
void loop() {
    static unsigned long lastWebSocketUpdate = 0;
    if (millis() - lastWebSocketUpdate > 200) {
        webSocket.loop();
        lastWebSocketUpdate = millis();
    }

    // Affichage des données toutes les secondes
    if (millis() - lastTest > 1000) {
        lastTest = millis();
        Serial.println(F("******* REGULATIONS ****"));
        Serial.print(F("regulC0.consigne: ")); Serial.println(regulC0.consigne);
        Serial.print(F("regulC1.consigne: ")); Serial.println(regulC1.consigne);
        Serial.print(F("regulC2.consigne: ")); Serial.println(regulC2.consigne);
        Serial.print(F("regulC3.consigne: ")); Serial.println(regulC3.consigne);
        Serial.print(F("regulC2_filtre.consigne: ")); Serial.println(regulC2_filtre.consigne);

        Serial.print(F("regulC0.Kp: ")); Serial.println(regulC0.Kp);
        Serial.print(F("regulC0.Ki: ")); Serial.println(regulC0.Ki);
        Serial.print(F("regulC0.Kd: ")); Serial.println(regulC0.Kd);

        Serial.print(F("regulC1.Kp: ")); Serial.println(regulC1.Kp);
        Serial.print(F("regulC1.Ki: ")); Serial.println(regulC1.Ki);
        Serial.print(F("regulC1.Kd: ")); Serial.println(regulC1.Kd);

        Serial.print(F("regulC2.Kp: ")); Serial.println(regulC2.Kp);
        Serial.print(F("regulC2.Ki: ")); Serial.println(regulC2.Ki);
        Serial.print(F("regulC2.Kd: ")); Serial.println(regulC2.Kd);

        Serial.print(F("regulC3.Kp: ")); Serial.println(regulC3.Kp);
        Serial.print(F("regulC3.Ki: ")); Serial.println(regulC3.Ki);
        Serial.print(F("regulC3.Kd: ")); Serial.println(regulC3.Kd);

        Serial.print(F("regulC2_filtre.Kp: ")); Serial.println(regulC2_filtre.Kp);
        Serial.print(F("regulC2_filtre.Ki: ")); Serial.println(regulC2_filtre.Ki);
        Serial.print(F("regulC2_filtre.Kd: ")); Serial.println(regulC2_filtre.Kd);

        Serial.print(F("regulC0.sortiePID: ")); Serial.println(regulC0.sortiePID_pc);
        Serial.print(F("regulC1.sortiePID: ")); Serial.println(regulC1.sortiePID_pc);
        Serial.print(F("regulC2.sortiePID: ")); Serial.println(regulC2.sortiePID_pc);
        Serial.print(F("regulC3.sortiePID: ")); Serial.println(regulC3.sortiePID_pc);
        Serial.print(F("regulC2_filtre.sortiePID: ")); Serial.println(regulC2_filtre.sortiePID_pc);


        Serial.println(F("--- Mesures ---"));
        Serial.print(F("Salinite C0: ")); Serial.print(salinityData.saliniteC0); Serial.println(F(" PSU"));
        Serial.print(F("Salinite C1: ")); Serial.print(salinityData.saliniteC1); Serial.println(F(" PSU"));
        Serial.print(F("Salinite C2: ")); Serial.print(salinityData.saliniteC2); Serial.println(F(" PSU"));
        Serial.print(F("Salinite C3: ")); Serial.print(salinityData.saliniteC3); Serial.println(F(" PSU"));
        Serial.print(F("Salinite Control: ")); Serial.print(salinityData.saliniteControl); Serial.println(F(" PSU"));

        Serial.print(F("Temperature Control: ")); Serial.print(salinityData.temperatureControl); Serial.println(F(" °C"));
        Serial.print(F("Temperature C0: ")); Serial.print(salinityData.temperatureC0); Serial.println(F(" °C"));
        Serial.print(F("Temperature C1: ")); Serial.print(salinityData.temperatureC1); Serial.println(F(" °C"));
        Serial.print(F("Temperature C2: ")); Serial.print(salinityData.temperatureC2); Serial.println(F(" °C"));
        Serial.print(F("Temperature C3: ")); Serial.print(salinityData.temperatureC3); Serial.println(F(" °C"));

        Serial.print(F("Debit C0: ")); Serial.print(salinityData.debitC0); Serial.println(F(" L/min"));
        Serial.print(F("Debit C1: ")); Serial.print(salinityData.debitC1); Serial.println(F(" L/min"));
        Serial.print(F("Debit C2: ")); Serial.print(salinityData.debitC2); Serial.println(F(" L/min"));
        Serial.print(F("Debit C3: ")); Serial.print(salinityData.debitC3); Serial.println(F(" L/min"));
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
}