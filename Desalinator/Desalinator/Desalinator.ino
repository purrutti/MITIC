/*
 Name:		Desalinator.ino
 Created:	10/09/2025 08:56:42
 Author:	pierr
*/
#include "Desalinator.h"

#include <Ethernet.h>
#include <WebSockets.h>
#include <WebSocketsClient.h>
#include <EEPROMex.h>
#include <ArduinoJson.h>

const byte PLCID = 7;

/***** PIN ASSIGNMENTS *****/

uint8_t pinRelaisPompeHP = 36;// Q0.0 - 24V

uint8_t pinPressionHP = 54;// I0.7 - 0-10V
uint8_t pinPressionEntree = 55;// I0.8 - 4-20mA
uint8_t pinPressionSaumure = 56;// I0.9 - 4-20mA
uint8_t pinPressionFresh = 57;// I0.10 - 4-20mA

uint8_t pinDebitEntree = 60;// I1.7 - 4-20mA
uint8_t pinDebitMOI = 61;// I1.8 - 4-20mA
uint8_t pindebitSaumure = 62;// I1.9 - 4-20mA
uint8_t pinDebitFresh = 63;// I1.10 - 4-20mA

uint8_t pinVanneEntree = 4;// A0.5 - 0-10V
uint8_t pinVanneSaumure = 5;// A0.6 - 0-10V
uint8_t pinVanneFresh = 6;// A0.7 - 0-10V
uint8_t pinV3VMOI = 7;// A1.7 - 0-10V

/***** ETHERNET COMMUNICATION *****/

byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xBB, PLCID };
IPAddress ip(192, 168, 1, 160 + PLCID);
WebSocketsClient webSocket;

tempo tempoSendData;
/***** RS485 *****/
ModbusRtu master(0, 3, 46);
tempo tempoMBSensorsRead;

ModbusSensor mbSensor(1);


bool salinite = false;
bool readSensors = true;

// Timer for debitMOI threshold check
unsigned long debitMOIBelowThresholdStart = 0;
bool debitMOIBelowThresholdActive = false;


// Facteurs correctifs de salinité
double factorDesalinator = 1.0;
int factorAddress = 230;


/******** INIT *******/

Regul regulPressionEntree, regulPressionSaumure, regulPressionFresh, regulRebouclage;
int EEPROMStartAddress = 10;
int EEPROMHPThresholdAddress = 200;
int EEPROMPressionEntreeThresholdAddress = 210;
int EEPROMDebitMOIThresholdAddress = 220;


/***** DATA STRUCTURES *****/
struct DesalinatorData {
    double pressionHP = 0;
    double pressionEntree = 0;
    double pressionSaumure = 0;
    double pressionFresh = 0;

    double debitEntree = 0;
    double debitMOI = 0;
    double debitSaumure = 0;
    double debitFresh = 0;

    double vanneEntree = 0;
    double vanneSaumure = 0;
    double vanneFresh = 0;
    double v3VMOI = 0;

    double conductivity = 0;
    double salinity = 0;
    double temperature = 0;

    bool pompeHP = false;
    double HP_threshold = 76.0;
    double pressionEntreeThreshold = 0.2;
    double debitMOIThreshold = 11;
};

DesalinatorData desalinatorData;
char buffer[600];

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
    SEND_SALINITY_PARAMS = 18
};



/***** UTILS *****/
float readFlow(uint8_t pin) {
    //2,0 x (I   4mA)   0,6v + 0, 6
    int ana = analogRead(pin);
    int mA = map(ana, 0, 1023, 0, 2000);


    int flow = map(mA, 400, 2000, 0, 3200);//0...32L/mn
    float debit = flow / 100.0;
    if (debit < 0) debit = 0;
    return debit;
}

float readPressure(uint8_t pin, int lissage, double anciennePression) {
    int ana = analogRead(pin);
    /*Serial.println("PIN analog read: " + String(pin));
    Serial.println("analog read: " + String(ana));*/
    int mA = map(ana, 0, 1023, 0, 2000);
    int mbars = map(mA, 400, 2000, 0, 4000);
    mbars = (lissage * mbars + (100.0 - lissage) * (anciennePression * 1000)) / 100;
    return ((float)mbars) / 1000.0;
}
double oldPress = 0;
double readHP() {
    long ana = analogRead(pinPressionHP);
    long volts = map(ana, 0, 1023, 0, 1000);
    double press = map(volts, 0, 1000, 0, 100000);
    press = 0.1 * press + 0.9 * oldPress;
    oldPress = press;
    return press / 1000;
}
double debitFresh, debitSaumure, debitEntree, debitMOI;

void readAnaSensors() {
    desalinatorData.pressionHP = readHP();

    desalinatorData.pressionEntree = readPressure(pinPressionEntree, 10, desalinatorData.pressionEntree);
    regulPressionEntree.mesure = desalinatorData.pressionEntree;

    desalinatorData.pressionSaumure = readPressure(pinPressionSaumure, 10, desalinatorData.pressionSaumure);
    regulPressionSaumure.mesure = desalinatorData.pressionSaumure;

    desalinatorData.pressionFresh = readPressure(pinPressionFresh, 10, desalinatorData.pressionFresh);
    regulPressionFresh.mesure = desalinatorData.pressionFresh;

    desalinatorData.debitSaumure = readFlow(pindebitSaumure);
    desalinatorData.debitFresh = readFlow(pinDebitFresh) / 3.2;
    desalinatorData.debitEntree = readFlow(pinDebitEntree);
    desalinatorData.debitMOI = readFlow(pinDebitMOI);
}

void Regulations() {
    int out;


    if (regulPressionEntree.autorisationForcage) out = (int)(regulPressionEntree.consigneForcage * 2.55);
    else
        out = (int)regulPressionEntree.compute();
    analogWrite(pinVanneEntree, out);
    desalinatorData.vanneEntree = map(out, 0, 255, 0, 100);
    regulPressionEntree.sortiePID_pc = desalinatorData.vanneEntree;

    if (regulPressionSaumure.autorisationForcage) out = (int)(regulPressionSaumure.consigneForcage * 2.55);
    else
        out = (int)regulPressionSaumure.compute();
    analogWrite(pinVanneSaumure, out);
    desalinatorData.vanneSaumure = map(out, 0, 255, 0, 100);

    regulPressionSaumure.sortiePID_pc = desalinatorData.vanneSaumure;

    if (regulPressionFresh.autorisationForcage) out = (int)(regulPressionFresh.consigneForcage * 2.55);
    else
        out = (int)regulPressionFresh.compute();
    analogWrite(pinVanneFresh, out);
    desalinatorData.vanneFresh = map(out, 0, 255, 0, 100);
    regulPressionFresh.sortiePID_pc = desalinatorData.vanneFresh;

    if (regulRebouclage.autorisationForcage) out = (int)(regulRebouclage.consigneForcage * 2.55);
    else
        out = (int)regulRebouclage.compute();
    analogWrite(pinV3VMOI, out);
    desalinatorData.v3VMOI = map(out, 0, 255, 0, 100);
    regulRebouclage.sortiePID_pc = desalinatorData.v3VMOI;
}




void setup() {
    Serial.begin(115200);
    Serial.println("Desalinator - Starting...");

    // Init pins
    pinMode(pinRelaisPompeHP, OUTPUT);
    pinMode(pinVanneEntree, OUTPUT);
    pinMode(pinVanneSaumure, OUTPUT);
    pinMode(pinVanneFresh, OUTPUT);
    pinMode(pinV3VMOI, OUTPUT);

    // Init Modbus
    master.begin(19200);
    master.setTimeOut(1000);

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

    initRegul();

    Serial.println("Setup complete - Ready for communication");
}

void initRegul() {
    Serial.println("INIT REGUL");
    int address = EEPROMStartAddress;

    regulPressionEntree = Regul();
    regulPressionSaumure = Regul();
    regulPressionFresh = Regul();
    regulRebouclage = Regul();

    address = regulPressionEntree.load(address);
    address = regulPressionSaumure.load(address);
    address = regulRebouclage.load(address);
    address = regulPressionFresh.load(address);

    // Load HP threshold from EEPROM
    double savedThreshold = EEPROM.readDouble(EEPROMHPThresholdAddress);
    if (!isnan(savedThreshold) && savedThreshold > 0 && savedThreshold < 200) {
        desalinatorData.HP_threshold = savedThreshold;
        Serial.print("HP threshold loaded from EEPROM: ");
        Serial.println(desalinatorData.HP_threshold);
    }
    else {
        // Use default value and save it
        desalinatorData.HP_threshold = 76.0;
        EEPROM.writeDouble(EEPROMHPThresholdAddress, desalinatorData.HP_threshold);
        Serial.println("HP threshold set to default value: 76.0 bars");
    }

    // Load Pression Entree threshold from EEPROM
    double savedPressionThreshold = EEPROM.readDouble(EEPROMPressionEntreeThresholdAddress);
    if (!isnan(savedPressionThreshold) && savedPressionThreshold >= 0 && savedPressionThreshold < 10) {
        desalinatorData.pressionEntreeThreshold = savedPressionThreshold;
        Serial.print("Pression Entree threshold loaded from EEPROM: ");
        Serial.println(desalinatorData.pressionEntreeThreshold);
    }
    else {
        // Use default value and save it
        desalinatorData.pressionEntreeThreshold = 0.2;
        EEPROM.writeDouble(EEPROMPressionEntreeThresholdAddress, desalinatorData.pressionEntreeThreshold);
        Serial.println("Pression Entree threshold set to default value: 0.2 bars");
    }

    // Load Debit MOI threshold from EEPROM
    double savedDebitThreshold = EEPROM.readDouble(EEPROMDebitMOIThresholdAddress);
    if (!isnan(savedDebitThreshold) && savedDebitThreshold > 0 && savedDebitThreshold < 50) {
        desalinatorData.debitMOIThreshold = savedDebitThreshold;
        Serial.print("Debit MOI threshold loaded from EEPROM: ");
        Serial.println(desalinatorData.debitMOIThreshold);
    }
    else {
        // Use default value and save it
        desalinatorData.debitMOIThreshold = 11.0;
        EEPROM.writeDouble(EEPROMDebitMOIThresholdAddress, desalinatorData.debitMOIThreshold);
        Serial.println("Debit MOI threshold set to default value: 11.0 L/min");
    }

    // Load Salinity Correction Factors from EEPROM
    double savedFactor = EEPROM.readDouble(factorAddress);
    

    if (!isnan(savedFactor) && savedFactor > 0.5 && savedFactor < 2.0) {
        factorDesalinator = savedFactor;
        Serial.print("Control correction factor loaded: ");
        Serial.println(factorDesalinator, 4);
    }



    regulPressionFresh.setPID(regulPressionFresh.Kp, regulPressionFresh.Ki, regulPressionFresh.Kd, 0, 255, REVERSE);
    regulPressionEntree.setPID(regulPressionEntree.Kp, regulPressionEntree.Ki, regulPressionEntree.Kd, 90, 255, DIRECT);
    regulPressionSaumure.setPID(regulPressionSaumure.Kp, regulPressionSaumure.Ki, regulPressionSaumure.Kd, 0, 255, REVERSE);
    regulRebouclage.setPID(regulRebouclage.Kp, regulRebouclage.Ki, regulRebouclage.Kd, 0, 160, DIRECT);
}

static unsigned long lastTest = 0;
// the loop function runs over and over again until power down or reset
void loop() {

    static unsigned long lastWebSocketUpdate = 0;
    if (millis() - lastWebSocketUpdate > 200) {
        webSocket.loop();
        lastWebSocketUpdate = millis();
    }

    // Lecture capteurs toutes les secondes
    if (millis() - lastTest > 1000) {
        lastTest = millis();
        Serial.print(F("******* REGULATIONS ****\n"));
        Serial.print(F("PWM regulPressionEntree.consigne: "));
        Serial.println(regulPressionEntree.consigne);
        Serial.print(F("PWM regulPressionSaumure.consigne: "));
        Serial.println(regulPressionSaumure.consigne);
        Serial.print(F("PWM regulRebouclage.consigne: "));
        Serial.println(regulRebouclage.consigne);
        Serial.print(F("PWM regulPressionFresh.consigne: "));
        Serial.println(regulPressionFresh.consigne);
        Serial.print(F("Regul fresh coef: "));
        Serial.println(regulPressionFresh.Kp);
        Serial.println(regulPressionFresh.Ki);

        Serial.print(F("PWM regulPressionEntree.sortiePID: "));
        Serial.println(regulPressionEntree.sortiePID);
        Serial.print(F("PWM regulPressionSaumure.sortiePID: "));
        Serial.println(regulPressionSaumure.sortiePID);
        Serial.print(F("PWM regulRebouclage.sortiePID: "));
        Serial.println(regulRebouclage.sortiePID);
        Serial.print(F("PWM regulPressionFresh.sortiePID: "));
        Serial.println(regulPressionFresh.sortiePID);

        Serial.println(F("--- Mesures ---"));
        Serial.print(F("Pression HP: "));
        Serial.print(desalinatorData.pressionHP); Serial.println(F(" bar"));
        Serial.print(F("Pression Entree: "));
        Serial.print(desalinatorData.pressionEntree); Serial.println(F(" bar"));
        Serial.print(F("Pression Saumure: "));
        Serial.print(desalinatorData.pressionSaumure); Serial.println(F(" bar"));
        Serial.print(F("Pression Fresh: "));
        Serial.print(desalinatorData.pressionFresh); Serial.println(F(" bar"));

        Serial.print(F("Debit Entree: "));
        Serial.print(desalinatorData.debitEntree); Serial.println(F(" L/min"));
        Serial.print(F("Debit MOI: "));
        Serial.print(desalinatorData.debitMOI); Serial.println(F(" L/min"));
        Serial.print(F("Debit Saumure: "));
        Serial.print(desalinatorData.debitSaumure); Serial.println(F(" L/min"));
        Serial.print(F("Debit Fresh: "));
        Serial.print(desalinatorData.debitFresh); Serial.println(F(" L/min"));


        Serial.print(F("Temperature: "));
        Serial.print(desalinatorData.temperature); Serial.println(F("  C"));
        Serial.print(F("Conductivite: "));
        Serial.print(desalinatorData.conductivity); Serial.println();
        Serial.print(F("Salinite: "));
        Serial.print(desalinatorData.salinity); Serial.println();
    }

    if (elapsed(&tempoMBSensorsRead)) {
        readSensors = true;
    }
    if (readSensors) {
        readMBSensors();
    }


    readAnaSensors();
    Regulations();
    checkPressionHP();
    sendData();
}

void checkPressionHP() {
    bool shouldStopPompe = false;
    String reason = "";


    if (desalinatorData.pompeHP == true) {

        // Check HP pressure threshold
        if (desalinatorData.pressionHP > desalinatorData.HP_threshold) {
            shouldStopPompe = true;
            reason = "HP pressure exceeded threshold";
        }

        // Check pression entree threshold
        if (desalinatorData.pressionEntree < desalinatorData.pressionEntreeThreshold) {
            shouldStopPompe = true;
            reason = "Inlet pressure below threshold";
        }

        // Check debit MOI threshold with 10-second timer
        if (desalinatorData.debitMOI < desalinatorData.debitMOIThreshold) {
            if (!debitMOIBelowThresholdActive) {
                // Start the timer
                debitMOIBelowThresholdStart = millis();
                debitMOIBelowThresholdActive = true;
            }
            else {
                // Check if 10 seconds have passed
                if (millis() - debitMOIBelowThresholdStart >= 10000) {
                    shouldStopPompe = true;
                    reason = "MOI flow below threshold for >10s";
                }
            }
        }
        else {
            // Reset timer if debit MOI is above threshold
            debitMOIBelowThresholdActive = false;
        }

        // Stop pompe if any safety condition is met
        if (shouldStopPompe) {
            desalinatorData.pompeHP = false;
            Serial.print("Pompe HP stopped: ");
            Serial.println(reason);
        }
    }
    else {
        debitMOIBelowThresholdActive = false;
    }


    digitalWrite(pinRelaisPompeHP, desalinatorData.pompeHP);
}


void sendData() {
    if (elapsed(&tempoSendData)) {
        StaticJsonDocument<512> doc;

        doc["cmd"] = 12;
        doc["cID"] = PLCID;
        doc["sID"] = PLCID;
        doc["time"] = RTC.getTime();

        doc["pressionHP"] = round(desalinatorData.pressionHP * 100) / 100.0;
        doc["pressionEntree"] = round(desalinatorData.pressionEntree * 100) / 100.0;
        doc["pressionSaumure"] = round(desalinatorData.pressionSaumure * 100) / 100.0;
        doc["pressionFresh"] = round(desalinatorData.pressionFresh * 100) / 100.0;

        doc["debitEntree"] = round(desalinatorData.debitEntree * 100) / 100.0;
        doc["debitMOI"] = round(desalinatorData.debitMOI * 100) / 100.0;
        doc["debitSaumure"] = round(desalinatorData.debitSaumure * 100) / 100.0;
        doc["debitFresh"] = round(desalinatorData.debitFresh * 100) / 100.0;

        doc["vanneEntree"] = round(desalinatorData.vanneEntree * 100) / 100.0;
        doc["vanneSaumure"] = round(desalinatorData.vanneSaumure * 100) / 100.0;
        doc["vanneFresh"] = round(desalinatorData.vanneFresh * 100) / 100.0;
        doc["v3VMOI"] = round(desalinatorData.v3VMOI * 100) / 100.0;

        doc["pompeHP"] = round(desalinatorData.pompeHP * 100) / 100.0;
        doc["HP_threshold"] = round(desalinatorData.HP_threshold * 100) / 100.0;

        doc["conductivity"] = round(desalinatorData.conductivity * 100) / 100.0;
        doc["salinity"] = round(desalinatorData.salinity * 100) / 100.0;
        doc["temperature"] = round(desalinatorData.temperature * 100) / 100.0;

        serializeJson(doc, buffer, sizeof(buffer));
        webSocket.sendTXT(buffer);

        Serial.println("Data sent:");
        Serial.println(buffer);
    }
}
void sendParams() {
    StaticJsonDocument<512> doc;
    doc["cmd"] = (int)SEND_DESALINATOR_PARAMS;
    doc["cID"] = PLCID;
    doc["sID"] = PLCID;
    doc["time"] = RTC.getTime();
    doc["HP_threshold"] = round(desalinatorData.HP_threshold * 100) / 100.0;
    doc["pompeHP"] = round(desalinatorData.pompeHP * 100) / 100.0;
    doc["pressionEntreeThreshold"] = round(desalinatorData.pressionEntreeThreshold * 100) / 100.0;
    doc["debitMOIThreshold"] = round(desalinatorData.debitMOIThreshold * 100) / 100.0;

    JsonObject regulRebouclageObj = doc.createNestedObject("regulRebouclage");
    regulRebouclageObj["cons"] = round(regulRebouclage.consigne * 100) / 100.0;
    regulRebouclageObj["Kp"] = regulRebouclage.Kp;
    regulRebouclageObj["Ki"] = regulRebouclage.Ki;
    regulRebouclageObj["Kd"] = regulRebouclage.Kd;
    regulRebouclageObj["aForcage"] = regulRebouclage.autorisationForcage ? "true" : "false";
    regulRebouclageObj["consForcage"] = round(regulRebouclage.consigneForcage * 100) / 100.0;
    regulRebouclageObj["offset"] = round(regulRebouclage.offset * 100) / 100.0;

    JsonObject regulPressionEntreeObj = doc.createNestedObject("regulPressionEntree");
    regulPressionEntreeObj["cons"] = round(regulPressionEntree.consigne * 100) / 100.0;
    regulPressionEntreeObj["Kp"] = regulPressionEntree.Kp;
    regulPressionEntreeObj["Ki"] = regulPressionEntree.Ki;
    regulPressionEntreeObj["Kd"] = regulPressionEntree.Kd;
    regulPressionEntreeObj["aForcage"] = regulPressionEntree.autorisationForcage ? "true" : "false";
    regulPressionEntreeObj["consForcage"] = round(regulPressionEntree.consigneForcage * 100) / 100.0;
    regulPressionEntreeObj["offset"] = round(regulPressionEntree.offset * 100) / 100.0;

    JsonObject regulPressionSaumureObj = doc.createNestedObject("regulPressionSaumure");
    regulPressionSaumureObj["cons"] = round(regulPressionSaumure.consigne * 100) / 100.0;
    regulPressionSaumureObj["Kp"] = regulPressionSaumure.Kp;
    regulPressionSaumureObj["Ki"] = regulPressionSaumure.Ki;
    regulPressionSaumureObj["Kd"] = regulPressionSaumure.Kd;
    regulPressionSaumureObj["aForcage"] = regulPressionSaumure.autorisationForcage ? "true" : "false";
    regulPressionSaumureObj["consForcage"] = round(regulPressionSaumure.consigneForcage * 100) / 100.0;
    regulPressionSaumureObj["offset"] = round(regulPressionSaumure.offset * 100) / 100.0;

    JsonObject regulPressionFreshObj = doc.createNestedObject("regulPressionFresh");
    regulPressionFreshObj["cons"] = round(regulPressionFresh.consigne * 100) / 100.0;
    regulPressionFreshObj["Kp"] = regulPressionFresh.Kp;
    regulPressionFreshObj["Ki"] = regulPressionFresh.Ki;
    regulPressionFreshObj["Kd"] = regulPressionFresh.Kd;
    regulPressionFreshObj["aForcage"] = regulPressionFresh.autorisationForcage ? "true" : "false";
    regulPressionFreshObj["consForcage"] = round(regulPressionFresh.consigneForcage * 100) / 100.0;
    regulPressionFreshObj["offset"] = round(regulPressionFresh.offset * 100) / 100.0;

    serializeJson(doc, buffer, sizeof(buffer));
    Serial.println(buffer);
    webSocket.sendTXT(buffer);
}

void receiveParams(StaticJsonDocument<512>& doc) {

    if (doc.containsKey("HP_threshold")) {
        desalinatorData.HP_threshold = doc["HP_threshold"];
        EEPROM.writeDouble(EEPROMHPThresholdAddress, desalinatorData.HP_threshold);
        Serial.print("HP threshold updated to: ");
        Serial.println(desalinatorData.HP_threshold);
    }

    if (doc.containsKey("pompeHP")) {
        desalinatorData.pompeHP = doc["pompeHP"];
        digitalWrite(pinRelaisPompeHP, desalinatorData.pompeHP);
        Serial.print("Pompe HP set to: ");
        Serial.println(desalinatorData.pompeHP ? "ON" : "OFF");
    }

    if (doc.containsKey("pressionEntreeThreshold")) {
        desalinatorData.pressionEntreeThreshold = doc["pressionEntreeThreshold"];
        EEPROM.writeDouble(EEPROMPressionEntreeThresholdAddress, desalinatorData.pressionEntreeThreshold);
        Serial.print("Pression Entree threshold updated to: ");
        Serial.println(desalinatorData.pressionEntreeThreshold);
    }

    if (doc.containsKey("debitMOIThreshold")) {
        desalinatorData.debitMOIThreshold = doc["debitMOIThreshold"];
        EEPROM.writeDouble(EEPROMDebitMOIThresholdAddress, desalinatorData.debitMOIThreshold);
        Serial.print("Debit MOI threshold updated to: ");
        Serial.println(desalinatorData.debitMOIThreshold);
    }


    JsonObject regul = doc["regulRebouclage"];
    regulRebouclage.consigne = regul["cons"];
    regulRebouclage.Kp = regul["Kp"];
    regulRebouclage.Ki = regul["Ki"];
    regulRebouclage.Kd = regul["Kd"];
    String aForcage = regul["aForcage"];
    if (aForcage.compareTo("true") == 0) regulRebouclage.autorisationForcage = true;
    else regulRebouclage.autorisationForcage = false;
    regulRebouclage.consigneForcage = regul["consForcage"];
    regulRebouclage.offset = regul["offset"];

    regul = doc["regulPressionEntree"];
    regulPressionEntree.consigne = regul["cons"];
    regulPressionEntree.Kp = regul["Kp"];
    regulPressionEntree.Ki = regul["Ki"];
    regulPressionEntree.Kd = regul["Kd"];
    String aForcage1 = regul["aForcage"];
    if (aForcage1.compareTo("true") == 0) regulPressionEntree.autorisationForcage = true;
    else regulPressionEntree.autorisationForcage = false;
    regulPressionEntree.consigneForcage = regul["consForcage"];
    regulPressionEntree.offset = regul["offset"];

    regul = doc["regulPressionSaumure"];
    regulPressionSaumure.consigne = regul["cons"];
    regulPressionSaumure.Kp = regul["Kp"];
    regulPressionSaumure.Ki = regul["Ki"];
    regulPressionSaumure.Kd = regul["Kd"];
    String aForcage2 = regul["aForcage"];
    if (aForcage2.compareTo("true") == 0) regulPressionSaumure.autorisationForcage = true;
    else regulPressionSaumure.autorisationForcage = false;
    regulPressionSaumure.consigneForcage = regul["consForcage"];
    regulPressionSaumure.offset = regul["offset"];

    regul = doc["regulPressionFresh"];
    regulPressionFresh.consigne = regul["cons"];
    regulPressionFresh.Kp = regul["Kp"];
    regulPressionFresh.Ki = regul["Ki"];
    regulPressionFresh.Kd = regul["Kd"];
    String aForcage3 = regul["aForcage"];
    if (aForcage3.compareTo("true") == 0) regulPressionFresh.autorisationForcage = true;
    else regulPressionFresh.autorisationForcage = false;
    regulPressionFresh.consigneForcage = regul["consForcage"];
    regulPressionFresh.offset = regul["offset"];


    int address = regulPressionEntree.save(EEPROMStartAddress);
    address = regulPressionSaumure.save(address);
    address = regulRebouclage.save(address);
    address = regulPressionFresh.save(address);
    Serial.println("Parameters updated");


    regulPressionFresh.setPID(regulPressionFresh.Kp, regulPressionFresh.Ki, regulPressionFresh.Kd, 0, 255, REVERSE);
    regulPressionEntree.setPID(regulPressionEntree.Kp, regulPressionEntree.Ki, regulPressionEntree.Kd, 90, 255, DIRECT);
    regulPressionSaumure.setPID(regulPressionSaumure.Kp, regulPressionSaumure.Ki, regulPressionSaumure.Kd, 0, 255, REVERSE);
    regulRebouclage.setPID(regulRebouclage.Kp, regulRebouclage.Ki, regulRebouclage.Kd, 0, 190, DIRECT);

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

/*double calculateSalinity(double temperature, double conductivity, double correctionFactor = 1.0) {
    double a[] = { 0.0080, -0.1692, 25.3851, 14.0941, -7.0261, 2.7081 };
    double b[] = { 0.0005, -0.0056, -0.0066, -0.0375, 0.0636, -0.0144 };
    double c[] = { 0.6766097, 2.00564e-2, 1.104259e-4, -6.9698e-7, 1.0031e-9 };
    double k = 0.0162;
    double C_ref = 42914.0;
    double R = conductivity / C_ref;
    double r_t = c[0] + c[1] * temperature + c[2] * pow(temperature, 2) +
        c[3] * pow(temperature, 3) + c[4] * pow(temperature, 4);
    double R_t = R / r_t;
    desalinatorData.salinity = (
        a[0] + a[1] * pow(R_t, 0.5) + a[2] * R_t + a[3] * pow(R_t, 1.5) +
        a[4] * pow(R_t, 2) + a[5] * pow(R_t, 2.5) +
        ((temperature - 15.0) / (1.0 + k * (temperature - 15.0))) *
        (b[0] + b[1] * pow(R_t, 0.5) + b[2] * R_t + b[3] * pow(R_t, 1.5) +
            b[4] * pow(R_t, 2) + b[5] * pow(R_t, 2.5))
        ) * correctionFactor;
    regulRebouclage.mesure = desalinatorData.salinity;
    return desalinatorData.salinity;
}*/

double calculateSalinity(double temperature, double conductivity, double correctionFactor = 1.0) {
    double a[] = { 0.0080, -0.1692, 25.3851, 14.0941, -7.0261, 2.7081 };
    double b[] = { 0.0005, -0.0056, -0.0066, -0.0375, 0.0636, -0.0144 };
    double c[] = { 0.6766097, 2.00564e-2, 1.104259e-4, -6.9698e-7, 1.0031e-9 };
    double k = 0.0162;
    double C_ref = 42914.0;
    double correctedConductivity = correctionFactor * conductivity;
    double R = correctedConductivity / C_ref;
    double r_t = c[0] + c[1] * temperature + c[2] * pow(temperature, 2) +
        c[3] * pow(temperature, 3) + c[4] * pow(temperature, 4);
    double R_t = R / r_t;
    desalinatorData.salinity = (
        a[0] + a[1] * pow(R_t, 0.5) + a[2] * R_t + a[3] * pow(R_t, 1.5) +
        a[4] * pow(R_t, 2) + a[5] * pow(R_t, 2.5) +
        ((temperature - 15.0) / (1.0 + k * (temperature - 15.0))) *
        (b[0] + b[1] * pow(R_t, 0.5) + b[2] * R_t + b[3] * pow(R_t, 1.5) +
            b[4] * pow(R_t, 2) + b[5] * pow(R_t, 2.5))
        );
    desalinatorData.conductivity = correctedConductivity;
    regulRebouclage.mesure = desalinatorData.salinity;
    return desalinatorData.salinity;
}

void readMBSensors() {
    mbSensor.query.u8id = 1;
    if (salinite) {
        if (mbSensor.readCond(&master)) {
            Serial.print("Sensor "); Serial.print(1); Serial.print(": Conductivity: ");
            Serial.println(mbSensor.cond_sensorValue);
            desalinatorData.conductivity = mbSensor.cond_sensorValue;
            salinite = false;
        }
    }
    else {
        if (mbSensor.readTemp(&master)) {
            Serial.print("Sensor "); Serial.print(1); Serial.print(": Temperature: ");
            Serial.println(mbSensor.temp_sensorValue);
            desalinatorData.temperature = mbSensor.temp_sensorValue;
            calculateSalinity(desalinatorData.temperature, desalinatorData.conductivity, factorDesalinator);
            Serial.print("Correction factor salinity:"); Serial.println(factorDesalinator);
            salinite = true;

            readSensors = false;
        }
    }
}

/*
double calculateSalinity(double temperature, double conductivity) {
    double a[] = { 0.0080, -0.1692, 25.3851, 14.0941, -7.0261, 2.7081 };
    double b[] = { 0.0005, -0.0056, -0.0066, -0.0375, 0.0636, -0.0144 };
    double c[] = { 0.6766097, 2.00564e-2, 1.104259e-4, -6.9698e-7, 1.0031e-9 };
    double k = 0.0162;
    double C_ref = 42914.0;
    double R = conductivity / C_ref;
    double r_t = c[0] + c[1] * temperature + c[2] * pow(temperature, 2) +
        c[3] * pow(temperature, 3) + c[4] * pow(temperature, 4);
    double R_t = R / r_t;
    desalinatorData.salinity = (
        a[0] + a[1] * pow(R_t, 0.5) + a[2] * R_t + a[3] * pow(R_t, 1.5) +
        a[4] * pow(R_t, 2) + a[5] * pow(R_t, 2.5) +
        ((temperature - 15.0) / (1.0 + k * (temperature - 15.0))) *
        (b[0] + b[1] * pow(R_t, 0.5) + b[2] * R_t + b[3] * pow(R_t, 1.5) +
            b[4] * pow(R_t, 2) + b[5] * pow(R_t, 2.5))
        );
    regulRebouclage.mesure = desalinatorData.salinity;
    return desalinatorData.salinity;
}*/

void readJSON(char* json) {
    StaticJsonDocument<512> doc;
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
    case 13:
        Serial.println("SEND PARAMS");
        sendParams();
        break;

    case 14:
        receiveParams(doc);
        break;
    case 21:
        // REQ_SALINITY_FACTORS - Envoi des facteurs correctifs
        sendSalinityFactors();
        break;

    case 22:
        // SEND_SALINITY_FACTORS - Reception des facteurs correctifs
        receiveSalinityFactors(doc);
        break;

    case 11:
        // Les donnees sont envoyees periodiquement
        break;

    default:
        Serial.print("Unknown command: ");
        Serial.println(command);
        break;
    }
}


void sendSalinityFactors() {
    StaticJsonDocument<600> doc;

    doc["cmd"] = (int)22;
    doc["cID"] = PLCID;
    doc["sID"] = PLCID;
    doc["time"] = RTC.getTime();
    doc["factorDesalinator"] = factorDesalinator;

    serializeJson(doc, buffer, sizeof(buffer));
    Serial.println("Sending salinity factors:");
    Serial.println(buffer);
    webSocket.sendTXT(buffer);
}

void receiveSalinityFactors(StaticJsonDocument<512>& doc) {
    bool updated = false;

    if (doc.containsKey("factorDesalinator")) {
        factorDesalinator = doc["factorDesalinator"];
        EEPROM.writeDouble(factorAddress, factorDesalinator);
        Serial.print("Control correction factor updated to: ");
        Serial.println(factorDesalinator, 4);
        updated = true;
    }

    if (updated) {
        Serial.println("All salinity correction factors updated successfully");
    }
}



