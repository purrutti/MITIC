/*
 Name:		Desalinator.ino
 Created:	10/09/2025 08:56:42
 Author:	pierr
*/
#include "C:\Users\pierr\Dropbox\Pierre\CNRS\repos\MITIC\Desalinator\Desalinator\Desalinator.h"

#include <Ethernet.h>
#include <WebSockets.h>
#include <WebSocketsClient.h>
#include <TimeLib.h>
#include <EEPROMex.h>
#include <ArduinoJson.h>
#include <RTC.h>



const byte PLCID = 8;

/***** PIN ASSIGNMENTS *****/
uint8_t pinDebitC0 = 56;// I0.9 - 4-20mA
uint8_t pinDebitC1 = 57;// I0.10 - 4-20mA

uint8_t pinDebitC2 = 58;// I0.11 - 4-20mA
uint8_t pinDebitC3 = 59;// I0.12 - 4-20mA

uint8_t pinV3VC0 = 4;// A0.5 - 0-10V
uint8_t pinV3VC1 = 5;// A0.6 - 0-10V
uint8_t pinV3VC2 = 6;// A0.7 - 0-10V
uint8_t pinV3VC2_Filtre = 8;// A1.5 - 0-10V
uint8_t pinV3VC3 = 9;// A1.6 - 0-10V

/***** ETHERNET COMMUNICATION *****/
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xBB, PLCID };
IPAddress ip(172, 16, 36, 200 + PLCID);
const char* SERVER_IP = "192.168.73.14";
WebSocketsClient webSocket;

/***** RS485 *****/
ModbusRtu master(0, 3, 46);
tempo tempoMBSensorsRead;

tempo tempoNextSensor;
ModbusSensor mbSensor(1);


bool salinite = false;
bool readSensors = true;


/******** INIT *******/

Regul regulPressionHP, regulPressionEntree, regulPressionSaumure, regulPressionFresh, regulDebitRecirculation;
int EEPROMStartAddress = 10;

/*
float readFlow(uint8_t pinDebitmetre) {

    int ana = analogRead(pinDebitmetre); // 0-1023 value corresponding to 0-5 V corresponding to 0-20 mA

    // Serial.print("debit ana:"); Serial.println(ana);
    int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
    //Serial.print("debit mA:"); Serial.println(ana);
    //double ancientDebit = debit;
    float debit = (0.625 * (mA - 400)) / 100.0; // flowrate in l/mn
    //debit = (lissage * debit + (100.0 - lissage) * ancientDebit) / 100.0;
    if (debit < 0) debit = 0;
    // Serial.print("debit:"); Serial.println(debit);
    return debit;
}

float readPressure(uint8_t pinPression) {
    int ana = analogRead(pinPression); // 0-1023 value corresponding to 0-10 V corresponding to 0-20 mA
    //if using 330 ohm resistor so 20mA = 6.6V
    //int ana2 = ana * 10 / 6.6;
    int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
    int mbars = map(mA, 400, 2000, 0, 4000); //map to milli amps with 2 extra digits
    float pression = ((double)mbars) / 1000.0; // pressure in bars
    return pression;
}*/


/***** UTILS *****/
float readFlow(uint8_t pin) {
    //2,0 x (I – 4mA) – 0,6v + 0, 6
    int ana = analogRead(pin);
    int mA = map(ana, 0, 1023, 0, 2000);


    int flow = map(mA, 400, 2000, 0, 3200);//0...32L/mn
    float debit = flow / 100.0;
    if (debit < 0) debit = 0;
    return debit;
}

float readPressure(uint8_t pin) {
    int ana = analogRead(pin);
    /*Serial.println("PIN analog read: " + String(pin));
    Serial.println("analog read: " + String(ana));*/
    int mA = map(ana, 0, 1023, 0, 2000);
    int mbars = map(mA, 400, 2000, 0, 4000);
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
    regulPressionHP.mesure = readHP();
    regulPressionEntree.mesure = readPressure(pinPressionEntree);
    regulPressionSaumure.mesure = readPressure(pinPressionSaumure);
    regulPressionFresh.mesure = readPressure(pinPressionFresh);

    debitSaumure = readFlow(pinDebitRecirculation);
    debitFresh = readFlow(pinDebitFresh) / 3.2;
    debitEntree = readFlow(pinDebitEntree);
    debitMOI = readFlow(pinDebitMOI);

}

void Regulations() {
    int out;

    out = (int)regulPressionEntree.compute();
    analogWrite(pinVanneEntree, out);

    out = (int)regulPressionSaumure.compute();
    analogWrite(pinVanneSaumure, out);

    out = (int)regulPressionFresh.compute();
    analogWrite(pinVanneFresh, out);

    out = (int)regulDebitRecirculation.compute();
    analogWrite(pinV3VMOI, out);
}




// the setup function runs once when you press reset or power the board
void setup() {

    Serial.begin(115200);
    master.begin(19200); // baud-rate at 19200
    master.setTimeOut(1000); // if there is no answer in 5000 ms, roll over
    delay(500);


    pinMode(pinRelaisPompeHP, OUTPUT);

    pinMode(pinVanneEntree, OUTPUT);
    pinMode(pinVanneSaumure, OUTPUT);
    pinMode(pinVanneFresh, OUTPUT);

    pinMode(pinV3VMOI, OUTPUT);
    Serial.println("START SETUP");
    /*

    Serial.println("ETHER BEGIN");
    Ethernet.begin(mac);
    Serial.println("ETHER");
    if (Ethernet.hardwareStatus() == EthernetNoHardware) {

        Serial.println("NO ETHERNET");
        while (true) {
            delay(1000); // do nothing, no point running without Ethernet hardware
            if (Ethernet.hardwareStatus() != EthernetNoHardware) break;
        }
    }



    Serial.println("Ethernet connected");

    Serial.print("localIP"); Serial.println(Ethernet.localIP());

    webSocket.begin(SERVER_IP, 81);
    //webSocket.begin("echo.websocket.org", 80);
    webSocket.onEvent(webSocketEvent);
    */
    if (true) setRtcTimeFromCompileTime();
    RTC.read();
    tempoMBSensorsRead.debut = 0;
    tempoMBSensorsRead.interval = 1000;
    /*
    tempoNextSensor.debut = 0;
    tempoNextSensor.interval = 1000;
    */
    initRegul();


}

void initRegul() {
    /****** INIT *****/
    Serial.println("INIT REGUL");
    int address = EEPROMStartAddress;
    regulPressionHP = Regul();
    regulPressionEntree = Regul();
    regulPressionSaumure = Regul();
    regulPressionFresh = Regul();
    regulDebitRecirculation = Regul();

    regulPressionHP.consigne = 10.0;
    regulPressionHP.Kp = 100;
    regulPressionHP.Ki = 10;
    regulPressionHP.Kd = 0;


    regulPressionEntree.consigne = 0.8;
    regulPressionEntree.Kp = 100;
    regulPressionEntree.Ki = 10;
    regulPressionEntree.Kd = 0;

    regulPressionSaumure.consigne = 0.8;
    regulPressionSaumure.Kp = 100;
    regulPressionSaumure.Ki = 10;
    regulPressionSaumure.Kd = 0;

    regulDebitRecirculation.consigne = 10.0;
    regulDebitRecirculation.Kp = 100;
    regulDebitRecirculation.Ki = 10;
    regulDebitRecirculation.Kd = 0;


    regulPressionFresh.consigne = 0.8;
    regulPressionFresh.Kp = 100;
    regulPressionFresh.Ki = 10;
    regulPressionFresh.Kd = 0;


    regulPressionHP.setPID(regulPressionHP.Kp, regulPressionHP.Ki, regulPressionHP.Kd, 0, 255, DIRECT);
    regulPressionFresh.setPID(regulPressionFresh.Kp, regulPressionFresh.Ki, regulPressionFresh.Kd, 0, 255, REVERSE);
    regulPressionEntree.setPID(regulPressionEntree.Kp, regulPressionEntree.Ki, regulPressionEntree.Kd, 125, 255, DIRECT);
    regulPressionSaumure.setPID(regulPressionSaumure.Kp, regulPressionSaumure.Ki, regulPressionSaumure.Kd, 0, 255, REVERSE);
    regulDebitRecirculation.setPID(regulDebitRecirculation.Kp, regulDebitRecirculation.Ki, regulDebitRecirculation.Kd, 0, 255, DIRECT);
}

static unsigned long lastTest = 0;
// the loop function runs over and over again until power down or reset
void loop() {

    // Lecture capteurs toutes les secondes
    if (millis() - lastTest > 1000) {
        lastTest = millis();
        regulPressionHP.pid->mySetpoint = &regulPressionHP.consigne;
        Serial.print(F("******* REGULATIONS ****\nPWM regulPressionHP.consigne: ")); Serial.println(*regulPressionHP.pid->mySetpoint);
        Serial.print(F("PWM regulPressionEntree.consigne: ")); Serial.println(regulPressionEntree.consigne);
        Serial.print(F("PWM regulPressionSaumure.consigne: ")); Serial.println(regulPressionSaumure.consigne);
        Serial.print(F("PWM regulDebitRecirculation.consigne: ")); Serial.println(regulDebitRecirculation.consigne);
        Serial.print(F("PWM regulPressionFresh.consigne: ")); Serial.println(regulPressionFresh.consigne);


        Serial.print(F("PWM regulPressionHP: ")); Serial.print(regulPressionHP.sortiePID / 2.55); Serial.println(F(" %"));
        Serial.print(F("PWM regulPressionEntree: ")); Serial.print(regulPressionEntree.sortiePID / 2.55); Serial.println(F(" %"));
        Serial.print(F("PWM regulPressionSaumure: ")); Serial.print(regulPressionSaumure.sortiePID / 2.55); Serial.println(F(" %"));
        Serial.print(F("PWM regulDebitRecirculation: ")); Serial.print(regulDebitRecirculation.sortiePID / 2.55); Serial.println(F(" %"));
        Serial.print(F("PWM regulPressionFresh: ")); Serial.print(regulPressionFresh.sortiePID / 2.55); Serial.println(F(" %"));
        Serial.println(F("--- Mesures ---"));
        Serial.print(F("Pression HP: ")); Serial.print(regulPressionHP.mesure); Serial.println(F(" bar"));
        Serial.print(F("Pression Entree: ")); Serial.print(regulPressionEntree.mesure); Serial.println(F(" bar"));
        Serial.print(F("Pression Saumure: ")); Serial.print(regulPressionSaumure.mesure); Serial.println(F(" bar"));
        Serial.print(F("Pression Fresh: ")); Serial.print(regulPressionFresh.mesure); Serial.println(F(" bar"));

        Serial.print(F("Debit Entree: ")); Serial.print(debitEntree); Serial.println(F(" L/min"));
        Serial.print(F("Debit MOI: ")); Serial.print(debitMOI); Serial.println(F(" L/min"));
        Serial.print(F("Debit Saumure: ")); Serial.print(debitSaumure); Serial.println(F(" L/min"));
        Serial.print(F("Debit Fresh: ")); Serial.print(debitFresh); Serial.println(F(" L/min"));
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
}

void checkPressionHP() {
    if (regulPressionHP.mesure > 50) {
        digitalWrite(pinRelaisPompeHP, LOW);
    }
    else {
        digitalWrite(pinRelaisPompeHP, HIGH);
        //Serial.println(HIGH);
    }
}




void webSocketEvent(WStype_t type, uint8_t* payload, size_t lenght) {
    // Serial.println(" WEBSOCKET EVENT:");
    // Serial.println(type);
    switch (type) {
    case WStype_DISCONNECTED:
        Serial.println(" Disconnected!");
        break;
    case WStype_CONNECTED:
        Serial.println(" Connected!");

        // send message to client
        webSocket.sendTXT("Connected");
        //sendParams();
        break;
    case WStype_TEXT:

        //Serial.print(" Payload:"); Serial.println((char*)payload);
        readJSON((char*)payload);

        break;
    case WStype_ERROR:
        Serial.println(" ERROR!");
        break;
    }
}
void readMBSensors() {
    if (elapsed(&tempoNextSensor)) {
        mbSensor.query.u8id = 1;
        if (salinite) {
            if (mbSensor.readCond(&master)) {
                Serial.print("Sensor "); Serial.print(1); Serial.print(": Conductivity: ");
                Serial.println(mbSensor.cond_sensorValue);
                salinite = false;
            }
        }
        else {
            if (mbSensor.readTemp(&master)) {
                Serial.print("Sensor "); Serial.print(1); Serial.print(": Temperature: ");
                Serial.println(mbSensor.temp_sensorValue);
                salinite = true;

                readSensors = false;
            }
        }
    }
}


void readJSON(char* json) {
    /*StaticJsonDocument<jsonDocSize> doc;
    char buffer[bufferSize];
    // Serial.print("payload received:"); Serial.println(json);
     //deserializeJson(doc, json);

    DeserializationError error = deserializeJson(doc, json);

    if (error) {
        Serial.print(F("deserializeJson() failed: "));
        Serial.println(error.f_str());
        return;
    }

    uint8_t command = doc["cmd"];
    uint8_t destID = doc["PLCID"];
    uint8_t aquaID = doc["AquaID"];

    uint32_t time = doc["time"];
    if (time > 0) RTC.setTime(time);
    if (command == SEND_MASTER_DATA) {

        tempAmbiante = doc["tempAmbiante"];
        tempChaud = doc["tempChaud"];
        tempFroid = doc["tempFroid"];
        pHAmbiant = doc["pHAmbiant"];
    }
    else
        if (destID == PLCID) {
            if (command == 4) {
                Serial.println("CALIB REQ received");
                calib.sensorID = doc[F("sensorID")];
                calib.calibParam = doc[F("calibParam")];
                calib.value = doc[F("value")];

                Serial.print(F("calib.sensorID:")); Serial.println(calib.sensorID);
                Serial.print(F("calib.calibParam:")); Serial.println(calib.calibParam);
                Serial.print(F("calib.value:")); Serial.println(calib.value);

                calib.calibRequested = true;
            }
            switch (command) {
            case REQ_PARAMS:
                sendParams();
                //condition.serializeParams(buffer, RTC.getTime(),CONDID);
                //webSocket.sendTXT(buffer);
                break;
            case REQ_DATA:
                sendData();
                break;
            case SEND_PARAMS:
                Serial.println("AQUA ID:" + String(aquaID));
                int i = aquaID - (4 * PLCID - 12 + 1);
                if (i >= 0 && i < 4) {
                    flume[i].deserializeParams(doc);
                    flume[i].save();
                }

                break;
            case 4:
                /*
                TODO
                */

                /* break;
             default:
                 Serial.println("DEFAULT");
                 //webSocket.sendTXT(F("wrong request"));
                 break;
             }
         }*/
}



unsigned long dateToTimestamp(int year, int month, int day, int hour, int minute, int second) {

    tmElements_t te;  //Time elements structure
    time_t unixTime; // a time stamp
    te.Day = day;
    te.Hour = hour;
    te.Minute = minute;
    te.Month = month;
    te.Second = second;
    te.Year = year - 1970;
    unixTime = makeTime(te);
    return unixTime;
}

void setRtcTimeFromCompileTime() {
    // Get compile date and time
    const char* compileDate = __DATE__;
    const char* compileTime = __TIME__;

    // Parse compile date
    int month, day, year;
    sscanf(compileDate, "%s %d %d", &month, &day, &year);

    // Parse compile time
    int hour, minute, second;
    sscanf(compileTime, "%d:%d:%d", &hour, &minute, &second);

    RTC.setTime(dateToTimestamp(year, month, day, hour, minute, second));
}
