/*
 Name:		Regul_Condition.ino
 Created:	05/01/2021 09:41:36
 Author:	pierr
*/

#include <ModbusRtu.h>
#include <TimeLib.h>
#include <EEPROMex.h>
#include <ArduinoJson.h>
#include "C:\Users\Max\Desktop\CocoriCO2\Libs/Mesocosmes.h"
#include "C:\Users\Max\Desktop\CocoriCO2\Libs/Hamilton.h"
#include "C:\Users\Max\Desktop\CocoriCO2\Libs/Condition.h"
#include <Ethernet.h>
#include <WebSocketsClient.h>
#include <RTC.h>

const uint8_t CONDID = 1;

/***** PIN ASSIGNMENTS *****/
const byte PIN_DEBITMETRE_1 = 56;
const byte PIN_DEBITMETRE_2 = 57;
const byte PIN_DEBITMETRE_3 = 58;
const byte PIN_NIVEAU_H_1 = 24;
const byte PIN_NIVEAU_L_1 = 2;
const byte PIN_NIVEAU_LL_1 = 55;
const byte PIN_NIVEAU_H_2 = 23;
const byte PIN_NIVEAU_L_2 = 26;
const byte PIN_NIVEAU_LL_2 = 54;
const byte PIN_NIVEAU_H_3 = 22;
const byte PIN_NIVEAU_L_3 = 25;
const byte PIN_NIVEAU_LL_3 = 3;

const byte PIN_V3V = 5;
const byte PIN_POS_V3V = 59;
const byte PIN_POMPE_MARCHE = 18;
const byte PIN_POMPE_DIR = 19;
const byte PIN_POMPE_ANA = 4;
/***************************/

uint16_t limitePIDMin = 90;
uint16_t limitePIDMax = 166;


enum {
    REQ_PARAMS = 0,
    REQ_DATA = 1,
    SEND_PARAMS = 2,
    SEND_DATA = 3,
    CALIBRATE_SENSOR = 4,
    REQ_MASTER_DATA = 5,
    SEND_MASTER_DATA = 6
};

Condition condition;



ModbusRtu master(0, 3, 46); // this is master and RS-232 or USB-FTDI
ModbusSensorHamilton Hamilton[4];// indexes O to 2 are mesocosms, index 3 is buffer tank
typedef struct Calibration {
    int sensorID;
    int calibParam;
    float value;
    bool calibEnCours;
    bool calibRequested;
}Calibration;

Calibration calib;


typedef struct tempo {
    unsigned long debut;
    unsigned long interval;
}tempo;

tempo tempoSensorRead;
tempo tempoRegulTemp;
tempo tempoRegulpH;
tempo tempoCheckMeso;
tempo tempoSendValues;
tempo tempoRR;

int sensorIndex = 0;
bool ppH = true;

// Enter a MAC address for your controller below.
// Newer Ethernet shields have a MAC address printed on a sticker on the shield
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, CONDID };

// Set the static IP address to use if the DHCP fails to assign
IPAddress ip(192, 168, 1, 160 + CONDID);

WebSocketsClient webSocket;

char buffer[600];


void webSocketEvent(WStype_t type, uint8_t* payload, size_t lenght) {
    //Serial.println(" WEBSOCKET EVENT:");
    //Serial.println(type);
    switch (type) {
    case WStype_DISCONNECTED:
        /*Serial.print(num); */Serial.println(" Disconnected!");
        break;
    case WStype_CONNECTED:
        Serial.println(" Connected!");

        // send message to client
        webSocket.sendTXT("Connected");
        break;
    case WStype_TEXT:

        Serial.print(" Payload:"); Serial.println((char*)payload);
        readJSON((char*)payload);

        // send message to client
        // webSocket.sendTXT(num, "message here");

        // send data to all connected clients
        // webSocket.broadcastTXT("message here");
        break;
    case WStype_ERROR:
        //Serial.println(" ERROR!");
        break;
    }
}

unsigned long dateToTimestamp(int year, int month, int day, int hour, int minute) {

    tmElements_t te;  //Time elements structure
    time_t unixTime; // a time stamp
    te.Day = day;
    te.Hour = hour;
    te.Minute = minute;
    te.Month = month;
    te.Second = 0;
    te.Year = year - 1970;
    unixTime = makeTime(te);
    return unixTime;
}

// the setup function runs once when you press reset or power the board
void setup() {
    pinMode(PIN_POMPE_ANA, OUTPUT);
    pinMode(PIN_POMPE_MARCHE, OUTPUT);
    pinMode(PIN_POMPE_DIR, OUTPUT);
    digitalWrite(PIN_POMPE_DIR, HIGH);

    pinMode(PIN_DEBITMETRE_1, INPUT);
    pinMode(PIN_DEBITMETRE_2, INPUT);
    pinMode(PIN_DEBITMETRE_3, INPUT);
    pinMode(PIN_NIVEAU_H_1, INPUT);
    pinMode(PIN_NIVEAU_L_1, INPUT);
    pinMode(PIN_NIVEAU_LL_1, INPUT);
    pinMode(PIN_NIVEAU_H_2, INPUT);
    pinMode(PIN_NIVEAU_L_2, INPUT);
    pinMode(PIN_NIVEAU_LL_2, INPUT);
    pinMode(PIN_NIVEAU_H_3, INPUT);
    pinMode(PIN_NIVEAU_L_3, INPUT);
    pinMode(PIN_NIVEAU_LL_3, INPUT);

    digitalWrite(PIN_POMPE_DIR, HIGH);




    Serial.begin(115200);
    master.begin(9600); // baud-rate at 19200
    master.setTimeOut(5000); // if there is no answer in 5000 ms, roll over

    Serial.println("START");

    condition = Condition();
    condition.startAddress = 2;

    load(2);
    condition.condID = CONDID;


    for (int i = 0; i < 4; i++) {
        Hamilton[i].setSensor(i + 1, &master);
    }
    condition.Meso[0] = Mesocosme(PIN_DEBITMETRE_1, PIN_NIVEAU_H_1, PIN_NIVEAU_L_1, PIN_NIVEAU_LL_1, 0);
    condition.Meso[1] = Mesocosme(PIN_DEBITMETRE_2, PIN_NIVEAU_H_2, PIN_NIVEAU_L_2, PIN_NIVEAU_LL_2, 1);
    condition.Meso[2] = Mesocosme(PIN_DEBITMETRE_3, PIN_NIVEAU_H_3, PIN_NIVEAU_L_3, PIN_NIVEAU_LL_3, 2);
    condition.mesurepH = -1;
    condition.mesureTemperature = -1;
    for (int i = 0; i < 3; i++) {
        condition.Meso[i].temperature = -1;
        condition.Meso[i].pH = -1;
        condition.Meso[i].debit = -1;
    }

    condition.regulpH.pid = PID((double*)&Hamilton[3].pH_sensorValue, &condition.regulpH.sortiePID, &condition.regulpH.consigne, condition.regulpH.Kp, condition.regulpH.Ki, condition.regulpH.Kd, DIRECT);
    condition.regulpH.pid.SetOutputLimits(30, 158);
    condition.regulpH.pid.SetMode(AUTOMATIC);

    condition.regulTemp.pid = PID((double*)&Hamilton[3].temp_sensorValue, &condition.regulTemp.sortiePID, &condition.regulTemp.consigne, condition.regulTemp.Kp, condition.regulTemp.Ki, condition.regulTemp.Kd, DIRECT);
    condition.regulTemp.pid.SetOutputLimits(limitePIDMin, limitePIDMax);
    condition.regulTemp.pid.SetMode(AUTOMATIC);

    //setPIDparams();

    
    


    tempoSensorRead.interval = 200;
    //tempoRegulTemp.interval = 100;
    tempoRegulTemp.interval = 0;

    tempoRegulpH.interval = 100;
    tempoCheckMeso.interval = 200;
    tempoSendValues.interval = 5000;
    tempoRR.debut = millis();
    tempoRR.interval = 1000;

    tempoSensorRead.debut = millis() + 2000;
    Serial.println("ETHER");
    Ethernet.begin(mac, ip);
    /*if (Ethernet.begin(mac) == 0) {
        Serial.println("Failed to configure Ethernet using DHCP");
        // no point in carrying on, so do nothing forevermore:
        // try to congifure using IP address instead of DHCP:
        Ethernet.begin(mac, ip);
    }*/
    Serial.print("link status"); Serial.println(Ethernet.linkStatus());
    Serial.print("hardware status"); Serial.println(Ethernet.hardwareStatus());

    if (Ethernet.hardwareStatus() == EthernetNoHardware) {
        while (true) {
            delay(1); // do nothing, no point running without Ethernet hardware
        }
    }
    Serial.println("Ethernet connected");

    webSocket.begin("192.168.1.10", 81,"/");
    //webSocket.begin("echo.websocket.org", 80);
    webSocket.onEvent(webSocketEvent);

    RTC.read();
}

// the loop function runs over and over again until power down or reset
void loop() {
    readMBSensors();


    checkMesocosmes();
    if (elapsed(&tempoRegulTemp.debut, tempoRegulTemp.interval)) {
        regulationTemperature();
        regulationpH();
    }

    if (elapsed(&tempoRegulpH.debut, tempoRegulpH.interval)) {

    }

    webSocket.loop();
    sendData();

}

void sendData() {
    if (elapsed(&tempoSendValues.debut, tempoSendValues.interval)) {
        Serial.println("SEND DATA");
        condition.serializeData(buffer, RTC.getTime(), CONDID);
        Serial.println(buffer);
        webSocket.sendTXT(buffer);
    }

}

void setPIDparams() {

     condition.regulpH.pid.SetTunings(condition.regulpH.Kp,condition.regulpH.Ki,condition.regulpH.Kd);
    condition.regulpH.pid.SetOutputLimits(0, 127);
    condition.regulpH.pid.SetMode(AUTOMATIC);
    condition.regulpH.pid.SetControllerDirection(REVERSE);

     condition.regulTemp.pid.SetOutputLimits(limitePIDMin, limitePIDMax);
     condition.regulTemp.pid.SetTunings(condition.regulTemp.Kp,condition.regulTemp.Ki,condition.regulTemp.Kd);
    condition.regulTemp.pid.SetMode(AUTOMATIC);
    condition.regulTemp.pid.SetControllerDirection(DIRECT);

}


int HamiltonCalibStep = 0;

int state = 0;

void calibrateSensor() {
    Serial.println("CALIBRATE PH");
    Serial.print("calib.value:"); Serial.println(calib.value);

    Serial.print("HamiltonCalibStep:"); Serial.println(HamiltonCalibStep);
    Hamilton[calib.sensorID].query.u8id = calib.sensorID + 1;
    Serial.print("Hamilton.query.u8id:"); Serial.println(Hamilton[calib.sensorID].query.u8id);
    if (calib.calibParam == 99) {
        if (state == 0) {
            if (Hamilton[calib.sensorID].factoryReset()) state = 1;
        }
        else {
            calib.calibEnCours = false;
            state = 0;
        }
    }
    else {
        HamiltonCalibStep = Hamilton[calib.sensorID].calibrate(calib.value, HamiltonCalibStep);
        if (HamiltonCalibStep == 4) {
            HamiltonCalibStep = 0;
            calib.calibEnCours = false;

            sendCalibOK();
        }
    }

}

void sendCalibOK() {
    String s = "{\"cmd\":20, \"cID\":" + String(CONDID) + String(",\"sID\":")+String(CONDID)+String("}");
    webSocket.sendTXT(s);
}


bool elapsed(unsigned long* previousMillis, unsigned long interval) {
    if (*previousMillis == 0) {
        *previousMillis = millis();
    }
    else {
        if ((unsigned long)(millis() - *previousMillis) >= interval) {
            *previousMillis = 0;
            return true;
        }
    }
    return false;
}

void checkMesocosmes() {
    if (elapsed(&tempoCheckMeso.debut, tempoCheckMeso.interval)) {
        for (int i = 0; i < 3; i++) {
            condition.Meso[i].readFlow(10);
            condition.Meso[i].checkLevel();
        }
    }

}

void readMBSensors() {
    if (elapsed(&tempoSensorRead.debut, tempoSensorRead.interval)) {
        if (calib.calibRequested) {
            calib.calibRequested = false;
            calib.calibEnCours = true;
        }
        if (calib.calibEnCours) {
            calibrateSensor();
        }
        else {
            if (ppH) {
                if (Hamilton[sensorIndex].readPH()) {
                    Serial.print("sensor address:"); Serial.println(Hamilton[sensorIndex].query.u8id);
                    Serial.print(F("pH:")); Serial.println(Hamilton[sensorIndex].pH_sensorValue);
                    if (sensorIndex < 3) condition.Meso[sensorIndex].pH = Hamilton[sensorIndex].pH_sensorValue;
                    if (sensorIndex == 3) condition.mesurepH = Hamilton[sensorIndex].pH_sensorValue;
                    ppH = false;
                }
            }
            else {
                if (Hamilton[sensorIndex].readTemp()) {
                    Serial.print("sensor address:"); Serial.println(Hamilton[sensorIndex].query.u8id);
                    Serial.print(F("temp:")); Serial.println(Hamilton[sensorIndex].temp_sensorValue);
                    if (sensorIndex < 3) condition.Meso[sensorIndex].temperature = Hamilton[sensorIndex].temp_sensorValue;
                    if (sensorIndex == 3) condition.mesureTemperature = Hamilton[sensorIndex].temp_sensorValue;
                    sensorIndex == 3 ? sensorIndex = 0 : sensorIndex++;
                    ppH = true;
                }
            }
        }
    }
    //webSocket.sendTXT("Read sensor");
}

int regulationTemperature() {
    if (condition.regulTemp.autorisationForcage) {
        if (condition.regulTemp.consigneForcage > 0 && condition.regulTemp.consigneForcage <= 100) {
            analogWrite(PIN_V3V, (int)(condition.regulTemp.consigneForcage * 255 / 100));
        }
        else {
            analogWrite(PIN_V3V, 0);
        }
    }
    else {
        if (customRegul) {
            /*
            condition.regulTemp.pid.Compute();

            double HWTep = 24.00;
            double CWTemp = condition.regulTemp.consigne - condition.regulTemp.offset;
            double diff = HWTep - CWTemp;
            double pc = (condition.regulTemp.consigne - CWTemp) / (HWTep - CWTemp);
            meanPIDOut_temp = pc * 205.0 + condition.regulTemp.sortiePID+50;
            if (meanPIDOut_temp > 200) meanPIDOut_temp = 200;
            if(meanPIDOut_temp < 95) meanPIDOut_temp = 95;





            condition.regulTemp.sortiePID_pc = (long)map((long)meanPIDOut_temp, 50, 255, 0, 100);
            if (condition.regulTemp.sortiePID_pc < 0) condition.regulTemp.sortiePID_pc = 0;*/

            if (elapsed(&tempoRR.debut, tempoRR.interval)) {
                double diff = lastTemp - Hamilton[3].temp_sensorValue;
                double err = condition.regulTemp.consigne - Hamilton[3].temp_sensorValue;

                int adjust = (int)(condition.regulTemp.Kd * diff + condition.regulTemp.Ki * err);
                if (meanPIDOut_temp > 200) meanPIDOut_temp = 200;
                if (meanPIDOut_temp < 70) meanPIDOut_temp = 70;

                meanPIDOut_temp += adjust;
                Serial.print("lastTemp"); Serial.println(lastTemp);
                Serial.print("Hamilton[3].temp_sensorValue"); Serial.println(Hamilton[3].temp_sensorValue);
                Serial.print("diff"); Serial.println(diff);
                Serial.print("err"); Serial.println(err);
                Serial.print("adjust"); Serial.println(adjust);
                Serial.print("meanPIDOut_temp"); Serial.println(meanPIDOut_temp);
                lastTemp = Hamilton[3].temp_sensorValue;

            }
            condition.regulTemp.sortiePID_pc = (int)map(meanPIDOut_temp, 50, 255, 0, 100);
            if (condition.regulTemp.sortiePID_pc < 0) condition.regulTemp.sortiePID_pc = 0;
            analogWrite(PIN_V3V, meanPIDOut_temp);

            return meanPIDOut_temp;
        }
        else {
            //condition.load();

            //setPIDparams();
            condition.regulTemp.pid.Compute();
            //Serial.println("Kp:" + String(condition.regulTemp.pid.GetKp()));
            condition.regulTemp.sortiePID_pc = (int)map(condition.regulTemp.sortiePID, 50, 255, 0, 100);
            if (condition.regulTemp.sortiePID_pc < 0) condition.regulTemp.sortiePID_pc = 0;
            analogWrite(PIN_V3V, condition.regulTemp.sortiePID);
            return condition.regulTemp.sortiePID_pc;
        }
    }

}

int regulationpH() {
    if (condition.regulpH.autorisationForcage) {
        if (condition.regulpH.consigneForcage > 0 && condition.regulpH.consigneForcage <= 100) {
            digitalWrite(PIN_POMPE_MARCHE, LOW);
            int pts = (int)map(condition.regulpH.consigneForcage,0,100,30,158);
            analogWrite(PIN_POMPE_ANA, pts);
            //Serial.println("FORCAGE!");
        }
        else {
            digitalWrite(PIN_POMPE_MARCHE, HIGH);
            analogWrite(PIN_POMPE_ANA, 0);
            //Serial.println("ARRET!");
        }
    }
    else {
        /*if (customRegul) {
            condition.regulpH.pid.Compute();

            meanPIDOut_pH = meanPIDOut_pH * (1 - regulFilter) + condition.regulpH.sortiePID * regulFilter;

            condition.regulpH.sortiePID_pc = (int)(meanPIDOut_pH / 1.27);
            if (condition.regulpH.sortiePID_pc < 0) condition.regulpH.sortiePID_pc = 0;

            if (meanPIDOut_pH < 1) digitalWrite(PIN_POMPE_MARCHE, HIGH);
            else {
                digitalWrite(PIN_POMPE_MARCHE, LOW);
                analogWrite(PIN_POMPE_ANA, (int)meanPIDOut_pH);
            }

            return meanPIDOut_pH;
        }
        else {*/
        condition.regulpH.pid.Compute();
        condition.regulpH.sortiePID_pc = (int)(condition.regulpH.sortiePID / 1.27);
        //condition.regulpH.sortiePID_pc = (int)map(condition.regulpH.sortiePID,30,158,0,100);
        if (condition.regulpH.sortiePID < 1) digitalWrite(PIN_POMPE_MARCHE, HIGH);
        else {
            digitalWrite(PIN_POMPE_MARCHE, LOW);
            analogWrite(PIN_POMPE_ANA, (int)condition.regulpH.sortiePID);
        }
        return condition.regulpH.sortiePID_pc;
        //}
    }

}

void save(int address) {
    condition.save();
}

void load(int address) {
    condition.load();
}



void readJSON(char* json) {
    StaticJsonDocument<512> doc;
    deserializeJson(doc, json);


        
    uint8_t command = doc["cmd"];
    uint8_t condID = doc["cID"];
    uint8_t senderID = doc["sID"];

    uint32_t time = doc["time"];
    if (time > 0) RTC.setTime(time);
    if (condID == CONDID) {
        switch (command) {
        case 0:

            condition.serializeParams(buffer, RTC.getTime(),CONDID);
            webSocket.sendTXT(buffer);
            break;
        case 1:
            condition.serializeData(buffer, RTC.getTime(), CONDID);
            webSocket.sendTXT(buffer);
            break;
        case 2:
        Serial.println("RECEIVED PARAMS:"+String(json));
            condition.deserializeParams(doc);
            condition.save();
            setPIDparams();
            /*condition.serializeParams(buffer, RTC.getTime(), CONDID);
            webSocket.sendTXT(buffer);*/
            break;
            /*case SEND_DATA:
                condition.deserializeData(doc);
                webSocket.sendTXT(s);
                break;*/


                /*case CALIBRATE_SENSOR:
                    readCalibRequest(doc);
                    break;*/
        case 4:
            Serial.println(F("CALIB REQ received"));
            calib.sensorID = doc[F("sensorID")];
            calib.calibParam = doc[F("calibParam")];
            calib.value = doc[F("value")];

            Serial.print(F("calib.sensorID:")); Serial.println(calib.sensorID);
            Serial.print(F("calib.calibParam:")); Serial.println(calib.calibParam);
            Serial.print(F("calib.value:")); Serial.println(calib.value);
            if (condID == CONDID) {
                calib.calibRequested = true;
            }
            break;
        default:
            //webSocket.sendTXT(F("wrong request"));
            break;
        }
    }

}
