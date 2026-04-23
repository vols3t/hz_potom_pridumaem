using Godot;
using System;
using System.Collections;
using System.Runtime.InteropServices;

public partial class SettingsPanel : PanelContainer
{
    [Signal]
    public delegate void PanelClosedEventHandler();

    [Export] public ColorRect BrightnessOverlay;

    private static float _savedBrightness = 50f;
    private static float _savedSound = 80f;

    private HSlider _brightnessSlider;
    private Label _brightnessValue;
    private HSlider _soundSlider;
    private Label _soundValue;
    private bool _isUpdatingUi;
    private bool _uiBuilt;
    private static bool? _deviceBrightnessSupported;
    private static bool? _deviceVolumeSupported;
    private static readonly Guid IAudioEndpointVolumeGuid = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private const int ClsCtxInprocServer = 0x1;
    private const uint CoinitMultithreaded = 0x0;
    private const int RpcEChangedMode = unchecked((int)0x80010106);

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out uint channelCount);
        int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
        int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
        int GetMasterVolumeLevel(out float levelDb);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid eventContext);
        int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);
        int GetChannelVolumeLevel(uint channelNumber, out float levelDb);
        int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);
        int GetMute(out bool isMuted);
        int GetVolumeStepInfo(out uint step, out uint stepCount);
        int VolumeStepUp(ref Guid eventContext);
        int VolumeStepDown(ref Guid eventContext);
        int QueryHardwareSupport(out uint hardwareSupportMask);
        int GetVolumeRange(out float volumeMindB, out float volumeMaxdB, out float volumeIncrementdB);
    }

    public override void _Ready()
    {
        ConfigureFullscreenLayout();
        BuildUi();
        ApplyAllSavedSettings();
        VisibilityChanged += OnVisibilityChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || @event == null)
            return;

        if (@event.IsActionPressed("ui_cancel"))
        {
            ClosePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    public void OpenPanel()
    {
        if (!IsInsideTree())
            return;

        Visible = true;
        MoveToFront();
        ApplyAllSavedSettings();
    }

    public void ClosePanel()
    {
        if (!Visible)
            return;

        Visible = false;
        EmitSignal(SignalName.PanelClosed);
    }

    private void OnVisibilityChanged()
    {
        if (Visible)
            ApplyAllSavedSettings();
    }

    private void ConfigureFullscreenLayout()
    {
        LayoutMode = 1;
        AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        AnchorRight = 1f;
        AnchorBottom = 0.84f;
        OffsetLeft = 0f;
        OffsetTop = 0f;
        OffsetRight = 0f;
        OffsetBottom = -8f;
        GrowHorizontal = GrowDirection.Both;
        GrowVertical = GrowDirection.Both;
    }

    private void BuildUi()
    {
        AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("112b45"), new Color("2f4f73"), 3, 16));

        var rootMargin = new MarginContainer();
        rootMargin.AddThemeConstantOverride("margin_left", 14);
        rootMargin.AddThemeConstantOverride("margin_top", 14);
        rootMargin.AddThemeConstantOverride("margin_right", 14);
        rootMargin.AddThemeConstantOverride("margin_bottom", 14);
        AddChild(rootMargin);

        var root = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 10);
        rootMargin.AddChild(root);

        BuildHeader(root);
        BuildBody(root);
        _uiBuilt = true;
    }

    private void BuildHeader(VBoxContainer parent)
    {
        var header = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 92)
        };
        header.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("16314d"), new Color("35597e"), 2, 12));
        parent.AddChild(header);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        header.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var title = new Label
        {
            Text = "Настройки",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeColorOverride("font_color", new Color("e9f2ff"));
        title.AddThemeFontSizeOverride("font_size", 48);
        row.AddChild(title);

        var closeButton = new Button
        {
            Text = "X",
            TooltipText = "Закрыть настройки (Esc)",
            CustomMinimumSize = new Vector2(44, 40),
            FocusMode = FocusModeEnum.None
        };
        closeButton.AddThemeStyleboxOverride("normal", BuildButtonStyle(new Color("274563"), new Color("7da6d1"), 2, 8));
        closeButton.AddThemeStyleboxOverride("hover", BuildButtonStyle(new Color("315679"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeStyleboxOverride("pressed", BuildButtonStyle(new Color("1f3851"), new Color("b1d7ff"), 2, 8));
        closeButton.AddThemeColorOverride("font_color", new Color("eaf4ff"));
        closeButton.AddThemeFontSizeOverride("font_size", 24);
        closeButton.Pressed += ClosePanel;
        row.AddChild(closeButton);
    }

    private void BuildBody(VBoxContainer parent)
    {
        var panel = new PanelContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("15324e"), new Color("3a5f83"), 2, 12));
        parent.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var content = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 14);
        margin.AddChild(content);

        BuildSliderCard(content, "Яркость", out _brightnessSlider, out _brightnessValue);
        _brightnessSlider.ValueChanged += OnBrightnessChanged;

        BuildSliderCard(content, "Звук", out _soundSlider, out _soundValue);
        _soundSlider.ValueChanged += OnSoundChanged;

        var hint = new Label
        {
            Text = "Яркость влияет на экран устройства, звук — на системную громкость.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        hint.AddThemeColorOverride("font_color", new Color("8fb4d8"));
        hint.AddThemeFontSizeOverride("font_size", 18);
        content.AddChild(hint);
    }

    private static void BuildSliderCard(
        VBoxContainer parent,
        string titleText,
        out HSlider slider,
        out Label valueLabel)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 100)
        };
        card.AddThemeStyleboxOverride("panel", BuildPanelStyle(new Color("173550"), new Color("3f658a"), 2, 10));
        parent.AddChild(card);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        card.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        margin.AddChild(row);

        var title = new Label
        {
            Text = titleText,
            CustomMinimumSize = new Vector2(180, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", new Color("edf4ff"));
        title.AddThemeFontSizeOverride("font_size", 30);
        row.AddChild(title);

        slider = new HSlider
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddChild(slider);

        valueLabel = new Label
        {
            Text = "0%",
            CustomMinimumSize = new Vector2(86, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        valueLabel.AddThemeColorOverride("font_color", new Color("ffd66b"));
        valueLabel.AddThemeFontSizeOverride("font_size", 28);
        row.AddChild(valueLabel);
    }

    private void ApplyAllSavedSettings()
    {
        if (!_uiBuilt)
            return;

        _isUpdatingUi = true;
        _brightnessSlider.SetValueNoSignal(_savedBrightness);
        _soundSlider.SetValueNoSignal(_savedSound);
        _isUpdatingUi = false;

        ApplyBrightness(_savedBrightness);
        ApplySound(_savedSound);
        _brightnessValue.Text = $"{Mathf.RoundToInt(_savedBrightness)}%";
        _soundValue.Text = $"{Mathf.RoundToInt(_savedSound)}%";
    }

    private void OnBrightnessChanged(double value)
    {
        _savedBrightness = Mathf.Clamp((float)value, 0f, 100f);
        _brightnessValue.Text = $"{Mathf.RoundToInt(_savedBrightness)}%";
        if (_isUpdatingUi)
            return;

        ApplyBrightness(_savedBrightness);
    }

    private void OnSoundChanged(double value)
    {
        _savedSound = Mathf.Clamp((float)value, 0f, 100f);
        _soundValue.Text = $"{Mathf.RoundToInt(_savedSound)}%";
        if (_isUpdatingUi)
            return;

        ApplySound(_savedSound);
    }

    private void ApplyBrightness(float value)
    {
        var v = Mathf.Clamp(value, 0f, 100f);
        if (TryApplyDeviceBrightness(v))
        {
            if (BrightnessOverlay != null)
                BrightnessOverlay.Color = new Color(0f, 0f, 0f, 0f);
            return;
        }

        if (BrightnessOverlay == null)
            return;

        if (Mathf.IsEqualApprox(v, 50f))
        {
            BrightnessOverlay.Color = new Color(0f, 0f, 0f, 0f);
            return;
        }

        if (v < 50f)
        {
            var t = (50f - v) / 50f;
            BrightnessOverlay.Color = new Color(0f, 0f, 0f, 0.58f * t);
            return;
        }

        var lightT = (v - 50f) / 50f;
        BrightnessOverlay.Color = new Color(1f, 1f, 1f, 0.30f * lightT);
    }

    private static void ApplySound(float value)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        if (busIndex < 0)
            busIndex = 0;

        _ = TryApplyDeviceVolume(value);

        var linear = Mathf.Clamp(value / 100f, 0f, 1f);
        if (linear <= 0.0001f)
        {
            AudioServer.SetBusMute(busIndex, true);
            AudioServer.SetBusVolumeDb(busIndex, -80f);
            return;
        }

        AudioServer.SetBusMute(busIndex, false);
        AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(linear));
    }

    private static bool TryApplyDeviceVolume(float value)
    {
        if (!IsWindows())
            return false;

        try
        {
            var ok = TryApplyCoreAudioVolume(value) || TryApplyLegacyWaveOutVolume(value);
            _deviceVolumeSupported = ok;
            return ok;
        }
        catch
        {
            _deviceVolumeSupported = false;
            return false;
        }
    }

    private bool TryApplyDeviceBrightness(float value)
    {
        if (!IsWindows())
            return false;

        try
        {
            var ok = TryApplyWmiBrightness(value) || TryApplyGammaBrightness(value);
            _deviceBrightnessSupported = ok;
            return ok;
        }
        catch
        {
            _deviceBrightnessSupported = false;
            return false;
        }
    }

    private static ushort[] BuildGammaRamp(float value)
    {
        var clamped = Mathf.Clamp(value, 0f, 100f);
        var factor = Mathf.Lerp(0.38f, 1.55f, clamped / 100f);
        var ramp = new ushort[256 * 3];
        for (var i = 0; i < 256; i++)
        {
            var adjusted = Mathf.Clamp(Mathf.RoundToInt(i * 256f * factor), 0, 65535);
            var sample = (ushort)adjusted;
            ramp[i] = sample;
            ramp[i + 256] = sample;
            ramp[i + 512] = sample;
        }

        return ramp;
    }

    private static bool IsWindows()
    {
        return string.Equals(OS.GetName(), "Windows", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryApplyCoreAudioVolume(float value)
    {
        IMMDeviceEnumerator enumerator = null;
        IMMDevice endpointDevice = null;
        object endpointObject = null;

        var coInitResult = CoInitializeEx(IntPtr.Zero, CoinitMultithreaded);
        var shouldUninitCom = coInitResult >= 0;
        if (coInitResult < 0 && coInitResult != RpcEChangedMode)
            return false;


        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out endpointDevice) < 0 ||
                endpointDevice == null)
            {
                return false;
            }

            var endpointVolumeGuid = IAudioEndpointVolumeGuid;
            if (endpointDevice.Activate(ref endpointVolumeGuid, ClsCtxInprocServer, IntPtr.Zero, out endpointObject) < 0 ||
                endpointObject is not IAudioEndpointVolume endpointVolume)
            {
                return false;
            }

            var scalar = Mathf.Clamp(value / 100f, 0f, 1f);
            var eventContext = Guid.Empty;
            _ = endpointVolume.SetMute(scalar <= 0.0001f, ref eventContext);
            return endpointVolume.SetMasterVolumeLevelScalar(scalar, ref eventContext) >= 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            ReleaseComObjectSafe(endpointObject);
            ReleaseComObjectSafe(endpointDevice);
            ReleaseComObjectSafe(enumerator);

            if (shouldUninitCom)
                CoUninitialize();
        }
    }

    private static bool TryApplyLegacyWaveOutVolume(float value)
    {
        var clamped = Mathf.Clamp(value, 0f, 100f);
        var level = (uint)Mathf.RoundToInt((clamped / 100f) * 65535f);
        var packed = (level & 0xFFFFu) | (level << 16);
        return waveOutSetVolume(IntPtr.Zero, packed) == 0;
    }

    private static bool TryApplyWmiBrightness(float value)
    {
        object scope = null;
        object query = null;
        object searcher = null;

        try
        {
            var scopeType = Type.GetType("System.Management.ManagementScope, System.Management");
            var queryType = Type.GetType("System.Management.ObjectQuery, System.Management");
            var searcherType = Type.GetType("System.Management.ManagementObjectSearcher, System.Management");
            if (scopeType == null || queryType == null || searcherType == null)
                return false;

            scope = Activator.CreateInstance(scopeType, @"\\.\root\wmi");
            scopeType.GetMethod("Connect", Type.EmptyTypes)?.Invoke(scope, null);

            query = Activator.CreateInstance(queryType, "SELECT * FROM WmiMonitorBrightnessMethods");
            searcher = Activator.CreateInstance(searcherType, scope, query);
            var collection = searcherType.GetMethod("Get", Type.EmptyTypes)?.Invoke(searcher, null) as IEnumerable;
            if (collection == null)
                return false;

            var targetBrightness = (byte)Mathf.RoundToInt(Mathf.Clamp(value, 0f, 100f));
            var hadItems = false;
            foreach (var item in collection)
            {
                hadItems = true;
                var invoke = item?.GetType().GetMethod("InvokeMethod", new[] { typeof(string), typeof(object[]) });
                invoke?.Invoke(item, new object[] { "WmiSetBrightness", new object[] { 1u, targetBrightness } });
                if (item is IDisposable disposableItem)
                    disposableItem.Dispose();
            }

            return hadItems;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (searcher is IDisposable disposableSearcher)
                disposableSearcher.Dispose();
            if (query is IDisposable disposableQuery)
                disposableQuery.Dispose();
            if (scope is IDisposable disposableScope)
                disposableScope.Dispose();
        }
    }

    private static bool TryApplyGammaBrightness(float value)
    {
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
            return false;

        try
        {
            var ramp = BuildGammaRamp(value);
            return SetDeviceGammaRamp(screenDc, ramp);
        }
        finally
        {
            _ = ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static void ReleaseComObjectSafe(object comObject)
    {
        if (comObject == null)
            return;

        try
        {
            if (Marshal.IsComObject(comObject))
                _ = Marshal.ReleaseComObject(comObject);
        }
        catch
        {
            // Ignore cleanup errors for COM release.
        }
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool SetDeviceGammaRamp(IntPtr hDc, ushort[] ramp);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr pvReserved, uint dwCoInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    private static StyleBoxFlat BuildPanelStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius
        };
        style.SetBorderWidthAll(borderWidth);
        return style;
    }

    private static StyleBoxFlat BuildButtonStyle(Color background, Color border, int borderWidth, int radius)
    {
        var style = BuildPanelStyle(background, border, borderWidth, radius);
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        return style;
    }
}
