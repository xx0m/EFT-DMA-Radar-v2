
namespace eft_dma_radar
{
    public class CameraManager
    {

        private ulong visorComponent;
        private ulong nvgComponent;
        private ulong thermalComponent;
        private ulong opticThermalComponent;

        private bool nvgComponentFound = false;
        private bool visorComponentFound = false;
        private bool fpsThermalComponentFound = false;
        private bool opticThermalComponentFound = false;

        private ulong _unityBase;
        private ulong _opticCamera;
        private ulong _fpsCamera;

        public bool IsReady { get => this._opticCamera != 0 && this._fpsCamera != 0; }
        public ulong NVGComponent { get => this.nvgComponent; }
        public ulong ThermalComponent { get => this.thermalComponent; }
        public ulong FPSCamera
        {
            get
            {
                return this._fpsCamera;
            }
        }

        private Config _config { get => Program.Config; }

        public CameraManager(ulong unityBase)
        {
            this._unityBase = unityBase;
            this.GetCamera();
        }

        private bool GetCamera()
        {
            var count = 400;
            var foundFPSCamera = false;
            var foundOpticCamera = false;
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

                if (!foundOpticCamera && cameraName.Contains("BaseOpticCamera(Clone)", StringComparison.OrdinalIgnoreCase))
                {
                    this._opticCamera = cameraObject;

                    if (!this.opticThermalComponentFound)
                    {
                        this.opticThermalComponent = this.GetComponentFromGameObject(this._opticCamera, "ThermalVision");
                        this.opticThermalComponentFound = this.opticThermalComponent != 0;
                    }

                    foundOpticCamera = this.opticThermalComponentFound;
                }
                else if (!foundFPSCamera && cameraName.Contains("FPS Camera", StringComparison.OrdinalIgnoreCase))
                {
                    this._fpsCamera = cameraObject;

                    if (!this.visorComponentFound) {
                        this.visorComponent = this.GetComponentFromGameObject(this._fpsCamera, "VisorEffect");
                        this.visorComponentFound = this.visorComponent != 0;
                    }

                    if (!this.nvgComponentFound)
                    {
                        this.nvgComponent = this.GetComponentFromGameObject(this._fpsCamera, "NightVision");
                        this.nvgComponentFound = this.NVGComponent != 0;
                    }

                    if (!this.fpsThermalComponentFound)
                    {
                        this.thermalComponent = this.GetComponentFromGameObject(this._fpsCamera, "ThermalVision");
                        this.fpsThermalComponentFound = this.thermalComponent != 0;
                    }

                    foundFPSCamera = this.nvgComponentFound && this.visorComponentFound && this.fpsThermalComponentFound;
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

        public ulong GetComponentFromGameObject(ulong gameObject, string componentName)
        {
            try
            {
                var count = 0x500;
                var scatterReadMap = new ScatterReadMap(count);
                var round1 = scatterReadMap.AddRound();
                var round2 = scatterReadMap.AddRound();
                var round3 = scatterReadMap.AddRound();
                var round4 = scatterReadMap.AddRound();
                var round5 = scatterReadMap.AddRound();

                var component = Memory.ReadPtr(gameObject + Offsets.GameObject.ObjectClass);

                for (int i = 0x8; i < 0x500; i += 0x10)
                {
                    var componentPtr = round1.AddEntry<ulong>(i, 0, component, null, (uint)(ulong)i);
                    var fieldsPtr = round2.AddEntry<ulong>(i, 1, componentPtr, null, 0x28);
                    var classNamePtr1 = round3.AddEntry<ulong>(i, 2, fieldsPtr, null, Offsets.UnityClass.Name[0]);
                    var classNamePtr2 = round4.AddEntry<ulong>(i, 3, classNamePtr1, null, Offsets.UnityClass.Name[1]);
                    var classNamePtr3 = round5.AddEntry<ulong>(i, 4, classNamePtr2, null, Offsets.UnityClass.Name[2]);
                }

                scatterReadMap.Execute();

                for (int i = 0x8; i < 0x500; i += 0x10)
                {
                    if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var componentPtr))
                        continue;
                    if (!scatterReadMap.Results[i][1].TryGetResult<ulong>(out var fieldsPtr))
                        continue;
                    if (!scatterReadMap.Results[i][4].TryGetResult<ulong>(out var classNamePtr))
                        continue;

                    var className = Memory.ReadString(classNamePtr, 64).Replace("\0", string.Empty);

                    if (string.IsNullOrEmpty(className))
                        continue;

                    if (className.Contains(componentName, StringComparison.OrdinalIgnoreCase))
                        return fieldsPtr;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (GetComponentFromGameObject) {ex.Message}\n{ex.StackTrace}");
            }


            return 0;
        }

        /// <summary>
        /// public function to turn nightvision on and off
        /// </summary>
        public void NightVision(bool state)
        {
            if (!this.IsReady)
                return;

            try
            {
                bool nightVisionOn = Memory.ReadValue<bool>(this.NVGComponent + Offsets.NightVision.On);

                if (state != nightVisionOn)
                    Memory.WriteValue(this.NVGComponent + Offsets.NightVision.On, state);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (NightVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// public function to turn visor on and off
        /// </summary>
        public void VisorEffect(bool state)
        {
            if (!this.IsReady)
                return;

            try
            {
                float intensity = Memory.ReadValue<float>(this.visorComponent + Offsets.VisorEffect.Intensity);
                bool visorDown = intensity == 1.0f;

                if (state == visorDown)
                    Memory.WriteValue(this.visorComponent + Offsets.VisorEffect.Intensity, state ? 0.0f : 1.0f);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (VisorEffect) {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// public function to turn thermalvision on and off
        /// </summary>
        public void ThermalVision(bool state)
        {
            if (!this.IsReady)
                return;

            try
            {
                this.ToggleThermalVision(state);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (ThermalVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ToggleThermalVision(bool state)
        {
            bool thermalOn = Memory.ReadValue<bool>(this.thermalComponent + Offsets.ThermalVision.On);

            if (state == thermalOn)
                return;

            try
            {
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.On, state);
                this.SetThermalVisionProperties(state);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (ToggleThermalVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void SetThermalVisionProperties(bool state)
        {
            try
            {
                bool isOn = !state;
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.IsNoisy, isOn);
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.IsFpsStuck, isOn);
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.IsMotionBlurred, isOn);
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.IsGlitched, isOn);
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.IsPixelated, isOn);
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0.0f);
                Memory.WriteValue(this.thermalComponent + Offsets.ThermalVision.UnsharpRadiusBlur, 2.0f);

                ulong thermalVisionUtilities = Memory.ReadPtr(this.thermalComponent + Offsets.ThermalVision.ThermalVisionUtilities);
                ulong valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);
                Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.MainThermalSetting.ColorCoefficient);
                Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.MainThermalSetting.MinTemperature);
                Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.MainThermalSetting.RampShift);
                Memory.WriteValue(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.MainThermalSetting.ColorScheme);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (SetThermalVisionProperties) {ex.Message}\n{ex.StackTrace}");
            }
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
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.IsNoisy, !on);
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.IsFpsStuck, !on);
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.IsMotionBlurred, !on);
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.IsGlitched, !on);
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.IsPixelated, !on);
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0.0f);
                Memory.WriteValue(this.opticThermalComponent + Offsets.ThermalVision.UnsharpRadiusBlur, 2.0f);

                var thermalVisionUtilities = Memory.ReadPtr(this.opticThermalComponent + Offsets.ThermalVision.ThermalVisionUtilities);
                var valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);
                Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.OpticThermalSetting.ColorCoefficient);
                Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.OpticThermalSetting.MinTemperature);
                Memory.WriteValue(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.OpticThermalSetting.RampShift);
                Memory.WriteValue(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.OpticThermalSetting.ColorScheme);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (OpticThermalVision) {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}