using nanoFramework.Networking;
using BotWaveStatus_ESP32.Utils;
using System;
using System.Diagnostics;
using System.Threading;

namespace BotWaveStatus_ESP32
{
    public class Program
    {
        public static void Main()
        {
            Debug.WriteLine("Welcome to BotWaveStatus_ESP32, dear developer !");
            Debug.WriteLine("Github: https://git.douxx.tech/BotWaveStatus_ESP32/");
            Debug.WriteLine("The program will now start...");


            ScreenManager screen = ScreenManager.Init();
            LedManager? ledManager = null;
            if (Config.USE_LED) ledManager = LedManager.Init();

            screen.Clear();

            // Wifi conn
            screen.StatusPage("CONNECTING", "Joining Wi-Fi network...", 20);

            var result = WifiNetworkHelper.ConnectDhcp(Config.WIFI_SSID, Config.WIFI_PSK);

            if (WifiNetworkHelper.Status != NetworkHelperStatus.NetworkIsReady)
            {
                screen.StatusPage("ERROR", "Wi-Fi Connection Failed", 0);
                Thread.Sleep(Timeout.Infinite);
            }

            screen.StatusPage("BOTWAVE", "Handshaking with server...", 60);
            BotWaveHelper botwave = new BotWaveHelper();

            if (botwave.ConnectToServer())
            {
                // Main loop, update dashboard and handle planned broadcasts
                while (true)
                {
                    long start = 0;
                    if (Config.DEBUG_TIMER)
                    {
                        start = DateTime.UtcNow.Ticks;
                    }

                    if (!botwave.IsConnected())
                    {
                        screen.StatusPage("DISCONNECTED", botwave.DisconnectReason);
                        break;
                    }

                    // check if we need to start a planned broadcast
                    botwave.CheckPlannedBroadcast();

                    ShowDashboard(screen, botwave, ledManager);


                    long elapsed = (DateTime.UtcNow.Ticks - start) / 10000;

                    if (Config.DEBUG_TIMER) Debug.WriteLine($"> Main loop took: {elapsed}ms");

                    long sleep_time = Config.REFRESH_RATE - elapsed;
                    if (sleep_time < 0) sleep_time = 0;

                    Thread.Sleep((int)sleep_time);
                }
            }
            else
            {
                screen.StatusPage("TIMEOUT", botwave.DisconnectReason);
            }


            Debug.WriteLine($"End of program ({botwave.DisconnectReason}). Use RST button or pin to restart.");
            Thread.Sleep(Timeout.Infinite);
        }

        private static void ShowDashboard(ScreenManager screen, BotWaveHelper botwave, LedManager? led)
        {
            screen.Clear();

            var broadcast = botwave.broadcastState;

            switch (broadcast.State)
            {
                default:
                    // nothing
                    screen.StatusPage("CONNECTED", "No active broadcast.");

                    led?.Disable(); // led OFF
                    break;

                case Broadcast.States.Planned:
                    {
                        // cntdwn
                        TimeSpan remaining = broadcast.StartAt - DateTime.UtcNow;

                        int progress = (int)Math.Clamp(100 * (1 - (remaining.TotalSeconds / 60.0)), 0, 100); // basically 0 if >= 60, 100 if 0 

                        if (remaining.TotalSeconds > 0)
                        {
                            screen.StatusPage("PLANNED BROADCAST",
                                $"Starts in: {(int)remaining.TotalMinutes}m {remaining.Seconds}s\nPS: {broadcast.PS}",
                                progress);

                            led?.ToggleLed(); // led blinks
                        }
                        break;
                    }

                case Broadcast.States.Playing:
                case Broadcast.States.Liveing:
                    {
                        // active broadcast
                        string mode = broadcast.State == Broadcast.States.Liveing
                            ? $"LIVE {broadcast.Frequency}MHz"
                            : $"PLAYING {broadcast.Frequency}MHz";

                        string file = broadcast.File != "none" ? $"File: {broadcast.File}" : "";

                        screen.StatusPage(mode,
                            $"PS: {broadcast.PS}\nRT: {broadcast.RT}\n{file}");

                        led?.Enable(); // led ON
                        break;
                    }
            }

            screen.Refresh();
            led?.Refresh();
        }
    }
}