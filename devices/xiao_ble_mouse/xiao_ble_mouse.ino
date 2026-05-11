#include <Arduino.h>
#include <BleMouse.h>

namespace
{
#ifdef D2
constexpr uint8_t SwitchPin = D2;
#else
constexpr uint8_t SwitchPin = 3;
#endif
constexpr unsigned long DebounceMs = 20;

BleMouse bleMouse("Shadow Switch Mouse", "Oomiya Fes", 100);

bool stablePressed = false;
bool lastRawPressed = false;
bool lastSentPressed = false;
bool lastConnected = false;
unsigned long lastRawChangeMs = 0;

bool readSwitchPressed()
{
    return digitalRead(SwitchPin) == LOW;
}

void releaseIfNeeded()
{
    if (lastSentPressed && bleMouse.isConnected())
    {
        bleMouse.release(MOUSE_LEFT);
        Serial.println("released");
    }
    lastSentPressed = false;
}

void sendPressed()
{
    if (!lastSentPressed && bleMouse.isConnected())
    {
        bleMouse.press(MOUSE_LEFT);
        Serial.println("pressed");
        lastSentPressed = true;
    }
}
}

void setup()
{
    Serial.begin(115200);
    pinMode(SwitchPin, INPUT_PULLUP);

    lastRawPressed = readSwitchPressed();
    stablePressed = lastRawPressed;
    lastRawChangeMs = millis();

    Serial.println("Starting Shadow Switch Mouse");
    Serial.println("Pair the BLE device named: Shadow Switch Mouse");
    bleMouse.begin();
}

void loop()
{
    const bool connected = bleMouse.isConnected();
    if (connected != lastConnected)
    {
        Serial.println(connected ? "connected" : "disconnected");
        if (!connected)
        {
            lastSentPressed = false;
        }
        else
        {
            bleMouse.release(MOUSE_LEFT);
            lastSentPressed = false;
            if (stablePressed)
            {
                sendPressed();
            }
        }
        lastConnected = connected;
    }

    const bool rawPressed = readSwitchPressed();
    const unsigned long now = millis();

    if (rawPressed != lastRawPressed)
    {
        lastRawPressed = rawPressed;
        lastRawChangeMs = now;
    }

    if ((now - lastRawChangeMs) >= DebounceMs && rawPressed != stablePressed)
    {
        stablePressed = rawPressed;
        if (stablePressed)
        {
            sendPressed();
        }
        else
        {
            releaseIfNeeded();
        }
    }

    if (!connected)
    {
        delay(20);
        return;
    }

    delay(1);
}
