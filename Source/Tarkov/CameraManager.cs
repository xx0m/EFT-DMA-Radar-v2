
using System.Diagnostics;
using System.Numerics;

namespace eft_dma_radar
{
    public class CameraManager
    {
        private readonly Stopwatch _swRefreshVM = new();

        private ulong visorComponent;
        private ulong nvgComponent;
        private ulong thermalComponent;
        private ulong frostBiteComponent;
        private ulong opticThermalComponent;
        private ulong inventoryBlurComponent;

        private bool nvgComponentFound = false;
        private bool visorComponentFound = false;
        private bool frostBiteComponentFound = false;
        private bool fpsThermalComponentFound = false;
        private bool opticThermalComponentFound = false;
        private bool inventoryBlurCompontentFound = false;

        private bool fovPtrFound = false;

        private ulong _unityBase;
        private ulong _opticCamera;
        private ulong _fpsCamera;
        private ulong _fovPtr;
        private ulong _viewMatrixPtr;

        private Matrix4x4 _viewMatrix;

        public bool IsReady
        {
            get => this._opticCamera != 0 && this._fpsCamera != 0;
        }

        public ulong NVGComponent
        {
            get => this.nvgComponent;
        }

        public ulong ThermalComponent
        {
            get => this.thermalComponent;
        }

        public ulong FrostbiteComponent
        {
            get => this.frostBiteComponent;
        }

        public ulong FPSCamera
        {
            get => this._fpsCamera;
        }

        public Matrix4x4 ViewMatrix
        {
            get => this._viewMatrix;
        }

        private Config _config
        {
            get => Program.Config;
        }

        public CameraManager(ulong unityBase)
        {
            this._unityBase = unityBase;
            this.GetCamera();
            this._swRefreshVM.Start();
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

                    if (!this.frostBiteComponentFound)
                    {
                        this.frostBiteComponent = this.GetComponentFromGameObject(this._fpsCamera, "FrostBiteEffect");
                        this.frostBiteComponentFound = this.frostBiteComponent != 0;
                    }

                    if (!this.fovPtrFound)
                    {
                        this._fovPtr = Memory.ReadPtrChain(this._fpsCamera, [0x30, 0x18]);
                        this.fovPtrFound = true;
                    }

                    if (!this.inventoryBlurCompontentFound)
                    {
                        this.inventoryBlurComponent = this.GetComponentFromGameObject(this._fpsCamera, "InventoryBlur");
                        this.inventoryBlurCompontentFound = this.inventoryBlurComponent != 0;
                    }

                    if (this._viewMatrixPtr == 0)
                        this._viewMatrixPtr = Memory.ReadPtrChain(this._fpsCamera, Offsets.FPSCamera.To_ViewMatrix);

                    foundFPSCamera = this.nvgComponentFound && this.visorComponentFound && this.fpsThermalComponentFound && this.frostBiteComponentFound && this._viewMatrixPtr != 0;
                }

                if (foundFPSCamera && foundOpticCamera)
                    break;
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

        private ulong GetOpticComponent()
        {
            var count = 32;
            var addr = Memory.ReadPtr(this._opticCamera + Offsets.GameObject.ObjectClass);

            var scatterReadMap = new ScatterReadMap(count);
            var round1 = scatterReadMap.AddRound();
            var round2 = scatterReadMap.AddRound();
            var round3 = scatterReadMap.AddRound();
            var round4 = scatterReadMap.AddRound();
            var round5 = scatterReadMap.AddRound();
            var round6 = scatterReadMap.AddRound();

            for (int i = 1; i < count; i++)
            {
                var fields = round1.AddEntry<ulong>(i, 0, addr, null, (uint)i * 0x8);
                var fieldsPtr = round2.AddEntry<ulong>(i, 1, fields, null, 0x28);
                var classNamePtr1 = round3.AddEntry<ulong>(i, 2, fieldsPtr, null, Offsets.UnityClass.Name[0]);
                var classNamePtr2 = round4.AddEntry<ulong>(i, 3, classNamePtr1, null, Offsets.UnityClass.Name[1]);
                var classNamePtr = round5.AddEntry<ulong>(i, 4, classNamePtr2, null, Offsets.UnityClass.Name[2]);
                var className = round6.AddEntry<string>(i, 5, classNamePtr, 64);
            }

            scatterReadMap.Execute();

            for (int i = 1; i < count; i++)
            {
                if (!scatterReadMap.Results[i][0].TryGetResult<ulong>(out var fields))
                    continue;
                if (!scatterReadMap.Results[i][5].TryGetResult<string>(out var className))
                    continue;

                className = className.Replace("\0", string.Empty);

                if (className == "ThermalVision")
                    return fields;
            }

            return 0;
        }

        /// <summary>
        /// public function to turn nightvision on and off
        /// </summary>
        public void NightVision(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady)
                return;

            try
            {
                var nightVisionOn = Memory.ReadValue<bool>(this.NVGComponent + Offsets.NightVision.On);

                if (state != nightVisionOn)
                    entries.Add(new ScatterWriteDataEntry<bool>(this.NVGComponent + Offsets.NightVision.On, state));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (NightVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// public function to turn visor on and off
        /// </summary>
        public void VisorEffect(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady)
                return;

            try
            {
                var intensity = Memory.ReadValue<float>(this.visorComponent + Offsets.VisorEffect.Intensity);
                var visorDown = intensity == 1.0f;

                if (state == visorDown)
                    entries.Add(new ScatterWriteDataEntry<float>(this.visorComponent + Offsets.VisorEffect.Intensity, state ? 0f : 1.0f));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (VisorEffect) {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// public function to turn frostbite on and off
        /// </summary>
        public void FrostBite(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady)
                return;

            try
            {
                var opacity = Memory.ReadValue<float>(this.frostBiteComponent + Offsets.FrostbiteEffect.Opacity);
                var frostBite = opacity == 0.75f;

                if (state == frostBite)
                    entries.Add(new ScatterWriteDataEntry<float>(this.frostBiteComponent + Offsets.FrostbiteEffect.Opacity, state ? 0f : 0.75f));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (FrostBite) {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// public function to turn thermalvision on and off
        /// </summary>
        public void ThermalVision(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady)
                return;

            try
            {
                this.ToggleThermalVision(state, ref entries);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (ThermalVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void ToggleThermalVision(bool state, ref List<IScatterWriteEntry> entries)
        {
            try
            {
                entries.Add(new ScatterWriteDataEntry<bool>(this.thermalComponent + Offsets.ThermalVision.On, state));
                this.SetThermalVisionProperties(state, ref entries);
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (ToggleThermalVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void SetThermalVisionProperties(bool state, ref List<IScatterWriteEntry> entries)
        {
            try
            {
                var thermalVisionUtilities = Memory.ReadPtr(this.thermalComponent + Offsets.ThermalVision.ThermalVisionUtilities);
                var valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);
                
                entries.Add(new ScatterWriteDataEntry<bool>(this.thermalComponent + Offsets.ThermalVision.IsNoisy, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.thermalComponent + Offsets.ThermalVision.IsFpsStuck, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.thermalComponent + Offsets.ThermalVision.IsMotionBlurred, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.thermalComponent + Offsets.ThermalVision.IsGlitched, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.thermalComponent + Offsets.ThermalVision.IsPixelated, !state));
                entries.Add(new ScatterWriteDataEntry<float>(this.thermalComponent + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0f));
                entries.Add(new ScatterWriteDataEntry<float>(this.thermalComponent + Offsets.ThermalVision.UnsharpRadiusBlur, 0.00001f));

                entries.Add(new ScatterWriteDataEntry<float>(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.MainThermalSetting.ColorCoefficient));
                entries.Add(new ScatterWriteDataEntry<float>(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.MainThermalSetting.MinTemperature));
                entries.Add(new ScatterWriteDataEntry<float>(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.MainThermalSetting.RampShift));

                entries.Add(new ScatterWriteDataEntry<int>(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.MainThermalSetting.ColorScheme));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (SetThermalVisionProperties) {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// public function to turn optic thermalvision on and off
        /// </summary>
        public void OpticThermalVision(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady)
                return;

            try
            {
                var opticComponent = this.GetOpticComponent();

                if (opticComponent == 0)
                    return;

                var thermalVisionUtilities = Memory.ReadPtr(this.opticThermalComponent + Offsets.ThermalVision.ThermalVisionUtilities);
                var valuesCoefs = Memory.ReadPtr(thermalVisionUtilities + Offsets.ThermalVisionUtilities.ValuesCoefs);

                entries.Add(new ScatterWriteDataEntry<bool>(opticComponent + 0x38, state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.opticThermalComponent + Offsets.ThermalVision.IsNoisy, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.opticThermalComponent + Offsets.ThermalVision.IsFpsStuck, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.opticThermalComponent + Offsets.ThermalVision.IsMotionBlurred, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.opticThermalComponent + Offsets.ThermalVision.IsGlitched, !state));
                entries.Add(new ScatterWriteDataEntry<bool>(this.opticThermalComponent + Offsets.ThermalVision.IsPixelated, !state));
                entries.Add(new ScatterWriteDataEntry<float>(this.opticThermalComponent + Offsets.ThermalVision.ChromaticAberrationThermalShift, 0f));
                entries.Add(new ScatterWriteDataEntry<float>(this.opticThermalComponent + Offsets.ThermalVision.UnsharpRadiusBlur, 0.00001f));

                entries.Add(new ScatterWriteDataEntry<float>(valuesCoefs + Offsets.ValuesCoefs.MainTexColorCoef, this._config.OpticThermalSetting.ColorCoefficient));
                entries.Add(new ScatterWriteDataEntry<float>(valuesCoefs + Offsets.ValuesCoefs.MinimumTemperatureValue, this._config.OpticThermalSetting.MinTemperature));
                entries.Add(new ScatterWriteDataEntry<float>(valuesCoefs + Offsets.ValuesCoefs.RampShift, this._config.OpticThermalSetting.RampShift));

                entries.Add(new ScatterWriteDataEntry<int>(thermalVisionUtilities + Offsets.ThermalVisionUtilities.CurrentRampPalette, this._config.OpticThermalSetting.ColorScheme));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (OpticThermalVision) {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void SetFOV(int fov, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady)
                return;

            try
            {
                var currentFOV = Memory.ReadValue<float>(this._fovPtr + 0x15C);

                if (currentFOV != fov)
                    entries.Add(new ScatterWriteDataEntry<float>(this._fovPtr + 0x15C, (float)fov));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (SetFOV) {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void InventoryBlur(bool state, ref List<IScatterWriteEntry> entries)
        {
            if (!this.IsReady || !state)
                return;

            try
            {
                var invOpen = Memory.ReadValue<bool>(this.inventoryBlurComponent + Offsets.InventoryBlur.BlurEnabled);

                if (invOpen)
                    entries.Add(new ScatterWriteDataEntry<bool>(this.inventoryBlurComponent + Offsets.InventoryBlur.BlurEnabled, false));
            }
            catch (Exception ex)
            {
                Program.Log($"CameraManager - (InventoryBlur) {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void UpdateViewMatrix()
        {
            if (this._swRefreshVM.ElapsedMilliseconds >= 10 && Memory.LocalPlayer is not null)
            {
                this._viewMatrix = Memory.ReadValue<Matrix4x4>(this._viewMatrixPtr + Offsets.ViewMatrix.Matrix);
                this._swRefreshVM.Restart();
            }
        }
    }
}