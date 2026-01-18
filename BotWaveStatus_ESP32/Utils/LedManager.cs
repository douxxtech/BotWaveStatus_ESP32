using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Text;

namespace BotWaveStatus_ESP32.Utils
{
    public class LedManager
    {
        private readonly GpioPin led;

        public bool LedActive { get; private set; }

        private LedManager(GpioPin led)
        {
            this.led = led;
        }

        public static LedManager Init()
        {
            GpioController controller = new GpioController();

            return new LedManager(controller.OpenPin(35, PinMode.Output));
        }

        public void Refresh() => led.Write(LedActive ? PinValue.High : PinValue.Low);
        public void ToggleLed() => LedActive = !LedActive;
        public void Enable() => LedActive = true;
        public void Disable() => LedActive = false;
    }
}
