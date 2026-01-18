using Iot.Device.Ssd13xx;
using nanoFramework.Hardware.Esp32;
using System;
using System.Device.Gpio;
using System.Device.I2c;
using System.Diagnostics;
using System.Threading;

namespace BotWaveStatus_ESP32.Utils
{
    public class ScreenManager
    {
        private readonly Ssd1306 driver;
        public const int Width = 128;
        public const int Height = 64;

        private int FontWidth => driver.Font.Width;
        private int FontHeight => driver.Font.Height;

        private ScreenManager(Ssd1306 driver)
        {
            this.driver = driver;
        }

        public static ScreenManager Init()
        {
            long start = 0;
            if (Config.DEBUG_TIMER)
            {
                start = DateTime.UtcNow.Ticks;
            }

            Configuration.SetPinFunction(17, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(18, DeviceFunction.I2C1_CLOCK);

            GpioController controller = new GpioController();

            // Vext Power (Specific to some Heltec/ESP32 boards)
            GpioPin vext = controller.OpenPin(36, PinMode.Output);
            vext.Write(PinValue.Low);

            // Reset sequence
            GpioPin oledReset = controller.OpenPin(21, PinMode.Output);
            oledReset.Write(PinValue.Low);
            Thread.Sleep(20);
            oledReset.Write(PinValue.High);
            Thread.Sleep(100);

            I2cConnectionSettings i2cSettings = new I2cConnectionSettings(1, Ssd1306.DefaultI2cAddress);
            I2cDevice i2c = I2cDevice.Create(i2cSettings);

            var driver = new Ssd1306(i2c, Ssd13xx.DisplayResolution.OLED128x64);
            driver.Font = new BasicFont();

            if (Config.DEBUG_TIMER)
            {
                long elapsed = (DateTime.UtcNow.Ticks - start) / 10000;
                Debug.WriteLine($"> Screen init took: {elapsed}ms");
            }

            return new ScreenManager(driver);
        }

        public void Clear() => DrawFilledRect(0, 0, Width, Height, false);

        public void Refresh() => driver.Display();

        public void DrawWrappedText(int x, int y, string text, int spacing = 2)
        {
            string[] lines = text.Split('\n');
            int currentY = y;

            foreach (var line in lines)
            {
                string[] words = line.Split(' ');
                string currentLine = "";

                foreach (var word in words)
                {
                    string testLine = currentLine == "" ? word : currentLine + " " + word;
                    int testLineWidth = testLine.Length * FontWidth;

                    if (x + testLineWidth > Width)
                    {
                        driver.DrawString(x, currentY, currentLine, 1);
                        currentY += FontHeight + spacing;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = testLine;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    driver.DrawString(x, currentY, currentLine, 1);
                    currentY += FontHeight + spacing;
                }
            }
        }

        public void DrawCenteredText(int y, string text, bool color = true)
        {
            int textWidth = text.Length * FontWidth;
            int x = (Width - textWidth) / 2;
            if (x < 0) x = 0;
            driver.DrawString(x, y, text, (byte)(color ? 1 : 0));
        }

        public void DrawSignalIcon(int x, int y)
        {
            DrawFilledRect(x, y + 4, 2, 2, true);
            DrawFilledRect(x + 3, y + 2, 2, 4, true);
            DrawFilledRect(x + 6, y, 2, 6, true);
        }

        public void StatusPage(string title, string description, int progress = -1)
        {
            Clear();

            DrawCenteredText(2, title);
            DrawFilledRect(0, 11, 128, 1, true);

            DrawWrappedText(2, 15, description);

            if (progress >= 0)
            {
                DrawProgressBar(52, progress);
            }

            Refresh();
        }

        public void DrawProgressBar(int y, int percent)
        {
            int margin = 10;
            int barWidth = Width - (margin * 2);
            int barHeight = 8;
            int fillWidth = (barWidth * percent) / 100;

            DrawRect(margin, y, barWidth, barHeight);
            DrawFilledRect(margin + 2, y + 2, fillWidth - 4 > 0 ? fillWidth - 4 : 0, barHeight - 4, true);
        }

        public void DrawRect(int x, int y, int w, int h)
        {
            for (int i = x; i < x + w; i++)
            {
                driver.DrawPixel(i, y, true);
                driver.DrawPixel(i, y + h - 1, true);
            }
            for (int i = y; i < y + h; i++)
            {
                driver.DrawPixel(x, i, true);
                driver.DrawPixel(x + w - 1, i, true);
            }
        }

        public void DrawFilledRect(int x, int y, int w, int h, bool color)
        {
            for (int i = x; i < x + w; i++)
                for (int j = y; j < y + h; j++)
                    driver.DrawPixel(i, j, color);
        }
    }
}