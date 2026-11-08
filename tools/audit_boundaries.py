#!/usr/bin/env python3
"""Donanım sınırını denetler.

Bu sürüm GPIO, SPI, ADC, Wi-Fi veya panel sürücüsü başlatmamalıdır.
Arduino.h yalnızca cihaz girişinde durur.
"""

from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
FORBIDDEN = (
    "pinMode",
    "digitalWrite",
    "digitalRead",
    "analogRead",
    "analogWrite",
    "gpio_set",
    "gpio_config",
    "ledc",
    "Wire.begin",
    "SPI.begin",
    "WiFi.begin",
    "WiFi.mode",
    "esp_wifi",
    "Preferences",
    "EEPROM",
    "TFT_eSPI",
    "LovyanGFX",
    "OneWire",
    "DallasTemperature",
)
errors = []
for folder in (ROOT / "src", ROOT / "include"):
    for path in folder.rglob("*"):
        if path.suffix not in {".cpp", ".hpp", ".h"}:
            continue
        text = path.read_text(encoding="utf-8")
        for token in FORBIDDEN:
            if token in text:
                errors.append(f"{path.relative_to(ROOT)} içinde {token}")
        if "Arduino.h" in text and path.name != "main.cpp":
            errors.append(f"{path.relative_to(ROOT)} Arduino.h içeriyor")

if errors:
    print("\n".join(errors))
    sys.exit(1)
print("donanım sınırı temiz")
