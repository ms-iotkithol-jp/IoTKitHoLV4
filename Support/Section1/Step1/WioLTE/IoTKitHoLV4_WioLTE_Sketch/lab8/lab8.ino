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
  AlarmInitialize(WIOLTE_D20);
  IoTHubInitialize();
  
  SerialUSB.println("### Setup completed.");
  Wio.LedSetRGB(COLOR_NONE);
}

void loop()
{
  static unsigned long nextMillis = 0;
  do {
    // IoT Hubからの受信を処理
    IoTHubLoop();
  }
  while (millis() < nextMillis);
  
  // 加速度センサーから測定値を取得
  while (!AccelWaitForSampling()) {}
  int x, y, z;
  AccelReadXYZ(&x, &y, &z);           

  // 温湿度センサーから測定値を取得
  float temp, humi;
  TemperatureAndHumidityRead(&temp, &humi);

  // JSON文字列を作成
  char jsonText[100];
  sprintf(jsonText, "{\"accelX\":%.2lf,\"accelY\":%.2lf,\"accelZ\":%.2lf,\"temperature\":%.1f,\"humidity\":%.1f,\"time\":\"%s\"}", AccelValueToGravity(x), AccelValueToGravity(y), AccelValueToGravity(z), temp, humi, TimeNow());
  SerialUSB.println(jsonText);

  // IoT Hubへ送信
  IoTHubSend(jsonText);
  
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
// -WioClient
// -MqttClient
// +IoTHubInitialize()
// +IoTHubSend()
// +IoTHubLoop()
// -IoTHubCallback()

#include <WioLTEClient.h>
#include <PubSubClient.h>    // https://github.com/knolleary/pubsubclient
#include <ArduinoJson.h>        // https://github.com/bblanchon/ArduinoJson

#define MQTT_SERVER_HOST  "beam.soracom.io"
#define MQTT_SERVER_PORT  (1883)

#define DEVICE_ID         "wiolte"
#define D2C_MESSAGE       "devices/"DEVICE_ID"/messages/events/"
#define C2D_MESSAGE       "devices/"DEVICE_ID"/messages/devicebound/#"
#define DTWIN_SUB_RES     "$iothub/twin/res/#"
#define DTWIN_PUB_GET     "$iothub/twin/GET/?$rid=1"
#define DTWIN_SUB_DESIRED "$iothub/twin/PATCH/properties/desired/#"

WioLTEClient WioClient(&Wio);
PubSubClient MqttClient;

void IoTHubInitialize()
{
  SerialUSB.println("### Connecting to MQTT server \""MQTT_SERVER_HOST"\"");
  MqttClient.setServer(MQTT_SERVER_HOST, MQTT_SERVER_PORT);
  MqttClient.setCallback(IoTHubCallback);
  MqttClient.setClient(WioClient);
  if (!MqttClient.connect(DEVICE_ID)) {
    SerialUSB.println("### ERROR! ###");
    return;
  }
  MqttClient.subscribe(C2D_MESSAGE);
  MqttClient.subscribe(DTWIN_SUB_RES);
  MqttClient.subscribe(DTWIN_SUB_DESIRED);
  delay(1000);
  MqttClient.publish(DTWIN_PUB_GET, "");
}

void IoTHubSend(const char* data)
{
  MqttClient.publish(D2C_MESSAGE, data);
}

void IoTHubLoop()
{
  MqttClient.loop();
}

void IoTHubCallback(char* topic, byte* payload, unsigned int length)
{
  SerialUSB.print("Callback -> ");
  SerialUSB.println(topic);
  SerialUSB.print("            ");
  for (int i = 0; i < length; i++) SerialUSB.print((char)payload[i]);
  SerialUSB.println("");

  if (strncmp(topic, C2D_MESSAGE, strlen(C2D_MESSAGE) - 1) == 0) {
    if (length != 7) return;
    if (payload[0] != '#') return;
    
    char message[length + 1];
    memcpy(message, payload, length);
    message[length] = '\0';

    long colorCode = strtol(&message[1], NULL, 16);
    int r = colorCode >> 16 & 0xff;
    int g = colorCode >> 8 & 0xff;
    int b = colorCode >> 0 & 0xff;
    
    Wio.LedSetRGB(r, g, b);
  }
  else if (strncmp(topic, DTWIN_SUB_RES, strlen(DTWIN_SUB_RES) - 1) == 0) {
    StaticJsonBuffer<200> jsonBuffer;
    JsonObject& json = jsonBuffer.parseObject(payload);
    if (!json.success()) return;
    
    int telemetryCycleMs = json["desired"]["telemetry-cycle-ms"];
    SerialUSB.print("telemetry-cycle-ms = ");
    SerialUSB.println(telemetryCycleMs);
    if (telemetryCycleMs >= 100) {
      Interval = telemetryCycleMs;
    }
    
    int alarm = json["desired"]["alarm"];
    SerialUSB.print("alarm = ");
    SerialUSB.println(alarm);
    AlarmSet(alarm);
  }
  else if (strncmp(topic, DTWIN_SUB_DESIRED, strlen(DTWIN_SUB_DESIRED) - 1) == 0) {
    StaticJsonBuffer<200> jsonBuffer;
    JsonObject& json = jsonBuffer.parseObject(payload);
    if (!json.success()) return;
    
    int telemetryCycleMs = json["telemetry-cycle-ms"];
    SerialUSB.print("telemetry-cycle-ms = ");
    SerialUSB.println(telemetryCycleMs);
    if (telemetryCycleMs >= 100) {
      Interval = telemetryCycleMs;
    }
    
    int alarm = json["alarm"];
    SerialUSB.print("alarm = ");
    SerialUSB.println(alarm);
    AlarmSet(alarm);
  }
}

////////////////////////////////////////////////////////////////////////////////
// -AlarmPin
// +AlarmInitialize()
// +AlarmSet()

int AlarmPin;

void AlarmInitialize(int pin)
{
  AlarmPin = pin;
  pinMode(AlarmPin, OUTPUT);
  digitalWrite(AlarmPin, LOW);
}

void AlarmSet(bool on)
{
  digitalWrite(AlarmPin, on ? HIGH : LOW);
}

////////////////////////////////////////////////////////////////////////////////

