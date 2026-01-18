using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace BotWaveStatus_ESP32
{
    public class Config
    {
        public static string WIFI_SSID = "WIFI_SSID";
        public static string WIFI_PSK = "WIFI_PSK";

        public static string BOTWAVE_SERVER_IP = "BOTWAVE_SERVER_IP";
        public static string BOTWAVE_SERVER_PORT = "BOTWAVE_SERVER_PORT";
        public static string BOTWAVE_PASSKEY = "BOTWAVE_PASSKEY"; // leave empty for no passkey

        public static string MACHINE_HOSTNAME = "MACHINE_HOSTNAME";

        public static string PROTOCOL_VERSION = "2.0.1";

        public static int REFRESH_RATE = 1000;

        // misc
        public static bool USE_LED = false; // if we should use led signals (the led is very bright)
        public static bool DEBUG_TIMER = false; // shows timers in the debug logs, its pratical but floods everything
    }
}
