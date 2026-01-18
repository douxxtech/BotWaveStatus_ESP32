using System;
using System.Collections;
using System.Diagnostics;
using System.Text;

namespace BotWaveStatus_ESP32.Utils
{
    public class Commands
    {
        public const string AUTH = "AUTH";
        public const string VER = "VER";
        public const string REGISTER = "REGISTER";
        public const string PING = "PING";
        public const string PONG = "PONG";
        public const string START = "START";
        public const string STOP = "STOP";
        public const string UPLOAD_TOKEN = "UPLOAD_TOKEN";
        public const string DOWNLOAD_TOKEN = "DOWNLOAD_TOKEN";
        public const string DOWNLOAD_URL = "DOWNLOAD_URL";
        public const string STREAM_TOKEN = "STREAM_TOKEN";
        public const string KICK = "KICK";
        public const string LIST_FILES = "LIST_FILES";
        public const string REMOVE_FILE = "REMOVE_FILE";
        public const string OK = "OK";
        public const string ERROR = "ERROR";
        public const string REGISTER_OK = "REGISTER_OK";
        public const string AUTH_FAILED = "AUTH_FAILED";
        public const string VERSION_MISMATCH = "VERSION_MISMATCH";
    }

    public class CommandData
    {
        public string Command { get; set; }
        public ArrayList Args { get; set; } = new ArrayList();
        public Hashtable Kwargs { get; set; } = new Hashtable();
    }

    public static class CommandParser
    {
        public static CommandData ParseCommand(string line)
        {
            long start = 0;
            if (Config.DEBUG_TIMER)
            {
                start = DateTime.UtcNow.Ticks;
            }

            var result = new CommandData();
            if (line == null || line.Length == 0)
                return result;

            var tokens = ShlexSplit(line);
            if (tokens.Length == 0)
                return result;

            result.Command = tokens[0].ToUpper();

            for (int i = 1; i < tokens.Length; i++)
            {
                if (tokens[i].IndexOf('=') != -1)
                {
                    var parts = tokens[i].Split('=');
                    if (parts.Length >= 2)
                    {
                        result.Kwargs[parts[0]] = parts[1];
                    }
                }
                else
                {
                    result.Args.Add(tokens[i]);
                }
            }

            if (Config.DEBUG_TIMER)
            {
                long elapsed = (DateTime.UtcNow.Ticks - start) / 10000;
                Debug.WriteLine($"> Command parsing took: {elapsed}ms");
            }

            return result;
        }

        private static string[] ShlexSplit(string line) // thanks chatgpt
        {
            ArrayList tokens = new ArrayList();
            StringBuilder current = new StringBuilder();
            bool inQuotes = false;
            char quoteChar = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == quoteChar)
                    {
                        inQuotes = false;
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '\'' || c == '"')
                    {
                        inQuotes = true;
                        quoteChar = c;
                    }
                    else if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                    {
                        if (current.Length > 0)
                        {
                            tokens.Add(current.ToString());
                            current.Clear();
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            string[] resultArray = new string[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
                resultArray[i] = (string)tokens[i];

            return resultArray;
        }

        public static string BuildCommand(string command, ArrayList args, Hashtable kwargs)
        {
            StringBuilder parts = new StringBuilder();
            parts.Append(command.ToUpper());

            if (args != null)
            {
                foreach (object arg in args)
                {
                    string argStr = arg.ToString();
                    parts.Append(' ');
                    parts.Append(QuoteIfNeeded(argStr));
                }
            }

            if (kwargs != null)
            {
                foreach (object keyObj in kwargs.Keys)
                {
                    string key = keyObj.ToString();
                    string value = kwargs[key].ToString();
                    parts.Append(' ');
                    parts.Append(key);
                    parts.Append('=');
                    parts.Append(QuoteIfNeeded(value));
                }
            }

            return parts.ToString();
        }

        public static void ParseResponse(string line, out string status, out string message)
        {
            CommandData parsed = ParseCommand(line);
            status = parsed.Command;
            message = parsed.Kwargs.Contains("message") ? parsed.Kwargs["message"].ToString() : "";
        }

        public static string BuildResponse(string status, string message = "")
        {
            if (message == null || message.Length == 0)
                return status.ToUpper();

            Hashtable kwargs = new Hashtable();
            kwargs["message"] = message;
            return BuildCommand(status, null, kwargs);
        }

        private static string QuoteIfNeeded(string s)
        {
            if (s.IndexOf(' ') >= 0 || s.IndexOf('\'') >= 0 || s.IndexOf('"') >= 0)
            {
                StringBuilder sb = new StringBuilder("'");
                for (int i = 0; i < s.Length; i++)
                {
                    if (s[i] == '\'') sb.Append("\\'");
                    else sb.Append(s[i]);
                }
                sb.Append("'");
                return sb.ToString();
            }
            return s;
        }
    }
}