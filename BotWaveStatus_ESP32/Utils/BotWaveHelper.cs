using Iot.Device.Ssd13xx.Commands.Ssd1306Commands;
using nanoFramework.Networking;
using nanoFramework.Runtime.Native;
using BotWaveStatus_ESP32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Net.WebSockets.WebSocketFrame;
using System.Text;
using System.Threading;

namespace BotWaveStatus_ESP32.Utils
{

    public class Broadcast
    {
        public enum States
        {
            Playing,
            Liveing,
            Stopped,
            Planned
        }

        public States State = States.Stopped;
        public string PS = "";
        public string RT = "";
        public string File = "";
        public string Frequency = "";
        public DateTime StartAt = DateTime.UtcNow;

    }
    public class BotWaveHelper
    {
        public enum States
        {
            None,
            Connecting,
            Registered,
            Kicked
        }

        private ClientWebSocket ws;
        public States State = States.None;
        public Broadcast broadcastState = new();
        public string DisconnectReason = "Server did not respond in time.";

        public bool ConnectToServer(string? ip = null, string? port = null)
        {
            bool success = false;
            long start = 0;
            if (Config.DEBUG_TIMER)
            {
                start = DateTime.UtcNow.Ticks;
            }

            if (string.IsNullOrEmpty(ip)) ip = Config.BOTWAVE_SERVER_IP;
            if (string.IsNullOrEmpty(port)) port = Config.BOTWAVE_SERVER_PORT;

            ws = new ClientWebSocket();
            ws.SslVerification = System.Net.Security.SslVerification.NoVerification;

            ws.MessageReceived += MessageReceived;

            State = States.Connecting;

            ws.Connect($"wss://{ip}:{port}");

            Hashtable machineInfo = new Hashtable();

            machineInfo["hostname"] = Config.MACHINE_HOSTNAME;
            machineInfo["machine"] = SystemInfo.TargetName;
            machineInfo["system"] = SystemInfo.Version.ToString();
            machineInfo["release"] = "idk";

            SendCommand(Commands.REGISTER, null, machineInfo);

            if (!string.IsNullOrEmpty(Config.BOTWAVE_PASSKEY))
            {
                ArrayList passkey = new ArrayList()
                {
                    Config.BOTWAVE_PASSKEY
                };

                SendCommand(Commands.AUTH, passkey);
            }

            ArrayList version = new ArrayList
            {
                Config.PROTOCOL_VERSION
            };

            SendCommand(Commands.VER, version);
            

            for (int i = 0; i < 50; i++)
            {
                if (State == States.Registered && ws.State == WebSocketState.Open)
                {
                    Debug.WriteLine("Registration successful!");
                    DisconnectReason = "Connection to the server lost."; // storing this if something goes wrong in the future

                    success = true;
                    break;
                } else if (State == States.Kicked)
                {
                    Debug.WriteLine("Something went wrong during auth...");
                }

                Thread.Sleep(100);
            }

            if (!success && State == States.Connecting) Debug.WriteLine("Registration timeout.");

            if (Config.DEBUG_TIMER)
            {
                long elapsed = (DateTime.UtcNow.Ticks - start) / 10000;
                Debug.WriteLine($"> Server connection took: {elapsed}ms");
            }

            return success;
        }

        public bool IsConnected() => ws != null && ws.State == WebSocketState.Open && State == States.Registered;

        public void CheckPlannedBroadcast()
        {
            if (broadcastState.State == Broadcast.States.Planned)
            {
                if (DateTime.UtcNow >= broadcastState.StartAt)
                {
                    broadcastState.State = Broadcast.States.Playing;
                    Debug.WriteLine("Planned broadcast is now starting!");
                }
            }
        }

        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            long start = 0;
            if (Config.DEBUG_TIMER)
            {
                start = DateTime.UtcNow.Ticks;
            }

            if (e.Frame.MessageType == WebSocketMessageType.Text)
            {
                string rawLine = Encoding.UTF8.GetString(e.Frame.Buffer, 0, e.Frame.Buffer.Length);

                CommandData data = CommandParser.ParseCommand(rawLine);

                HandleLogic(data);
            }

            if (Config.DEBUG_TIMER)
            {
                long elapsed = (DateTime.UtcNow.Ticks - start) / 10000;
                Debug.WriteLine($"> Webbook message processing took: {elapsed}ms");
            }
        }

        private void HandleLogic(CommandData data)
        {
            Debug.WriteLine($"Command recieved: {data.Command}");

            switch (data.Command)
            {
                default:
                    {
                        Hashtable message = new();
                        message["message"] = $"The command {data.Command} is not implemented :/";
                        SendCommand(Commands.ERROR, null, message);
                        break;
                    }

                case Commands.PING:
                    {
                        SendCommand(Commands.PONG);
                        break;
                    }

                case Commands.LIST_FILES:
                    {
                        Hashtable files = new();
                        files["files"] = "{}";
                        SendCommand(Commands.OK, null, files);
                        break;
                    }

                case Commands.START:
                    {
                        broadcastState.PS = (string)data.Kwargs["ps"] ?? "Unknown";
                        broadcastState.RT = (string)data.Kwargs["rt"] ?? "Unknown";
                        broadcastState.File = (string)data.Kwargs["filename"] ?? "Unknown";
                        broadcastState.Frequency = (string)data.Kwargs["frequency"] ?? "90.0";

                        string startAtStr = (string)data.Kwargs["start_at"];
                        long unixSeconds = 0;

                        if (startAtStr != null && startAtStr.Length > 0)
                        {
                            try
                            {
                                double parsedDouble = double.Parse(startAtStr);
                                unixSeconds = (long)parsedDouble;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("Timestamp parse failed: " + ex.Message);
                                unixSeconds = 0;
                            }
                        }

                        if (unixSeconds <= 0)
                        {
                            broadcastState.StartAt = DateTime.UtcNow;
                        }
                        else
                        {
                            DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                            broadcastState.StartAt = new DateTime(unixEpoch.Ticks + (unixSeconds * 10000000));
                        }

                        broadcastState.State = Broadcast.States.Planned;

                        Debug.WriteLine(
                            $"Broadcast scheduled for: {broadcastState.StartAt:yyyy-MM-dd HH:mm:ss}"
                        );

                        Hashtable message = new();
                        message["message"] = "Start acknowledged";
                        SendCommand(Commands.OK, null, message);

                        break;
                    }

                case Commands.STOP:
                    {
                        broadcastState.State = Broadcast.States.Stopped;
                        Hashtable message = new();
                        message["message"] = "Stop acknowledged";
                        SendCommand(Commands.OK, null, message);

                        break;
                    }

                case Commands.STREAM_TOKEN:
                    {
                        broadcastState.PS = (string)data.Kwargs["ps"] ?? "Unknown";
                        broadcastState.RT = (string)data.Kwargs["rt"] ?? "Unknown";
                        broadcastState.File = "none";
                        broadcastState.Frequency = (string)data.Kwargs["frequency"] ?? "90.0";

                        broadcastState.State = Broadcast.States.Liveing;

                        Hashtable message = new();
                        message["message"] = "Live acknowledged";
                        SendCommand(Commands.OK, null, message);
                        break;
                    }

                case Commands.KICK:
                    {
                        string reason = (string)data.Kwargs["reason"] ?? "Kicked by administrator";
                        Debug.WriteLine(reason);
                        State = States.Kicked;
                        DisconnectReason = reason;
                        break;
                    }

                case Commands.VERSION_MISMATCH:
                    {
                        DisconnectReason = "Protocol version mismatch.";
                        State = States.Kicked;
                        break;
                    }

                case Commands.AUTH_FAILED:
                    {
                        DisconnectReason = "Incorrect passkey.";
                        State = States.Kicked;
                        break;
                    }

                case Commands.REGISTER_OK:
                    {
                        State = States.Registered;
                        break;
                    }
            }

        }

        public void SendCommand(string command, ArrayList args = null, Hashtable kwargs = null)
        {
            if (ws != null && ws.State == WebSocketState.Open)
            {
                try
                {
                    string message = CommandParser.BuildCommand(command, args, kwargs);

                    ws.SendString(message);

                    Debug.WriteLine(">>> Sent: " + message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Send failed: " + ex.Message);
                }
            }
            else
            {
                Debug.WriteLine("Cannot send: Client is disconnected.");
            }
        }
    }
}
