/*
  Test_IO_Desalinator.ino
  Programme de test pour vérifier les entrées / sorties
*/

#include <Arduino.h>
#include <Stepper.h>

/***** PIN ASSIGNMENTS *****/
uint8_t pinRelaisPompeHP = 36;

uint8_t pinPressionHP = 54;
uint8_t pinPressionEntree = 55;
uint8_t pinPressionSaumure = 56;
uint8_t pinPressionFresh = 57;

uint8_t pinDebitEntree = 60;
uint8_t pinDebitMOI = 61;
uint8_t pinDebitRecirculation = 62;
uint8_t pinDebitFresh = 63;

uint8_t pinVanneEntree = 4;
uint8_t pinVanneSaumure = 5;
uint8_t pinVanneFresh = 6;
uint8_t pinV3VMOI = 7;

/***** STEPPER MOTOR *****/
const int stepsPerRevolution = 200;
Stepper myStepper(stepsPerRevolution, 7, 9, 8, 45); // ?? attention aux pins déjà utilisés !

/***** UTILS *****/
float readFlow(uint8_t pin) {
    int ana = analogRead(pin);
    int mA = map(ana, 0, 1023, 0, 2000);
    float debit = (0.625 * (mA - 400)) / 100.0;
    if (debit < 0) debit = 0;
    return debit;
}

float readPressure(uint8_t pin) {
    int ana = analogRead(pin);
    int mA = map(ana, 0, 1023, 0, 2000);
    int mbars = map(mA, 400, 2000, 0, 4000);
    return ((float)mbars) / 1000.0;
}

/***** SETUP *****/
void setup() {
    Serial.begin(115200);
    Serial.println(F("=== Test I/O Desalinator ==="));

    pinMode(pinRelaisPompeHP, OUTPUT);

    pinMode(pinVanneEntree, OUTPUT);
    pinMode(pinVanneSaumure, OUTPUT);
    pinMode(pinVanneFresh, OUTPUT);
    pinMode(pinV3VMOI, OUTPUT);

    myStepper.setSpeed(30);

    delay(2000);
}

/***** LOOP *****/
void loop() {
    static unsigned long lastTest = 0;
    static int etape = 0;

    // Test relais pompe
    bool r = (millis() / 10000) % 2;
    digitalWrite(pinRelaisPompeHP, r);

    // Balayage PWM vannes
    int pwmVal = (millis() / 500) % 256;
    analogWrite(pinVanneEntree, pwmVal);
    analogWrite(pinVanneSaumure, pwmVal);
    analogWrite(pinVanneFresh, pwmVal);
    analogWrite(pinV3VMOI, pwmVal);

    // Lecture capteurs toutes les secondes
    if (millis() - lastTest > 1000) {
        lastTest = millis();

        Serial.println(F("--- Relais ---"));
        Serial.print(F("Relais: ")); Serial.println(r); 
        Serial.println(F("--- Vannes ---"));
        Serial.print(F("PWM Vannes: ")); Serial.print(pwmVal / 2.55); Serial.println(F(" %"));
        Serial.println(F("--- Mesures ---"));
        Serial.print(F("Pression HP: ")); Serial.print(readPressure(pinPressionHP)); Serial.println(F(" bar"));
        Serial.print(F("Pression Entree: ")); Serial.print(readPressure(pinPressionEntree)); Serial.println(F(" bar"));
        Serial.print(F("Pression Saumure: ")); Serial.print(readPressure(pinPressionSaumure)); Serial.println(F(" bar"));
        Serial.print(F("Pression Fresh: ")); Serial.print(readPressure(pinPressionFresh)); Serial.println(F(" bar"));

        Serial.print(F("Debit Entree: ")); Serial.print(readFlow(pinDebitEntree)); Serial.println(F(" L/min"));
        Serial.print(F("Debit MOI: ")); Serial.print(readFlow(pinDebitMOI)); Serial.println(F(" L/min"));
        Serial.print(F("Debit Recirculation: ")); Serial.print(readFlow(pinDebitRecirculation)); Serial.println(F(" L/min"));
        Serial.print(F("Debit Fresh: ")); Serial.print(readFlow(pinDebitFresh)); Serial.println(F(" L/min"));
    }

    // Stepper : un tour avant / arrière toutes les 5 secondes
    if (millis() > (etape + 1) * 5000) {
        if ((etape % 2) == 0) {
            Serial.println(F("Stepper -> Sens horaire"));
            myStepper.step(stepsPerRevolution);
        }
        else {
            Serial.println(F("Stepper -> Sens antihoraire"));
            myStepper.step(-stepsPerRevolution);
        }
        etape++;
    }
}
