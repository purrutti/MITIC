#pragma once
#include <EEPROMex.h>
#include <PID_v1.h>
#include <ModbusRtu.h>
class ModbusSensor {
public:
    union u_tag {
        uint16_t b[2];
        float fval;
    } u;

    float params[4];

    bool querySent = false;
    byte status[5];

    float cond_sensorValue;
    float temp_sensorValue;

    uint16_t data[16];
    modbus_t query;
    ModbusSensor() {}

    ModbusSensor(uint8_t slaveAddress) {
        query.u8id = slaveAddress; // slave address
        query.u8fct = 3; // function code (this one is registers read)
        query.u16RegAdd = 1; // start address in slave
        query.u16CoilsNo = 1; // number of elements (coils or registers) to read
        query.au16reg = data; // pointer to a memory array in the Arduino
        data[0] = 5;
    }

    void setQuery(uint8_t fct, uint16_t RegAdd, uint16_t CoilsNb) {
        query.u8fct = fct; // function code (this one is registers read)
        query.u16RegAdd = RegAdd; // start address in slave
        query.u16CoilsNo = CoilsNb; // number of elements (coils or registers) to read
    }

    void setQueryCond() {
        setQuery(3, 2089, 10);
    }
    void setQueryTemp() {
        setQuery(3, 2409, 10);
    }

    void clearData() {
        for (int i = 0; i < 16; i++) data[i] = 0;
    }

    bool readCond(ModbusRtu* master) {
        setQueryCond();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                u.b[0] = data[2];
                u.b[1] = data[3];
                cond_sensorValue = u.fval;
                querySent = false;
                clearData();
                return 1;
            }
        }
        return 0;
    }

    bool readTemp(ModbusRtu* master) {
        setQueryTemp();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                u.b[0] = data[2];
                u.b[1] = data[3];
                temp_sensorValue = u.fval;
                querySent = false;
                clearData();
                return 1;
            }
        }
        return 0;
    }
};

typedef struct tempo {
    unsigned long debut;
    unsigned long interval;
}tempo;

bool elapsed(tempo* t) {
    if (t->debut == 0) {
        t->debut = millis();
    }
    else {
        if ((unsigned long)(millis() - t->debut) >= t->interval) {
            t->debut = 0;
            return true;
        }
    }
    return false;
}

class Regul {
public:
    double mesure;
    double sortiePID;
    double consigne;
    double Kp;
    double Ki;
    double Kd;
    double sortiePID_pc;
    bool autorisationForcage;
    int consigneForcage;
    double offset;
    int Min, Max;
    PID* pid;
    int startAddress;

    bool useOffset;

    double meanPIDOutput = 255;
    Regul() :
        mesure(0.0),
        sortiePID(0.0),
        consigne(0.0),
        Kp(2.0),
        Ki(3.0),
        Kd(4.0),
        sortiePID_pc(0.0),
        autorisationForcage(false),
        consigneForcage(0),
        offset(0.0),
        Min(0),
        Max(255),
        startAddress(0),
        useOffset(false),
        meanPIDOutput(255.0)
    {
        pid = new PID(&mesure, &sortiePID, &consigne, 1.0, 0.1, 0.0, DIRECT);
        pid->SetMode(AUTOMATIC);
    }
    double compute() {
        // protection si la mesure est invalide
        if (isnan(mesure)) {
            // n'exécutez pas le PID si la mesure est NaN — renvoyez 0 pour sécurité
            sortiePID = 0.0;
            sortiePID_pc = 0.0;
            return sortiePID;
        }

        // exécutez le PID normalement
        pid->Compute();
        sortiePID_pc = (int)(sortiePID * 100.0 / 255.0);
        return sortiePID;
    }
    int save(int startAddress) {
        int add = startAddress;
        EEPROM.updateDouble(add, consigne); add += sizeof(double);
        EEPROM.updateDouble(add, Kp); add += sizeof(double);
        EEPROM.updateDouble(add, Ki); add += sizeof(double);
        EEPROM.updateDouble(add, Kd); add += sizeof(double);
        EEPROM.updateDouble(add, offset); add += sizeof(double);

        EEPROM.updateInt(add, autorisationForcage); add += sizeof(int);
        EEPROM.updateInt(add, consigneForcage); add += sizeof(int);

        EEPROM.updateInt(add, useOffset); add += sizeof(int);
        return add;
    }

    int load(int startAddress) {
        int add = startAddress;
        consigne = EEPROM.readDouble(add); add += sizeof(double);
        Kp = EEPROM.readDouble(add); add += sizeof(double);
        Ki = EEPROM.readDouble(add); add += sizeof(double);
        Kd = EEPROM.readDouble(add); add += sizeof(double);
        offset = EEPROM.readDouble(add); add += sizeof(double);

        autorisationForcage = EEPROM.readInt(add); add += sizeof(int);
        consigneForcage = EEPROM.readInt(add); add += sizeof(int);

        useOffset = EEPROM.readInt(add); add += sizeof(int);
        
        return add;
    }

    void setPID(double kp, double ki, double kd,int _Min, int _Max, int sens) {

        pid->myInput = &mesure;
        pid->myOutput = &sortiePID;
        pid->mySetpoint = &consigne;
        Kp = kp;
        Ki = ki;
        Kd = kd;
        Min = _Min;
        Max = _Max;
        pid->SetControllerDirection(sens);
        pid->SetTunings(Kp, Ki, Kd);
        pid->SetOutputLimits(Min, Max);
        pid->SetMode(AUTOMATIC);
        Serial.println("SET PID");
        Serial.println("KP:" + String(Kp));
        Serial.println("KI:" + String(Ki));
        Serial.println("KD:" + String(Kd));
        Serial.println("setpoint:" + String(consigne));
    }
};