namespace eft_dma_radar
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
            var foundFPSCamera = false;
            var foundOpticCamera = false;
            var count = 400;
            var addr = Memory.ReadPtr(this._unityBase + Offsets.ModuleBase.CameraObjectManager);

            var scatterReadMap = new ScatterReadMap(count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                var allCameras = round1.AddEntry<ulong>(i, 0, addr, null, 0x0);
                var camera = round2.AddEntry<ulong>(i, 1, allCameras, null, (uint)i * 0x8);
                var cameraObject = round3.AddEntry<ulong>(i, 2, camera, null, Offsets.GameObject.ObjectClass);
                var cameraNamePtr = round4.AddEntry<ulong>(i, 3, cameraObject, null, Offsets.GameObject.ObjectName);
            }

            scatterReadMap.Execute();

            for (int i = 0; i < count; i++)
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var allCameras))
                    continue;
                if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var camera))
                    continue;
                if (!scatterReadMap.Results[i][2].TryGetResult<ulong>(out var cameraObject))
                    continue;
                if (!scatterReadMap.Results[i][3].TryGetResult<ulong>(out var cameraNamePtr))
                    continue;

                var cameraName = Memory.ReadString(cameraNamePtr, 64).Replace("\0", string.Empty);

                if (!foundFPSCamera && cameraName.Contains("BaseOpticCamera(Clone)", StringComparison.OrdinalIgnoreCase))
                {
                    this._opticCamera = cameraObject;
                    foundFPSCamera = true;
                }
                else if (!foundOpticCamera && cameraName.Contains("FPS Camera", StringComparison.OrdinalIgnoreCase))
                {
                    foundOpticCamera = true;
                    this._fpsCamera = cameraObject;
                }

                if (foundFPSCamera && foundOpticCamera)
                {
                    break;
                }
            }

            return foundFPSCamera && foundOpticCamera;
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
                    var fieldsPtr = Memory.ReadPtr(componentPtr + 0x28);
                    var classNamePtr = Memory.ReadPtrChain(fieldsPtr, Offsets.UnityClass.Name);
                    var className = Memory.ReadString(classNamePtr, 64).Replace("\0", string.Empty);

                    if (string.IsNullOrEmpty(className))
                        continue;

                    if (className.Contains(componentName, StringComparison.OrdinalIgnoreCase))
                        return fieldsPtr;
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
            if (!this.IsReady)
                return;

            try
            {
                var nightVisionComponent = this.GetComponentFromGameObject(this._fpsCamera, "NightVision");
                if (nightVisionComponent == 0)
                    return;

                bool nightVisionOn = Memory.ReadValue<bool>(nightVisionComponent + Offsets.NightVision.On);

                if (on != nightVisionOn)
                    Memory.WriteValue(nightVisionComponent + Offsets.NightVision.On, on);
            }
            catch { }
        }

        /// <summary>
        /// public function to turn visor on and off
        /// </summary>
        public void VisorEffect(bool on)
        {
            if (!this.IsReady)
                return;

            try
            {
                ulong visorComponent = this.GetComponentFromGameObject(this._fpsCamera, "VisorEffect");
                if (visorComponent == 0)
                    return;

                float intensity = Memory.ReadValue<float>(visorComponent + Offsets.VisorEffect.Intensity);
                bool visorDown = intensity == 1.0f;

                if (on == visorDown)
                    Memory.WriteValue(visorComponent + Offsets.VisorEffect.Intensity, on ? 0.0f : 1.0f);
            }
            catch { }
        }

        /// <summary>
        /// public function to turn thermalvision on and off
        /// </summary>
        public void ThermalVision(bool on)
        {
            if (!this.IsReady)
                return;

            try
            {
                ulong fpsThermal = this.GetComponentFromGameObject(this._fpsCamera, "ThermalVision");
                if (fpsThermal == 0)
                    return;

                this.ToggleThermalVision(fpsThermal, on);
            }
            catch { }
        }

        private void ToggleThermalVision(ulong fpsThermal, bool on)
        {
            bool thermalOn = Memory.ReadValue<bool>(fpsThermal + Offsets.ThermalVision.On);

            if (on == thermalOn)
                return;

            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.On, on);
            this.SetThermalVisionProperties(fpsThermal, on);
        }

        private void SetThermalVisionProperties(ulong fpsThermal, bool on)
        {
            bool isOn = !on;
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.IsNoisy, isOn);
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.IsFpsStuck, isOn);
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.IsMotionBlurred, isOn);
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.IsGlitched, isOn);
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.IsPixelated, isOn);
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0.0f);
            Memory.WriteValue(fpsThermal + Offsets.ThermalVision.UnsharpRadiusBlur, 2.0f);

            ulong thermalVisionUtilities = Memory.ReadPtr(fpsThermal + Offsets.ThermalVision.ThermalVisionUtilities);
            ulong valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);
            Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.MainThermalSetting.ColorCoefficient);
            Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.MainThermalSetting.MinTemperature);
            Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.MainThermalSetting.RampShift);
            Memory.WriteValue(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.MainThermalSetting.ColorScheme);
        }

        /// <summary>
        /// public function to turn optic thermalvision on and off
        /// </summary>
        public void OpticThermalVision(bool on)
        {
            if (!this.IsReady)
                return;

            try
            {
                ulong opticComponent = 0;
                var component = Memory.ReadPtr(this._opticCamera + Offsets.GameObject.ObjectClass);
                var opticThermal = this.GetComponentFromGameObject(this._opticCamera, "ThermalVision");
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