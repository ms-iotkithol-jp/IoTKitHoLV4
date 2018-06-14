#include <WioLTEforArduino.h>
#include <stdio.h>

#define COLOR_SETUP      0, 10,  0  // Green
#define COLOR_MEASURE    0,  0, 10  // Blue
#define COLOR_SEND      10,  0,  0  // Red
#define COLOR_NONE       0,  0,  0  // None

WioLTE Wio;
unsigned long Interval = 3000;

////////////////////////////////////////////////////////////////////////////////
// +setup()
// +loop()

void setup()
{
  delay(200);
  
  SerialUSB.println("");
  SerialUSB.println("--- START ---------------------------------------------------");
  
  SerialUSB.println("### I/O Initialize.");
  Wio.Init();
  Wio.LedSetRGB(COLOR_SETUP);
  
  SerialUSB.println("### Power supply ON.");
  Wio.PowerSupplyGrove(true);
  Wio.PowerSupplyLTE(true);
  delay(500);

  SerialUSB.println("### Turn on or reset.");
  if (!Wio.TurnOnOrReset()) {
    SerialUSB.println("### ERROR! ###");
    return;
  }
  
  SerialUSB.println("### Connecting to \"soracom.io\".");
  if (!Wio.Activate("soracom.io", "sora", "sora")) {
    SerialUSB.println("### ERROR! ###");
    return;
  }
  
  SerialUSB.println("### Module Initialize.");
  TimeInitialize();
  AccelInitialize(16, 800);
  TemperatureAndHumidityBegin(WIOLTE_D38);
  
  SerialUSB.println("### Setup completed.");
  Wio.LedSetRGB(COLOR_NONE);
}

void loop()
{
  static unsigned long nextMillis = 0;
  while (millis() < nextMillis) {}
  
  // 加速度センサーから測定値を取得
  Wio.LedSetRGB(COLOR_MEASURE);
  while (!AccelWaitForSampling()) {}
  int x, y, z;
  AccelReadXYZ(&x, &y, &z);           
  Wio.LedSetRGB(COLOR_NONE);

  // 温湿度センサーから測定値を取得
  float temp, humi;
  TemperatureAndHumidityRead(&temp, &humi);

  // JSON文字列を作成
  char jsonText[100];
  sprintf(jsonText, "{\"accelX\":%.2lf,\"accelY\":%.2lf,\"accelZ\":%.2lf,\"temperature\":%.1f,\"humidity\":%.1f,\"time\":\"%s\"}", AccelValueToGravity(x), AccelValueToGravity(y), AccelValueToGravity(z), temp, humi, TimeNow());
  SerialUSB.println(jsonText);

  nextMillis = millis() + Interval;
}

////////////////////////////////////////////////////////////////////////////////
// +TimeInitialize()
// +TimeNow()

time_t StartTime;
unsigned long StartMillis;

void TimeInitialize()
{
  Wio.SyncTime("ntp.nict.jp");
  
  struct tm now;
  Wio.GetTime(&now);
  StartTime = mktime(&now) + 9 * 60 * 60;
  StartMillis = millis();
}

const char* TimeNow()
{
  static char nowTimeFullStr[100];
  
  unsigned long nowMillis = millis();

  time_t nowTime = StartTime + nowMillis / 1000;
  unsigned long nowTimeMs = nowMillis % 1000;
  
  char nowTimeStr[100];
  strftime(nowTimeStr, sizeof (nowTimeStr), "%Y-%m-%dT%H:%M:%S", localtime(&nowTime));
  sprintf(nowTimeFullStr, "%s.%03lu", nowTimeStr, nowTimeMs);

  return nowTimeFullStr;
}

////////////////////////////////////////////////////////////////////////////////
// -Accel
// +AccelInitialize()
// +AccelWaitForSampling()
// +AccelReadXYZ()
// +AccelValueToGravity()

#include <ADXL345.h>          // https://github.com/Seeed-Studio/Accelerometer_ADXL345

ADXL345 Accel;

void AccelInitialize(int range, int samplingFrequency)
{
  Accel.powerOn();
  Accel.setRangeSetting(range);
  Accel.setRate(samplingFrequency);
}

bool AccelWaitForSampling()
{
  return Accel.getInterruptSource(ADXL345_DATA_READY);
}

void AccelReadXYZ(int* x, int* y, int* z)
{
  Accel.readXYZ(x, y, z);
}

double AccelValueToGravity(int val)
{
  return (double)val * 16 / 512;
}

////////////////////////////////////////////////////////////////////////////////////////
// -TemperatureAndHumidityPin
// +TemperatureAndHumidityBegin()
// +TemperatureAndHumidityRead()
// -DHT11Init()
// -DHT11Start()
// -DHT11ReadByte()
// -DHT11Finish()
// -DHT11Check()

int TemperatureAndHumidityPin;

void TemperatureAndHumidityBegin(int pin)
{
  TemperatureAndHumidityPin = pin;
  DHT11Init(TemperatureAndHumidityPin);
}

bool TemperatureAndHumidityRead(float* temperature, float* humidity)
{
  byte data[5];
  
  DHT11Start(TemperatureAndHumidityPin);
  for (int i = 0; i < 5; i++) data[i] = DHT11ReadByte(TemperatureAndHumidityPin);
  DHT11Finish(TemperatureAndHumidityPin);
  
  if(!DHT11Check(data, sizeof (data))) return false;
  if (data[1] >= 10) return false;
  if (data[3] >= 10) return false;

  *humidity = (float)data[0] + (float)data[1] / 10.0f;
  *temperature = (float)data[2] + (float)data[3] / 10.0f;

  return true;
}

void DHT11Init(int pin)
{
  digitalWrite(pin, HIGH);
  pinMode(pin, OUTPUT);
}

void DHT11Start(int pin)
{
  // Host the start of signal
  digitalWrite(pin, LOW);
  delay(18);
  
  // Pulled up to wait for
  pinMode(pin, INPUT);
  while (!digitalRead(pin)) ;
  
  // Response signal
  while (digitalRead(pin)) ;
  
  // Pulled ready to output
  while (!digitalRead(pin)) ;
}

byte DHT11ReadByte(int pin)
{
  byte data = 0;
  
  for (int i = 0; i < 8; i++) {
    while (digitalRead(pin)) ;

    while (!digitalRead(pin)) ;
    unsigned long start = micros();

    while (digitalRead(pin)) ;
    unsigned long finish = micros();

    if ((unsigned long)(finish - start) > 50) data |= 1 << (7 - i);
  }
  
  return data;
}

void DHT11Finish(int pin)
{
  // Releases the bus
  while (!digitalRead(pin)) ;
  digitalWrite(pin, HIGH);
  pinMode(pin, OUTPUT);
}

bool DHT11Check(const byte* data, int dataSize)
{
  if (dataSize != 5) return false;

  byte sum = 0;
  for (int i = 0; i < dataSize - 1; i++) {
    sum += data[i];
  }

  return data[dataSize - 1] == sum;
}

////////////////////////////////////////////////////////////////////////////////

