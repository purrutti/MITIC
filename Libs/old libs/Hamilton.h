#pragma once
#define HAMILTON

class ModbusSensorHamilton {
public:
    union u_tag {
        uint16_t b[2];
        float fval;
    } u;

    bool querySent;

    uint16_t data[10];
    /*uint16_t dataPH[10];
    uint16_t dataTemp[10];
    uint16_t dataLevel[4];
    uint16_t dataCalibrationCommand[2];
    uint16_t dataCalibrationValue[2];
    uint16_t dataCalibrationStatus[6];*/

    modbus_t query;
    /*modbus_t queryPH;
    modbus_t queryTemp;
    modbus_t querySetLevel;
    modbus_t queryCalibrationCommand;
    modbus_t queryCalibrationValue;
    modbus_t queryCalibrationStatus;*/

    ModbusSensorHamilton() {
    };

    void setSensor(uint8_t slaveAddress, ModbusRtu* m) {
        query.u8id = slaveAddress; // slave address
        query.u8fct = 3; // function code (this one is registers read)
        query.u16RegAdd = 2089; // start address in slave
        query.u16CoilsNo = 10; // number of elements (coils or registers) to read
        query.au16reg = data; // pointer to a memory array in the Arduino


        /*queryPH.u8id = slaveAddress; // slave address
        queryPH.u8fct = 3; // function code (this one is registers read)
        queryPH.u16RegAdd = 2089; // start address in slave
        queryPH.u16CoilsNo = 10; // number of elements (coils or registers) to read
        queryPH.au16reg = dataPH; // pointer to a memory array in the Arduino

        queryTemp.u8id = slaveAddress; // slave address
        queryTemp.u8fct = 3; // function code (this one is registers read)
        queryTemp.u16RegAdd = 2409; // start address in slave
        queryTemp.u16CoilsNo = 10; // number of elements (coils or registers) to read
        queryTemp.au16reg = dataTemp; // pointer to a memory array in the Arduino

        querySetLevel.u8id = slaveAddress; // slave address
        querySetLevel.u8fct = 16; // function code (this one is registers read)
        querySetLevel.u16RegAdd = 4287; // start address in slave
        querySetLevel.u16CoilsNo = 4; // number of elements (coils or registers) to read
        querySetLevel.au16reg = dataLevel; // pointer to a memory array in the Arduino
        dataLevel[0] = 48;
        dataLevel[1] = 0;
        dataLevel[2] = 31182;
        dataLevel[3] = 244;

        queryCalibrationCommand.u8id = slaveAddress; // slave address
        queryCalibrationCommand.u8fct = 16; // function code (this one is registers read)
        queryCalibrationCommand.u16RegAdd = 5339; // start address in slave
        queryCalibrationCommand.u16CoilsNo = 2; // number of elements (coils or registers) to read
        queryCalibrationCommand.au16reg = dataCalibrationCommand; // pointer to a memory array in the Arduino

        queryCalibrationValue.u8id = slaveAddress; // slave address
        queryCalibrationValue.u8fct = 16; // function code (this one is registers read)
        queryCalibrationValue.u16RegAdd = 5321; // start address in slave
        queryCalibrationValue.u16CoilsNo = 2; // number of elements (coils or registers) to read
        queryCalibrationValue.au16reg = dataCalibrationValue; // pointer to a memory array in the Arduino

        queryCalibrationStatus.u8id = slaveAddress; // slave address
        queryCalibrationStatus.u8fct = 3; // function code (this one is registers read)
        queryCalibrationStatus.u16RegAdd = 5317; // start address in slave
        queryCalibrationStatus.u16CoilsNo = 6; // number of elements (coils or registers) to read
        queryCalibrationStatus.au16reg = dataCalibrationStatus; // pointer to a memory array in the Arduino*/

        master = m;
        querySent = false;
    }
    float pH_sensorValue;
    float temp_sensorValue;

    ModbusRtu* master;

    void setQuery(uint8_t fct, uint16_t RegAdd, uint16_t CoilsNb) {
        query.u8fct = fct; // function code (this one is registers read)
        query.u16RegAdd = RegAdd; // start address in slave
        query.u16CoilsNo = CoilsNb; // number of elements (coils or registers) to read
    }
    void setQuerypH() {
        setQuery(3, 2089, 10);
    }
    void setQueryTemp() {
        setQuery(3, 2409, 10);
    }
    void setQuerySetLevel() {
        setQuery(16, 4287, 4);
        query.au16reg[0] = 48;
        query.au16reg[1] = 0;
        query.au16reg[2] = 31182;
        query.au16reg[3] = 244;
    }
    void setQueryCalibrationCommand() {
        setQuery(16, 5339, 2);
    }
    void setQueryCalibrationValue() {
        setQuery(16, 5321, 2);
    }
    void setQueryCalibrationStatus() {
        setQuery(16, 5317, 6);
    }

    bool setLevel()
    {
        setQuerySetLevel();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool readPH()
    {
        setQuerypH();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                u.b[0] = data[2];
                u.b[1] = data[3];
                if (u.fval < 9)                pH_sensorValue = u.fval;
                querySent = false;
                return 1;

            }
        }

        return 0;

    }

    bool readTemp()
    {
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
                return 1;
            }
        }

        return 0;

    }
    /*
    int cmd: 1 = initial measurment
    2 = cancel an active calbration
    3 = restore standard calbration
    4 = restore product calibration*/
    bool sendCalibrationCommand(int cmd)
    {
        setQueryCalibrationCommand();
        data[0] = cmd;
        data[1] = 0;
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool sendCalibrationValue(float value)
    {
        setQueryCalibrationValue();
        u.fval = value;
        data[0] = u.b[0];
        data[1] = u.b[1];
        //Serial.print("calb value:"); Serial.print(dataCalibrationValue[0]); Serial.print(","); Serial.print(dataCalibrationValue[1]);
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                return 1;
            }
        }
        return 0;
    }

    bool getCalibrationStatus()
    {
        setQueryCalibrationStatus();
        if (!querySent) {
            master->query(query);
            querySent = true;
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                for (int i = 0; i < 6; i++) {
                    Serial.print("status "); Serial.print(i); Serial.print(":"); Serial.println(data[i]);
                }
                return 1;
            }
        }
        return 0;
    }

    int calibrate(float value, int step) {
        Serial.println(F("CALIB HAMILTON"));
        switch (step) {
        case 0:
            //PAS NECESSAIRE??
            if (setLevel()) {
                step++;
                Serial.println(F("set Level S OK"));
            }
            break;
        case 1:
            if (sendCalibrationCommand(1)) {//1 = request calibration; 2 = cancel calibration
                step++;
                Serial.println(F("send calib OK"));
            }
            break;
        case 2:
            if (sendCalibrationValue(value)) {
                step++;
                Serial.println(F("send calib value OK"));
            }
            break;
        case 3:
            if (getCalibrationStatus()) {
                step++;
                Serial.println(F("get status OK"));
            }
            break;
        }
        return step;
    }

    bool factoryReset()
    {
        Serial.println(F("factory reset"));
        setQuery(16, 8191, 2);
        data[0] = 911;

        if (!querySent) {
            master->query(query);
            querySent = true;
            Serial.println(F("query sent"));
        }
        else {
            master->poll();
            if (master->getState() == COM_IDLE) {
                querySent = false;
                Serial.println(F("query OK"));
                return 1;
            }
        }
        return 0;
    }


};