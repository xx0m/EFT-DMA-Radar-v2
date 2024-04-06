using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace eft_dma_radar.Source.Tarkov
{
    public class CameraManager
    {
        private ulong _unityBase;
        private ulong _opticCamera;
        private ulong _fpsCamera;
        public bool IsReady
        {
            get
            {
                return this._opticCamera != 0 && this._fpsCamera != 0;
            }
        }

        private Config _config
        {
            get => Program.Config;
        }

        public CameraManager(ulong unityBase)
        {
            this._unityBase = unityBase;
            this.GetCamera();
        }

        private bool GetCamera()
        {
            try
            {
                var addr = Memory.ReadPtr(this._unityBase + Offsets.ModuleBase.CameraObjectManager);
                for (int i = 0; i < 500; i++)
                {
                    var allCameras = Memory.ReadPtr(addr + 0x0);
                    var camera = Memory.ReadPtr(allCameras + (ulong)i * 0x8);

                    if (camera != 0)
                    {
                        var cameraObject = Memory.ReadPtr(camera + Offsets.GameObject.ObjectClass);
                        var cameraNamePtr = Memory.ReadPtr(cameraObject + Offsets.GameObject.ObjectName);

                        var cameraName = Memory
                            .ReadString(cameraNamePtr, 64)
                            .Replace("\0", string.Empty);
                        if (
                            cameraName.Contains(
                                "BaseOpticCamera(Clone)",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            this._opticCamera = cameraObject;
                        }
                        if (cameraName.Contains("FPS Camera", StringComparison.OrdinalIgnoreCase))
                        {
                            this._fpsCamera = cameraObject;
                        }
                        if (this._opticCamera != 0 && this._fpsCamera != 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (DMAShutdown)
            {
                throw;
            }
            return false;
        }

        public async Task<bool> GetCameraAsync()
        {
            return await Task.Run(() => this.GetCamera());
        }

        public void UpdateCamera()
        {
            if (this._unityBase == 0)
                return;
            this.GetCamera();
        }

        private ulong GetComponentFromGameObject(ulong gameObject, string componentName)
        {
            var component = Memory.ReadPtr(gameObject + Offsets.GameObject.ObjectClass);
            // Loop through a fixed range of potential component slots
            for (int i = 0x8; i < 0x500; i += 0x10)
            {
                try
                {
                    var componentPtr = Memory.ReadPtr(component + (ulong)i);
                    if (componentPtr == 0)
                        continue;
                    var fieldsPtr = Memory.ReadPtr(componentPtr + 0x28);
                    componentPtr = Memory.ReadPtr(componentPtr + 0x28);
                    var classNamePtr = Memory.ReadPtrChain(fieldsPtr, Offsets.UnityClass.Name);
                    var className = Memory.ReadString(classNamePtr, 64).Replace("\0", string.Empty);
                    if (className.Contains(componentName, StringComparison.OrdinalIgnoreCase))
                    {
                        return componentPtr;
                    }
                }
                catch { }
            }
            return 0;
        }

        /// <summary>
        /// public function to turn nightvision on and off
        /// </summary>
        public void NightVision(bool on)
        {
            if (this.IsReady)
            {
                try
                {
                    var nightVisionComponent = this.GetComponentFromGameObject(this._fpsCamera, "NightVision");
                    var nightVisionOn = Memory.ReadValue<bool>(nightVisionComponent + Offsets.NightVision.On);

                    if (on != nightVisionOn)
                    {
                        Memory.WriteValue(nightVisionComponent + Offsets.NightVision.On, !nightVisionOn);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// public function to turn visor on and off
        /// </summary>
        public void VisorEffect(bool on)
        {
            if (this.IsReady)
            {
                try
                {
                    var visorComponent = this.GetComponentFromGameObject(this._fpsCamera, "VisorEffect");
                    bool visorDown = (Memory.ReadValue<float>(visorComponent + Offsets.VisorEffect.Intensity) == 1.0f);

                    if (on == visorDown)
                    {
                        Memory.WriteValue(visorComponent + Offsets.VisorEffect.Intensity, (on ? 0.0f : 1.0f));
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// public function to turn thermalvision on and off
        /// </summary>
        public void ThermalVision(bool on)
        {
            if (this.IsReady)
            {
                try
                {
                    var FPSThermal = GetComponentFromGameObject(this._fpsCamera, "ThermalVision");
                    var thermalOn = Memory.ReadValue<bool>(FPSThermal + Offsets.ThermalVision.On);

                    if (on != thermalOn)
                    {
                    
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.On, !thermalOn);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.IsNoisy, thermalOn);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.IsFpsStuck, thermalOn);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.IsMotionBlurred, thermalOn);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.IsGlitched, thermalOn);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.IsPixelated, thermalOn);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0.0f);
                        Memory.WriteValue(FPSThermal + Offsets.ThermalVision.UnsharpRadiusBlur, 2.0f);

                        var thermalVisionUtilities = Memory.ReadPtr(FPSThermal + Offsets.ThermalVision.ThermalVisionUtilities);
                        var valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);
                        Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.MainThermalSetting.ColorCoefficient);
                        Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.MainThermalSetting.MinTemperature);
                        Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.MainThermalSetting.RampShift);
                        Memory.WriteValue(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.MainThermalSetting.ColorScheme);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// public function to turn optic thermalvision on and off
        /// </summary>
        public void OpticThermalVision(bool on)
        {
            if (this.IsReady)
            {
                try
                {
                    ulong opticComponent = 0;
                    var component = Memory.ReadPtr(this._opticCamera + Offsets.GameObject.ObjectClass);
                    var opticThermal = GetComponentFromGameObject(this._opticCamera, "ThermalVision");
                    for (int i = 0x8; i < 0x100; i += 0x10)
                    {
                        var fields = Memory.ReadPtr(component + (ulong)i);
                        if (fields == 0)
                            continue;
                        var fieldsPtr_ = Memory.ReadPtr(fields + 0x28);
                        var classNamePtr = Memory.ReadPtrChain(fieldsPtr_, Offsets.UnityClass.Name);
                        var className = Memory.ReadString(classNamePtr, 64).Replace("\0", string.Empty);
                        if (className == "ThermalVision")
                        {
                            opticComponent = fields;
                            break;
                        }
                    }

                    Memory.WriteValue(opticComponent + 0x38, on);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.IsNoisy, !on);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.IsFpsStuck, !on);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.IsMotionBlurred, !on);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.IsGlitched, !on);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.IsPixelated, !on);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0.0f);
                    Memory.WriteValue(opticThermal + Offsets.ThermalVision.UnsharpRadiusBlur, 2.0f);
               
                    var thermalVisionUtilities = Memory.ReadPtr(opticThermal + Offsets.ThermalVision.ThermalVisionUtilities);
                    var valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);
                    Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.OpticThermalSetting.ColorCoefficient);
                    Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.OpticThermalSetting.MinTemperature);
                    Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.OpticThermalSetting.RampShift);
                    Memory.WriteValue(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.OpticThermalSetting.ColorScheme);
                }
                catch { }

            }
        }
    }
}