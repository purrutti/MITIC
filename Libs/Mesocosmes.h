#pragma once

#define MESO 

class Mesocosme {
public:

    uint8_t _pinDebitmetre;
    uint8_t _pinNiveauH;
    uint8_t _pinNiveauL;
    uint8_t _pinNiveauLL;

    uint8_t _mesocosmeIndex;

    bool alarmeNiveauHaut;
    bool alarmeNiveauBas;
    bool alarmeNiveauTresBas;

    float debit;
    float temperature;
    float pH;

    Mesocosme() {
    };
    Mesocosme(uint8_t mesocosmeIndex) {
        _mesocosmeIndex = mesocosmeIndex;
    };
    Mesocosme(uint8_t pinDebitmetre, uint8_t pinNiveauH, uint8_t pinNiveauL, uint8_t pinNiveauLL, uint8_t mesocosmeIndex) {
        _pinDebitmetre = pinDebitmetre;
        _pinNiveauH = pinNiveauH;
        _pinNiveauL = pinNiveauL;
        _pinNiveauLL = pinNiveauLL;
        _mesocosmeIndex = mesocosmeIndex;
    };

    bool checkLevel() {
        alarmeNiveauHaut = digitalRead(_pinNiveauH) ? true : false;
        alarmeNiveauBas = digitalRead(_pinNiveauL) ? false : true;
        alarmeNiveauTresBas = digitalRead(_pinNiveauLL) ? false : true;
        return(alarmeNiveauHaut || alarmeNiveauBas || alarmeNiveauTresBas);
    }

    float readFlow(int lissage) {
        
        int ana = analogRead(_pinDebitmetre); // 0-1023 value corresponding to 0-5 V corresponding to 0-20 mA
        int mA = map(ana, 0, 1023, 0, 2000); //map to milli amps with 2 extra digits
        double ancientDebit = debit;
        debit = (0.625 * (mA - 400)) / 100.0; // flowrate in l/mn
        debit = (lissage * debit + (100.0 - lissage) * ancientDebit)/100.0;
        return debit;
    }

};