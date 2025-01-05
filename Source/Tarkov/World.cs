using Offsets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace eft_dma_radar
{
    public class World
    {
        private ulong TOD_Sky;
        private ulong WeatherController;
        private ulong TOD_Components;
        private ulong TOD_CycleParameters;
        private ulong TOD_DayParameters;
        private ulong TOD_NightParameters;
        private ulong TOD_SunParameters;
        private ulong TOD_MoonParameters;
        private ulong TOD_AmbientParameters;

        private ulong TOD_Time;
        private ulong GameDateTime;

        private ulong WeatherControllerDebug;

        private int _timeOfDay = -1;
        private bool _timeFrozen = false;
        private bool _removeSun = false;
        private bool _removeMoon = false;
        private bool _removeShadows = false;

        private bool _removeClouds = false;
        private bool _removeFog = false;
        private bool _removeRain = false;
        private bool _debugWeather = false;

        private bool _updateIntervalSet = false;
        private bool _dayLightSet = false;
        private bool _nightLightSet = false;
        private int _dayLightIntensity = -1;
        private int _nightLightIntensity = -1;

        public World()
        {

        }

        #region WeatherController
        public bool InitializeWeatherController()
        {
            this.WeatherController = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "EFT.Weather.WeatherController");
            this.WeatherControllerDebug = Memory.ReadValue<ulong>(this.WeatherController + Offsets.WeatherController.WeatherDebug);

            return true;
        }

        public void ModifyCloudDensity(bool state, ref List<IScatterWriteEntry> entries)
        {
            var cloudStateChanged = state != this._removeClouds;

            if (cloudStateChanged)
            {
                this._removeClouds = state;

                this.CheckDebugWeatherEnabled(ref entries);
                entries.Add(new ScatterWriteDataEntry<float>(this.WeatherControllerDebug + Offsets.WeatherDebug.CloudDensity, (state ? -1f : -0.089f)));
            }
        }

        public void ModifyFog(bool state, ref List<IScatterWriteEntry> entries)
        {
            var fogStateChanged = state != this._removeFog;

            if (fogStateChanged)
            {
                this._removeFog = state;

                this.CheckDebugWeatherEnabled(ref entries);
                entries.Add(new ScatterWriteDataEntry<float>(this.WeatherControllerDebug + Offsets.WeatherDebug.Fog, (state ? 0.001f : 0.01f)));
            }
        }

        public void ModifyRain(bool state, ref List<IScatterWriteEntry> entries)
        {
            var rainStateChanged = state != this._removeRain;

            if (rainStateChanged)
            {
                this._removeRain = state;

                this.CheckDebugWeatherEnabled(ref entries);
                entries.Add(new ScatterWriteDataEntry<float>(this.WeatherControllerDebug + Offsets.WeatherDebug.Rain, (state ? 0f : 0.1f)));
            }
        }

        private bool CheckDebugWeatherEnabled(ref List<IScatterWriteEntry> entries)
        {
            if ((this._removeRain || this._removeFog || this._removeClouds) && !this._debugWeather)
            {
                this._debugWeather = true;
                entries.Add(new ScatterWriteDataEntry<bool>(this.WeatherControllerDebug + Offsets.WeatherDebug.IsEnabled, true));
            }
            else if (!this._removeRain && !this._removeFog && !this._removeClouds && this._debugWeather)
            {
                this._debugWeather = false;
                entries.Add(new ScatterWriteDataEntry<bool>(this.WeatherControllerDebug + Offsets.WeatherDebug.IsEnabled, false));
            }

            return this._debugWeather;
        }
        #endregion

        #region TOD_Sky
        public bool InitializeTOD_Sky()
        {
            var tod_sky_static = MonoSharp.GetStaticFieldDataOfClass("Assembly-CSharp", "TOD_Sky");
            this.TOD_Sky = Memory.ReadValue<ulong>(tod_sky_static + Offsets.TOD_SKY.CachedPtr);
            var instance = Memory.ReadValue<ulong>(this.TOD_Sky + Offsets.TOD_SKY.Instance);

            this.TOD_CycleParameters = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.Cycle);
            this.TOD_DayParameters = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.Day);
            this.TOD_NightParameters = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.Night);
            this.TOD_SunParameters = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.Sun);
            this.TOD_MoonParameters = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.Moon);
            this.TOD_AmbientParameters = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.Ambient);
            this.TOD_Components = Memory.ReadValue<ulong>(instance + Offsets.TOD_SKY.TOD_Components);

            this.TOD_Time = Memory.ReadValue<ulong>(this.TOD_Components + Offsets.TOD_Components.Time);
            this.GameDateTime = Memory.ReadValue<ulong>(this.TOD_Time + Offsets.TOD_Time.GameDateTime);

            return true;
        }

        public void ModifySunSize(bool state, ref List<IScatterWriteEntry> entries)
        {
            var sunStateChanged = state != this._removeSun;

            if (sunStateChanged)
            {
                this._removeSun = state;
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_SunParameters + Offsets.TOD_SunParameters.MeshSize, (state ? 0f : 1f)));
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_SunParameters + Offsets.TOD_SunParameters.MeshBrightness, (state ? 0f : 1f)));
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_SunParameters + Offsets.TOD_SunParameters.MeshContrast, (state ? 0f : 1f)));
            }
        }

        public void ModifyMoonSize(bool state, ref List<IScatterWriteEntry> entries)
        {
            var moonStateChanged = state != this._removeMoon;

            if (moonStateChanged)
            {
                this._removeMoon = state;
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_MoonParameters + Offsets.TOD_MoonParameters.MeshSize, (state ? 0f : 1f)));
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_MoonParameters + Offsets.TOD_MoonParameters.MeshBrightness, (state ? 0f : 1f)));
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_MoonParameters + Offsets.TOD_MoonParameters.MeshContrast, (state ? 0f : 1f)));
            }
        }

        public void ModifyShadows(bool state, ref List<IScatterWriteEntry> entries)
        {
            var shadowStateChanged = state != this._removeShadows;

            if (shadowStateChanged)
            {
                this._removeShadows = state;
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_DayParameters + Offsets.TOD_DayParameters.ShadowStrength, (state ? 0f : 1f)));
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_NightParameters + Offsets.TOD_NightParameters.ShadowStrength, (state ? 0f : 1f)));
            }
        }

        public void ModifyDayLightIntensity(bool state, int lightIntensity, ref List<IScatterWriteEntry> entries)
        {
            var dayLightStateChanged = state != this._dayLightSet;
            var dayLightIntensityChanged = lightIntensity != this._dayLightIntensity;

            if (dayLightStateChanged || dayLightIntensityChanged)
            {
                if (dayLightStateChanged)
                {
                    this._dayLightSet = state;
                    this.CheckAmbientUpdateInterval(ref entries);
                    entries.Add(new ScatterWriteDataEntry<float>(this.TOD_DayParameters + Offsets.TOD_DayParameters.AmbientMultiplier, (state ? 1f : 1f)));
                    entries.Add(new ScatterWriteDataEntry<float>(this.TOD_DayParameters + Offsets.TOD_DayParameters.ReflectionMultiplier, (state ? 1f : 1f)));

                    if (!state && this._dayLightIntensity != -1)
                    {
                        this._dayLightIntensity = -1;
                        entries.Add(new ScatterWriteDataEntry<float>(this.TOD_DayParameters + Offsets.TOD_DayParameters.LightIntensity, 1f));
                    }
                }
                    

                if (state && dayLightIntensityChanged)
                {
                    this._dayLightIntensity = lightIntensity;
                    entries.Add(new ScatterWriteDataEntry<float>(this.TOD_DayParameters + Offsets.TOD_DayParameters.LightIntensity, (state ? (float)lightIntensity : 1f)));
                }
            }
        }

        public void ModifyNightLightIntensity(bool state, int nightIntensity, ref List<IScatterWriteEntry> entries)
        {
            var nightLightStateChanged = state != this._nightLightSet;
            var nightLightIntensityChanged = nightIntensity != this._nightLightIntensity;

            if (nightLightStateChanged || nightLightIntensityChanged)
            {
                if (nightLightStateChanged)
                {
                    this._nightLightSet = state;
                    this.CheckAmbientUpdateInterval(ref entries);
                    entries.Add(new ScatterWriteDataEntry<float>(this.TOD_NightParameters + Offsets.TOD_NightParameters.AmbientMultiplier, (state ? 6f : 1f)));
                    entries.Add(new ScatterWriteDataEntry<float>(this.TOD_NightParameters + Offsets.TOD_NightParameters.ReflectionMultiplier, (state ? 1f : 1f)));

                    if (!state && this._nightLightIntensity != -1)
                    {
                        this._nightLightIntensity = -1;
                        entries.Add(new ScatterWriteDataEntry<float>(this.TOD_NightParameters + Offsets.TOD_NightParameters.LightIntensity, 0.2f));
                    }
                }

                if (state && nightLightIntensityChanged)
                {
                    this._nightLightIntensity = nightIntensity;
                    entries.Add(new ScatterWriteDataEntry<float>(this.TOD_NightParameters + Offsets.TOD_NightParameters.LightIntensity, (state ? (float)nightIntensity : 1f)));
                }
            }
        }

        private bool CheckAmbientUpdateInterval(ref List<IScatterWriteEntry> entries)
        {
            if ((this._dayLightSet || this._nightLightSet) && !this._updateIntervalSet)
            {
                this._updateIntervalSet = true;
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_AmbientParameters + Offsets.TOD_AmbientParameters.UpdateInterval, 9999999f));
            }
            else if (!this._dayLightSet && !this._nightLightSet && this._updateIntervalSet)
            {
                this._updateIntervalSet = false;
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_AmbientParameters + Offsets.TOD_AmbientParameters.UpdateInterval, 10f));
            }

            return this._updateIntervalSet;
        }

        public void FreezeTime(bool state, ref List<IScatterWriteEntry> entries)
        {
            var freezeStateChanged = state != this._timeFrozen;

            if (freezeStateChanged)
            {
                this._timeFrozen = state;
                entries.Add(new ScatterWriteDataEntry<bool>(this.TOD_Time + Offsets.TOD_Time.LockCurrentTime, this._timeFrozen));
                entries.Add(new ScatterWriteDataEntry<bool>(this.GameDateTime + Offsets.GameDateTime.Locked, this._timeFrozen));
            }
        }

        public void SetTimeOfDay(int time, ref List<IScatterWriteEntry> entries)
        {
            if (this._timeFrozen && this._timeOfDay != time)
            {
                this._timeOfDay = time;
                entries.Add(new ScatterWriteDataEntry<float>(this.TOD_CycleParameters + Offsets.TOD_CycleParameters.Hour, (float)this._timeOfDay));
            }
            else if (!this._timeFrozen && this._timeOfDay != -1)
            {
                this._timeOfDay = -1;
            }
        }
        #endregion
    }
}
