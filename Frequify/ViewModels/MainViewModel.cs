using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Frequify.Audio;
using Frequify.DSP;
using Frequify.DSP.Analysis;
using Frequify.Infrastructure;
using Frequify.Models;
using Microsoft.Win32;

namespace Frequify.ViewModels;

/// <summary>
/// Coordinates file loading, analysis, mastering, preview, and export interactions.
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const string AutoPresetName = "Auto";
    private const int GraphWidth = 336;
    private const int SpectrumHeight = 96;
    private const int EqHeight = 100;
    private const int PreviewHeight = 82;

    private readonly AudioLoader _audioLoader;
    private readonly AudioAnalyzer _audioAnalyzer;
    private readonly AudioPlayer _audioPlayer;
    private readonly AudioExporter _audioExporter;

    private AudioFileData? _sourceAudio;
    private AudioFileData? _masteredAudio;

    private string _loadedFilePath = "No file loaded";
    private string _statusMessage = "Load a WAV or MP3 file to begin.";
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _isAdvancedMode;
    private double _progressPercent;
    private AnalysisMetrics? _metrics;
    private AnalysisMetrics? _sourceMetrics;
    private AnalysisMetrics? _masteredMetrics;
    private AutoPresetSnapshot? _autoPresetSnapshot;
    private GenrePreset? _selectedPreset;
    private double _autoMasteringStrength = 1.15;
    private bool _isPreviewOriginalView = true;
    private double _previewPositionSeconds;
    private double _previewDurationSeconds = 1;
    private double _previewVolume = 0.9;
    private bool _syncingPreviewPosition;
    private readonly DispatcherTimer _previewTimer;
    private AudioFileData? _loadedPreviewAudio;

    /// <summary>
    /// Initializes a new main view model.
    /// </summary>
    public MainViewModel()
    {
        _audioLoader = new AudioLoader();
        _audioAnalyzer = new AudioAnalyzer();
        _audioPlayer = new AudioPlayer();
        _audioExporter = new AudioExporter();

        Settings = new MasteringSettings();
        Presets = new ObservableCollection<GenrePreset>(GenrePresets.GetDefaults());
        SelectedPreset = Presets.FirstOrDefault();

        SpectrumPoints = new PointCollection();
        EqResponsePoints = new PointCollection();
        OriginalWaveformPoints = new PointCollection();
        OriginalVisualizerPoints = new PointCollection();
        MasteredWaveformPoints = new PointCollection();
        MasteredVisualizerPoints = new PointCollection();

        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _previewTimer.Tick += (_, _) => SyncPreviewTransport();

        AttachEqChangeHandlers();
        UpdateEqResponse();

        LoadFileCommand = new RelayCommand(LoadFile, () => !IsBusy);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsBusy && _sourceAudio is not null);
        MasterCommand = new AsyncRelayCommand(MasterAsync, () => !IsBusy && _sourceAudio is not null);
        PreviewOriginalCommand = new RelayCommand(() => { SetPreviewOriginalView(); PlayPreview(); }, () => !IsBusy && _sourceAudio is not null);
        PreviewMasteredCommand = new RelayCommand(() => { SetPreviewMasteredView(); PlayPreview(); }, () => !IsBusy && _masteredAudio is not null);
        PreviewCommand = new RelayCommand(TogglePreview, () => !IsBusy && CurrentPreviewAudio is not null);
        RestartPreviewCommand = new RelayCommand(RestartPreview, () => !IsBusy && CurrentPreviewAudio is not null);
        SetPreviewOriginalViewCommand = new RelayCommand(SetPreviewOriginalView, () => !IsBusy && _sourceAudio is not null);
        SetPreviewMasteredViewCommand = new RelayCommand(SetPreviewMasteredView, () => !IsBusy && _masteredAudio is not null);
        ExportCommand = new RelayCommand(Export, () => !IsBusy && _masteredAudio is not null);
        ApplyPresetCommand = new RelayCommand(ApplyPreset, () => !IsBusy && SelectedPreset is not null);
        SetSimpleModeCommand = new RelayCommand(SetSimpleMode, () => !IsBusy);
        SetAdvancedModeCommand = new RelayCommand(SetAdvancedMode, () => !IsBusy);

        _audioPlayer.SetVolume((float)_previewVolume);
    }

    /// <summary>
    /// Gets chain settings exposed to the UI.
    /// </summary>
    public MasteringSettings Settings { get; }

    /// <summary>
    /// Gets available genre presets.
    /// </summary>
    public ObservableCollection<GenrePreset> Presets { get; }

    /// <summary>
    /// Gets supported loudness targets in LUFS.
    /// </summary>
    public IReadOnlyList<int> TargetLufsOptions { get; } = new[] { -16, -14, -12, -9 };

    /// <summary>
    /// Gets or sets whether the detailed advanced control mode is active.
    /// </summary>
    public bool IsAdvancedMode
    {
        get => _isAdvancedMode;
        private set
        {
            if (SetProperty(ref _isAdvancedMode, value))
            {
                OnPropertyChanged(nameof(CurrentModeText));
            }
        }
    }

    /// <summary>
    /// Gets current mode label.
    /// </summary>
    public string CurrentModeText => IsAdvancedMode ? "Advanced mode" : "Simple mode";

    /// <summary>
    /// Gets or sets selected preset.
    /// </summary>
    public GenrePreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value is not null)
            {
                OnPropertyChanged(nameof(IsAutoPresetSelected));
                ApplyPreset();
                OnPropertyChanged(nameof(SelectedPresetDescription));
                RaiseCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets whether the Auto preset is currently selected.
    /// </summary>
    public bool IsAutoPresetSelected => string.Equals(SelectedPreset?.Name, AutoPresetName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets auto-mastering strength multiplier.
    /// </summary>
    public double AutoMasteringStrength
    {
        get => _autoMasteringStrength;
        set
        {
            var clamped = Math.Clamp(value, 0.5, 2.0);
            if (SetProperty(ref _autoMasteringStrength, clamped))
            {
                OnPropertyChanged(nameof(AutoMasteringStrengthText));
                RefreshAutoPresetFromCurrentAnalysis(applyIfSelected: IsAutoPresetSelected);
            }
        }
    }

    /// <summary>
    /// Gets formatted auto-mastering strength text.
    /// </summary>
    public string AutoMasteringStrengthText => $"{AutoMasteringStrength:P0}";

    /// <summary>
    /// Gets selected preset explanation.
    /// </summary>
    public string SelectedPresetDescription => SelectedPreset?.Description ?? "Select a preset to see its mastering intent.";

    /// <summary>
    /// Gets file path display text.
    /// </summary>
    public string LoadedFilePath
    {
        get => _loadedFilePath;
        private set => SetProperty(ref _loadedFilePath, value);
    }

    /// <summary>
    /// Gets or sets current status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets or sets current user-facing error text.
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>
    /// Gets whether an error message is currently available.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Gets or sets operation progress in percent.
    /// </summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    /// <summary>
    /// Gets whether a background task is running.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets integrated LUFS text.
    /// </summary>
    public string IntegratedLufsText => _metrics is null ? "-" : $"{_metrics.IntegratedLufs:F1} LUFS";

    /// <summary>
    /// Gets true peak text.
    /// </summary>
    public string TruePeakText => _metrics is null ? "-" : $"{_metrics.TruePeakDbTp:F1} dBTP";

    /// <summary>
    /// Gets RMS level text.
    /// </summary>
    public string RmsText => _metrics is null ? "-" : $"{_metrics.RmsDbFs:F1} dBFS";

    /// <summary>
    /// Gets crest factor text.
    /// </summary>
    public string CrestFactorText => _metrics is null ? "-" : $"{_metrics.CrestFactorDb:F1} dB";

    /// <summary>
    /// Gets source integrated loudness text.
    /// </summary>
    public string SourceIntegratedLufsText => FormatMetric(_sourceMetrics?.IntegratedLufs, "LUFS");

    /// <summary>
    /// Gets source true peak text.
    /// </summary>
    public string SourceTruePeakText => FormatMetric(_sourceMetrics?.TruePeakDbTp, "dBTP");

    /// <summary>
    /// Gets source RMS text.
    /// </summary>
    public string SourceRmsText => FormatMetric(_sourceMetrics?.RmsDbFs, "dBFS");

    /// <summary>
    /// Gets source crest factor text.
    /// </summary>
    public string SourceCrestFactorText => FormatMetric(_sourceMetrics?.CrestFactorDb, "dB");

    /// <summary>
    /// Gets mastered integrated loudness text.
    /// </summary>
    public string MasteredIntegratedLufsText => FormatMetric(_masteredMetrics?.IntegratedLufs, "LUFS");

    /// <summary>
    /// Gets mastered true peak text.
    /// </summary>
    public string MasteredTruePeakText => FormatMetric(_masteredMetrics?.TruePeakDbTp, "dBTP");

    /// <summary>
    /// Gets mastered RMS text.
    /// </summary>
    public string MasteredRmsText => FormatMetric(_masteredMetrics?.RmsDbFs, "dBFS");

    /// <summary>
    /// Gets mastered crest factor text.
    /// </summary>
    public string MasteredCrestFactorText => FormatMetric(_masteredMetrics?.CrestFactorDb, "dB");

    /// <summary>
    /// Gets low-band gain reduction text.
    /// </summary>
    public string LowGrText { get; private set; } = "0.0 dB";

    /// <summary>
    /// Gets mid-band gain reduction text.
    /// </summary>
    public string MidGrText { get; private set; } = "0.0 dB";

    /// <summary>
    /// Gets high-band gain reduction text.
    /// </summary>
    public string HighGrText { get; private set; } = "0.0 dB";

    /// <summary>
    /// Gets spectrum polyline points.
    /// </summary>
    public PointCollection SpectrumPoints { get; private set; }

    /// <summary>
    /// Gets equalizer response polyline points.
    /// </summary>
    public PointCollection EqResponsePoints { get; private set; }

    /// <summary>
    /// Gets waveform points for the original track.
    /// </summary>
    public PointCollection OriginalWaveformPoints { get; private set; }

    /// <summary>
    /// Gets energy visualizer points for the original track.
    /// </summary>
    public PointCollection OriginalVisualizerPoints { get; private set; }

    /// <summary>
    /// Gets waveform points for the mastered track.
    /// </summary>
    public PointCollection MasteredWaveformPoints { get; private set; }

    /// <summary>
    /// Gets energy visualizer points for the mastered track.
    /// </summary>
    public PointCollection MasteredVisualizerPoints { get; private set; }

    /// <summary>
    /// Gets whether original preview view is selected.
    /// </summary>
    public bool IsPreviewOriginalView
    {
        get => _isPreviewOriginalView;
        private set
        {
            if (SetProperty(ref _isPreviewOriginalView, value))
            {
                OnPropertyChanged(nameof(IsPreviewMasteredView));
                OnPropertyChanged(nameof(CurrentPreviewLabel));
                RaiseCommandStates();
            }
        }
    }

    /// <summary>
    /// Gets whether mastered preview view is selected.
    /// </summary>
    public bool IsPreviewMasteredView => !IsPreviewOriginalView;

    /// <summary>
    /// Gets current preview label.
    /// </summary>
    public string CurrentPreviewLabel => IsPreviewOriginalView ? "Original" : "Mastered";

    /// <summary>
    /// Gets or sets playback cursor position in seconds.
    /// </summary>
    public double PreviewPositionSeconds
    {
        get => _previewPositionSeconds;
        set
        {
            var clamped = Math.Clamp(value, 0, Math.Max(PreviewDurationSeconds, 0.001));
            if (SetProperty(ref _previewPositionSeconds, clamped))
            {
                OnPropertyChanged(nameof(PreviewPositionText));
                OnPropertyChanged(nameof(PreviewPlayheadX));
                if (!_syncingPreviewPosition)
                {
                    _audioPlayer.Seek(clamped);
                }
            }
        }
    }

    /// <summary>
    /// Gets current preview duration in seconds.
    /// </summary>
    public double PreviewDurationSeconds
    {
        get => _previewDurationSeconds;
        private set
        {
            if (SetProperty(ref _previewDurationSeconds, Math.Max(value, 0.001)))
            {
                OnPropertyChanged(nameof(PreviewDurationText));
                OnPropertyChanged(nameof(PreviewPlayheadX));
            }
        }
    }

    /// <summary>
    /// Gets or sets preview playback volume from 0 to 1.
    /// </summary>
    public double PreviewVolume
    {
        get => _previewVolume;
        set
        {
            var clamped = Math.Clamp(value, 0, 1);
            if (SetProperty(ref _previewVolume, clamped))
            {
                _audioPlayer.SetVolume((float)clamped);
            }
        }
    }

    /// <summary>
    /// Gets formatted playback position text.
    /// </summary>
    public string PreviewPositionText => FormatTime(PreviewPositionSeconds);

    /// <summary>
    /// Gets formatted playback duration text.
    /// </summary>
    public string PreviewDurationText => FormatTime(PreviewDurationSeconds);

    /// <summary>
    /// Gets waveform playhead x position in graph units.
    /// </summary>
    public double PreviewPlayheadX => Math.Clamp((PreviewPositionSeconds / Math.Max(PreviewDurationSeconds, 0.001)) * GraphWidth, 0, GraphWidth);

    /// <summary>
    /// Gets play/pause text for preview transport button.
    /// </summary>
    public string PreviewToggleText => IsPreviewPlaying ? "Pause" : "Play";

    /// <summary>
    /// Gets whether preview is currently playing.
    /// </summary>
    public bool IsPreviewPlaying => _audioPlayer.PlaybackState == NAudio.Wave.PlaybackState.Playing;

    /// <summary>
    /// Gets the load command.
    /// </summary>
    public RelayCommand LoadFileCommand { get; }

    /// <summary>
    /// Gets the analyze command.
    /// </summary>
    public AsyncRelayCommand AnalyzeCommand { get; }

    /// <summary>
    /// Gets the master command.
    /// </summary>
    public AsyncRelayCommand MasterCommand { get; }

    /// <summary>
    /// Gets the preview command.
    /// </summary>
    public RelayCommand PreviewCommand { get; }

    /// <summary>
    /// Gets the preview-original command.
    /// </summary>
    public RelayCommand PreviewOriginalCommand { get; }

    /// <summary>
    /// Gets the preview-mastered command.
    /// </summary>
    public RelayCommand PreviewMasteredCommand { get; }

    /// <summary>
    /// Gets the restart-preview command.
    /// </summary>
    public RelayCommand RestartPreviewCommand { get; }

    /// <summary>
    /// Gets the command to select original preview view.
    /// </summary>
    public RelayCommand SetPreviewOriginalViewCommand { get; }

    /// <summary>
    /// Gets the command to select mastered preview view.
    /// </summary>
    public RelayCommand SetPreviewMasteredViewCommand { get; }

    /// <summary>
    /// Gets the export command.
    /// </summary>
    public RelayCommand ExportCommand { get; }

    /// <summary>
    /// Gets the apply-preset command.
    /// </summary>
    public RelayCommand ApplyPresetCommand { get; }

    /// <summary>
    /// Gets command to enable simple mode.
    /// </summary>
    public RelayCommand SetSimpleModeCommand { get; }

    /// <summary>
    /// Gets command to enable advanced mode.
    /// </summary>
    public RelayCommand SetAdvancedModeCommand { get; }

    /// <summary>
    /// Releases playback resources.
    /// </summary>
    public void Dispose()
    {
        _previewTimer.Stop();
        _audioPlayer.Dispose();
    }

    private void LoadFile()
    {
        ErrorMessage = string.Empty;
        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3|WAV Audio|*.wav|MP3 Audio|*.mp3",
            Multiselect = false,
            Title = "Load audio file"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        LoadFromPath(dialog.FileName);
    }

    /// <summary>
    /// Loads an audio file by absolute path.
    /// </summary>
    /// <param name="filePath">Audio file path.</param>
    public void LoadFromPath(string filePath)
    {
        ErrorMessage = string.Empty;

        try
        {
            StopPreviewInternal();
            _sourceAudio = _audioLoader.LoadAudio(filePath);
            _masteredAudio = null;
            _metrics = null;
            _sourceMetrics = null;
            _masteredMetrics = null;
            LowGrText = "0.0 dB";
            MidGrText = "0.0 dB";
            HighGrText = "0.0 dB";
            OnPropertyChanged(nameof(LowGrText));
            OnPropertyChanged(nameof(MidGrText));
            OnPropertyChanged(nameof(HighGrText));
            LoadedFilePath = filePath;
            StatusMessage = "File loaded and analyzed. Ready for preview or mastering.";
            _sourceMetrics = _audioAnalyzer.Analyze(_sourceAudio);
            _metrics = _sourceMetrics;
            RefreshAutoPresetFromCurrentAnalysis(applyIfSelected: IsAutoPresetSelected);
            SpectrumPoints = CreateSpectrumPoints(_sourceMetrics.Spectrum);
            OnPropertyChanged(nameof(SpectrumPoints));
            BuildComparisonVisuals();
            SetPreviewOriginalView();
            EnsurePreviewTrackLoaded();
            SyncPreviewTransport();
            RefreshAnalysisBindings();
            RefreshComparisonBindings();
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load file: {ex.Message}";
            StatusMessage = "Load failed. Review the message and try a valid WAV or MP3 file.";
        }
    }

    private async Task AnalyzeAsync()
    {
        if (_sourceAudio is null)
        {
            return;
        }

        await RunBusyTask("Analyzing audio...", async () =>
        {
            await UpdateProgressStageAsync(8, "Preparing analysis buffers...");
            await UpdateProgressStageAsync(18, "Scanning waveform content...");

            var metrics = await Task.Run(() => _audioAnalyzer.Analyze(_sourceAudio));

            await UpdateProgressStageAsync(55, "Calculating loudness, peak, and dynamics...");
            _sourceMetrics = metrics;
            _metrics = _masteredMetrics ?? _sourceMetrics;

            await UpdateProgressStageAsync(72, "Building spectrum profile...");

            SpectrumPoints = CreateSpectrumPoints(metrics.Spectrum);
            OnPropertyChanged(nameof(SpectrumPoints));

            await UpdateProgressStageAsync(84, "Preparing automatic mastering profile...");
            RefreshAutoPresetFromCurrentAnalysis(applyIfSelected: IsAutoPresetSelected);

            if (IsAutoPresetSelected)
            {
                await UpdateProgressStageAsync(92, "Applying auto mastering settings to controls...");
            }
            else
            {
                await UpdateProgressStageAsync(92, "Auto mastering profile cached for this track...");
            }

            RefreshAnalysisBindings();
            RefreshComparisonBindings();
            ProgressPercent = 100;
            StatusMessage = IsAutoPresetSelected
                ? "Analysis complete. Auto mastering settings were refreshed from this track."
                : "Analysis complete. Auto mastering is ready for this track when you select Auto.";
        });
    }

    private async Task MasterAsync()
    {
        if (_sourceAudio is null)
        {
            return;
        }

        await RunBusyTask("Applying mastering chain...", async () =>
        {
            await UpdateProgressStageAsync(10, "Preparing mastering stages...");
            await UpdateProgressStageAsync(22, "Calibrating EQ and dynamics...");

            var chain = new MasteringChain(Settings);
            var stageProgress = new Progress<MasteringChain.StageUpdate>(update =>
            {
                ProgressPercent = Math.Clamp(update.Percent, 0, 100);
                StatusMessage = update.Message;
            });

            await UpdateProgressStageAsync(34, "Processing signal through mastering chain...");
            var output = await Task.Run(() => chain.Process(_sourceAudio, stageProgress));

            await UpdateProgressStageAsync(62, "Measuring gain reduction and loudness...");

            _masteredAudio = output;
            _sourceMetrics ??= await Task.Run(() => _audioAnalyzer.Analyze(_sourceAudio));
            LowGrText = $"{chain.LowBandGainReductionDb:F1} dB";
            MidGrText = $"{chain.MidBandGainReductionDb:F1} dB";
            HighGrText = $"{chain.HighBandGainReductionDb:F1} dB";
            OnPropertyChanged(nameof(LowGrText));
            OnPropertyChanged(nameof(MidGrText));
            OnPropertyChanged(nameof(HighGrText));

            await UpdateProgressStageAsync(78, "Analyzing mastered result...");
            _masteredMetrics = await Task.Run(() => _audioAnalyzer.Analyze(output));
            _metrics = _masteredMetrics;

            await UpdateProgressStageAsync(88, "Updating visualizations and preview...");
            SpectrumPoints = CreateSpectrumPoints(_masteredMetrics.Spectrum);
            OnPropertyChanged(nameof(SpectrumPoints));
            BuildComparisonVisuals();
            if (IsPreviewMasteredView)
            {
                EnsurePreviewTrackLoaded();
                SyncPreviewTransport();
            }
            RefreshAnalysisBindings();
            RefreshComparisonBindings();
            ProgressPercent = 100;
            StatusMessage = "Mastering complete. You can preview and export the result.";
            RaiseCommandStates();
        });
    }

    private void PreviewOriginal()
    {
        SetPreviewOriginalView();
        PlayPreview();
    }

    private void PreviewMastered()
    {
        SetPreviewMasteredView();
        PlayPreview();
    }

    private void Preview()
    {
        TogglePreview();
    }

    private void TogglePreview()
    {
        if (IsPreviewPlaying)
        {
            PausePreview();
            return;
        }

        PlayPreview();
    }

    private void PlayPreview()
    {
        ErrorMessage = string.Empty;
        if (!EnsurePreviewTrackLoaded())
        {
            StatusMessage = "Preview could not start for this track.";
            return;
        }

        if (_audioPlayer.Play())
        {
            _previewTimer.Start();
            StatusMessage = $"Playing {CurrentPreviewLabel.ToLowerInvariant()} track preview.";
            OnPropertyChanged(nameof(PreviewToggleText));
        }
        else
        {
            StatusMessage = "Preview could not start for this track.";
            OnPropertyChanged(nameof(PreviewToggleText));
        }

        RaiseCommandStates();
    }

    private void PausePreview()
    {
        _audioPlayer.Pause();
        _previewTimer.Stop();
        SyncPreviewTransport();
        StatusMessage = "Playback paused.";
        OnPropertyChanged(nameof(PreviewToggleText));
        RaiseCommandStates();
    }

    private void RestartPreview()
    {
        if (!EnsurePreviewTrackLoaded())
        {
            StatusMessage = "Preview could not start for this track.";
            return;
        }

        if (_audioPlayer.Restart())
        {
            _previewTimer.Start();
            SyncPreviewTransport();
            StatusMessage = "Playback restarted from the beginning.";
            OnPropertyChanged(nameof(PreviewToggleText));
        }
        else
        {
            StatusMessage = "Preview could not start for this track.";
        }

        RaiseCommandStates();
    }

    private void SetPreviewOriginalView()
    {
        if (_sourceAudio is null)
        {
            return;
        }

        var wasPlaying = IsPreviewPlaying;
        IsPreviewOriginalView = true;
        EnsurePreviewTrackLoaded();
        if (wasPlaying)
        {
            _audioPlayer.Play();
            _previewTimer.Start();
        }
        SyncPreviewTransport();
    }

    private void SetPreviewMasteredView()
    {
        if (_masteredAudio is null)
        {
            return;
        }

        var wasPlaying = IsPreviewPlaying;
        IsPreviewOriginalView = false;
        EnsurePreviewTrackLoaded();
        if (wasPlaying)
        {
            _audioPlayer.Play();
            _previewTimer.Start();
        }
        SyncPreviewTransport();
    }

    private bool EnsurePreviewTrackLoaded()
    {
        var audio = CurrentPreviewAudio;
        if (audio is null)
        {
            return false;
        }

        if (ReferenceEquals(_loadedPreviewAudio, audio) && _audioPlayer.DurationSeconds > 0)
        {
            return true;
        }

        _previewTimer.Stop();
        _audioPlayer.Stop();
        var loaded = _audioPlayer.Load(audio);
        if (!loaded)
        {
            return false;
        }

        _loadedPreviewAudio = audio;

        _audioPlayer.SetVolume((float)PreviewVolume);
        PreviewDurationSeconds = _audioPlayer.DurationSeconds;
        PreviewPositionSeconds = Math.Clamp(PreviewPositionSeconds, 0, PreviewDurationSeconds);
        if (PreviewPositionSeconds > 0)
        {
            _audioPlayer.Seek(PreviewPositionSeconds);
        }

        RaiseCommandStates();
        return true;
    }

    private void SyncPreviewTransport()
    {
        _syncingPreviewPosition = true;
        try
        {
            PreviewDurationSeconds = _audioPlayer.DurationSeconds;
            PreviewPositionSeconds = Math.Clamp(_audioPlayer.PositionSeconds, 0, PreviewDurationSeconds);
        }
        finally
        {
            _syncingPreviewPosition = false;
        }

        if (_audioPlayer.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
        {
            _previewTimer.Stop();
        }

        OnPropertyChanged(nameof(PreviewToggleText));
        RaiseCommandStates();
    }

    private void StopPreviewInternal()
    {
        _previewTimer.Stop();
        _audioPlayer.Stop();
        _loadedPreviewAudio = null;
        PreviewPositionSeconds = 0;
        PreviewDurationSeconds = 1;
        OnPropertyChanged(nameof(PreviewToggleText));
        RaiseCommandStates();
    }

    private void Export()
    {
        if (_masteredAudio is null)
        {
            return;
        }

        ErrorMessage = string.Empty;
        var dialog = new SaveFileDialog
        {
            Filter = "WAV Audio|*.wav",
            FileName = "mastered.wav",
            Title = "Export mastered WAV"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _audioExporter.ExportWav(dialog.FileName, _masteredAudio);
            StatusMessage = "Export complete.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not export file: {ex.Message}";
            StatusMessage = "Export failed. Check path permissions and try again.";
        }
    }

    private void ApplyPreset()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        if (string.Equals(SelectedPreset.Name, AutoPresetName, StringComparison.OrdinalIgnoreCase))
        {
            ApplyAutoPreset();
            return;
        }

        SelectedPreset.Apply(Settings);
        UpdateEqResponse();
        OnPropertyChanged(nameof(SelectedPresetDescription));
        StatusMessage = $"Preset applied: {SelectedPreset.Name}. Sliders and controls are updated.";
    }

    private void ApplyAutoPreset()
    {
        if (_sourceAudio is null)
        {
            StatusMessage = "Auto preset is ready. Load a track first so settings can adapt to the input audio.";
            return;
        }

        _sourceMetrics ??= _audioAnalyzer.Analyze(_sourceAudio);
        RefreshAutoPresetFromCurrentAnalysis(applyIfSelected: false);
        if (_autoPresetSnapshot is null)
        {
            StatusMessage = "Auto preset could not analyze this track. Try Analyze first, then reapply.";
            return;
        }

        ApplyAutoPresetSnapshot(_autoPresetSnapshot);
        UpdateEqResponse();
        OnPropertyChanged(nameof(SelectedPresetDescription));
        StatusMessage = $"Preset applied: {SelectedPreset?.Name}. Mix-preserving mastering adjustments were applied based on analysis.";
    }

    private void RefreshAutoPresetFromCurrentAnalysis(bool applyIfSelected)
    {
        if (_sourceMetrics is null)
        {
            return;
        }

        _autoPresetSnapshot = BuildAutoPresetSnapshot(_sourceMetrics, AutoMasteringStrength);
        if (applyIfSelected && _autoPresetSnapshot is not null)
        {
            ApplyAutoPresetSnapshot(_autoPresetSnapshot);
            UpdateEqResponse();
        }
    }

    private AutoPresetSnapshot BuildAutoPresetSnapshot(AnalysisMetrics metrics, double strength)
    {
        var lowEnergy = BandAverage(metrics.Spectrum, 0.00, 0.20);
        var midEnergy = BandAverage(metrics.Spectrum, 0.20, 0.70);
        var highEnergy = BandAverage(metrics.Spectrum, 0.70, 1.00);

        var lowToMidRatio = lowEnergy / Math.Max(midEnergy, 1e-4);
        var highToMidRatio = highEnergy / Math.Max(midEnergy, 1e-4);
        var avgEnergy = (lowEnergy + midEnergy + highEnergy) / 3.0;
        var midToAverageRatio = midEnergy / Math.Max(avgEnergy, 1e-4);

        var integratedLufs = metrics.IntegratedLufs;
        var truePeakDbTp = metrics.TruePeakDbTp;
        var rmsDbFs = metrics.RmsDbFs;
        var crestDb = metrics.CrestFactorDb;

        var dynamicsFactor = Math.Clamp((crestDb - 8.0) / 8.0, 0.0, 1.0);
        var loudnessLiftFactor = Math.Clamp((-12.0 - integratedLufs) / 12.0, 0.0, 1.0);
        var strengthFactor = Math.Clamp(strength, 0.5, 2.0);
        var compressionIntensity = Math.Clamp((0.35 * dynamicsFactor) + (0.40 * loudnessLiftFactor), 0.0, 1.0) * (0.8 + (0.35 * (strengthFactor - 1.0)));
        compressionIntensity = Math.Clamp(compressionIntensity, 0.0, 1.0);

        var bassHeavyFactor = Math.Clamp((lowToMidRatio - 1.10) / 0.70, 0.0, 1.0);
        var bassLightFactor = Math.Clamp((0.92 - lowToMidRatio) / 0.50, 0.0, 1.0);
        var brightFactor = Math.Clamp((highToMidRatio - 1.08) / 0.55, 0.0, 1.0);
        var darkFactor = Math.Clamp((0.90 - highToMidRatio) / 0.45, 0.0, 1.0);
        var midHoleFactor = Math.Clamp((0.95 - midToAverageRatio) / 0.35, 0.0, 1.0);

        var peakRiskFactor = Math.Clamp((truePeakDbTp + 0.5) / 0.8, 0.0, 1.0);
        var highPassCutoffHz = Math.Clamp(24.0 + (bassLightFactor * 9.0 * strengthFactor) + (peakRiskFactor * 4.0 * strengthFactor) - (bassHeavyFactor * 6.0), 20.0, 40.0);

        var lowShelfFrequencyHz = Math.Clamp(110.0 + ((lowToMidRatio - 1.0) * 20.0), 80.0, 180.0);
        var midBellFrequencyHz = Math.Clamp(1400.0 + ((1.0 - midToAverageRatio) * 800.0), 700.0, 2800.0);
        var highShelfFrequencyHz = Math.Clamp(8500.0 + ((highToMidRatio - 1.0) * 1000.0), 6500.0, 12000.0);

        var lowShelfGainDb = Math.Clamp(((bassLightFactor * 1.4) - (bassHeavyFactor * 1.0)) * strengthFactor, -2.8, 2.8);
        var midBellGainDb = Math.Clamp((midHoleFactor * 1.2) * strengthFactor, -1.2, 2.2);
        var highShelfGainDb = Math.Clamp(((darkFactor * 1.3) - (brightFactor * 0.9)) * strengthFactor, -2.4, 2.6);

        var lowShelfQ = Math.Clamp(0.70 + ((bassHeavyFactor + bassLightFactor) * 0.25), 0.55, 1.20);
        var midBellQ = Math.Clamp(1.20 + (midHoleFactor * 0.65), 1.00, 2.20);
        var highShelfQ = Math.Clamp(0.70 + ((brightFactor + darkFactor) * 0.20), 0.55, 1.15);

        var thresholdBase = Math.Clamp(rmsDbFs + 8.5 - (compressionIntensity * 2.3 * strengthFactor), -30.0, -12.0);

        var lowThresholdDb = Math.Clamp(thresholdBase - 1.5, -40.0, -6.0);
        var midThresholdDb = Math.Clamp(thresholdBase, -40.0, -6.0);
        var highThresholdDb = Math.Clamp(thresholdBase + 1.5, -40.0, -6.0);

        var lowRatio = Math.Clamp(1.45 + (compressionIntensity * 0.95 * strengthFactor), 1.2, 3.2);
        var midRatio = Math.Clamp(1.35 + (compressionIntensity * 0.85 * strengthFactor), 1.2, 3.0);
        var highRatio = Math.Clamp(1.25 + (compressionIntensity * 0.75 * strengthFactor), 1.1, 2.8);

        var attackBase = Math.Clamp(14.0 + (dynamicsFactor * 14.0), 4.0, 38.0);
        var releaseBase = Math.Clamp(120.0 + (dynamicsFactor * 110.0), 70.0, 280.0);

        var lowAttackMs = Math.Clamp(attackBase + 6.0, 1.0, 80.0);
        var midAttackMs = Math.Clamp(attackBase, 1.0, 80.0);
        var highAttackMs = Math.Clamp(attackBase - 4.0, 1.0, 80.0);

        var lowReleaseMs = Math.Clamp(releaseBase + 35.0, 40.0, 400.0);
        var midReleaseMs = Math.Clamp(releaseBase, 40.0, 400.0);
        var highReleaseMs = Math.Clamp(releaseBase - 20.0, 40.0, 400.0);

        var saturationDrive = Math.Clamp(0.06 + (compressionIntensity * 0.08 * strengthFactor) + ((1.0 - dynamicsFactor) * 0.02), 0.0, 0.35);
        var stereoWidth = Math.Clamp(1.0 + ((highToMidRatio - 1.0) * 0.05 * strengthFactor), 0.90, 1.14);

        var limiterCeilingDbTp = Math.Clamp(-1.0 - (Math.Max(truePeakDbTp + 0.2, 0.0) * 0.20 * strengthFactor), -1.8, -0.8);
        var limiterLookaheadMs = Math.Clamp(2.5 + (dynamicsFactor * 2.5 * strengthFactor) + (Math.Max(truePeakDbTp + 0.8, 0.0) * 0.8 * strengthFactor), 1.2, 8.0);

        var targetLufs = TargetLufsOptions
            .OrderBy(option => Math.Abs(option - integratedLufs))
            .First();

        return new AutoPresetSnapshot(
            highPassCutoffHz,
            lowShelfFrequencyHz,
            lowShelfGainDb,
            lowShelfQ,
            midBellFrequencyHz,
            midBellGainDb,
            midBellQ,
            highShelfFrequencyHz,
            highShelfGainDb,
            highShelfQ,
            lowThresholdDb,
            lowRatio,
            lowAttackMs,
            lowReleaseMs,
            midThresholdDb,
            midRatio,
            midAttackMs,
            midReleaseMs,
            highThresholdDb,
            highRatio,
            highAttackMs,
            highReleaseMs,
            saturationDrive,
            stereoWidth,
            limiterCeilingDbTp,
            limiterLookaheadMs,
            targetLufs);
    }

    private void ApplyAutoPresetSnapshot(AutoPresetSnapshot snapshot)
    {
        Settings.HighPass.IsEnabled = true;
        Settings.Equalizer.IsEnabled = true;
        Settings.Compression.IsEnabled = true;
        Settings.Saturation.IsEnabled = true;
        Settings.Stereo.IsEnabled = true;
        Settings.Limiter.IsEnabled = true;
        Settings.Loudness.IsEnabled = true;

        Settings.HighPass.CutoffHz = snapshot.HighPassCutoffHz;

        Settings.Equalizer.LowShelf.FrequencyHz = snapshot.LowShelfFrequencyHz;
        Settings.Equalizer.LowShelf.GainDb = snapshot.LowShelfGainDb;
        Settings.Equalizer.LowShelf.Q = snapshot.LowShelfQ;

        Settings.Equalizer.MidBell.FrequencyHz = snapshot.MidBellFrequencyHz;
        Settings.Equalizer.MidBell.GainDb = snapshot.MidBellGainDb;
        Settings.Equalizer.MidBell.Q = snapshot.MidBellQ;

        Settings.Equalizer.HighShelf.FrequencyHz = snapshot.HighShelfFrequencyHz;
        Settings.Equalizer.HighShelf.GainDb = snapshot.HighShelfGainDb;
        Settings.Equalizer.HighShelf.Q = snapshot.HighShelfQ;

        Settings.Compression.Low.ThresholdDb = snapshot.LowThresholdDb;
        Settings.Compression.Low.Ratio = snapshot.LowRatio;
        Settings.Compression.Low.AttackMs = snapshot.LowAttackMs;
        Settings.Compression.Low.ReleaseMs = snapshot.LowReleaseMs;

        Settings.Compression.Mid.ThresholdDb = snapshot.MidThresholdDb;
        Settings.Compression.Mid.Ratio = snapshot.MidRatio;
        Settings.Compression.Mid.AttackMs = snapshot.MidAttackMs;
        Settings.Compression.Mid.ReleaseMs = snapshot.MidReleaseMs;

        Settings.Compression.High.ThresholdDb = snapshot.HighThresholdDb;
        Settings.Compression.High.Ratio = snapshot.HighRatio;
        Settings.Compression.High.AttackMs = snapshot.HighAttackMs;
        Settings.Compression.High.ReleaseMs = snapshot.HighReleaseMs;

        Settings.Saturation.Drive = snapshot.SaturationDrive;
        Settings.Stereo.Width = snapshot.StereoWidth;
        Settings.Limiter.CeilingDbTp = snapshot.LimiterCeilingDbTp;
        Settings.Limiter.LookaheadMs = snapshot.LimiterLookaheadMs;
        Settings.Loudness.TargetLufs = snapshot.TargetLufs;
    }

    private async Task UpdateProgressStageAsync(double percent, string status)
    {
        ProgressPercent = Math.Clamp(percent, 0, 100);
        StatusMessage = status;
        await Task.Delay(65);
    }

    private static double BandAverage(IReadOnlyList<double> values, double startRatio, double endRatio)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var start = Math.Clamp((int)Math.Floor(values.Count * startRatio), 0, values.Count - 1);
        var end = Math.Clamp((int)Math.Ceiling(values.Count * endRatio), start + 1, values.Count);

        var sum = 0.0;
        for (var i = start; i < end; i++)
        {
            sum += Math.Clamp(values[i], 0.0, 1.0);
        }

        return sum / Math.Max(1, end - start);
    }

    private sealed record AutoPresetSnapshot(
        double HighPassCutoffHz,
        double LowShelfFrequencyHz,
        double LowShelfGainDb,
        double LowShelfQ,
        double MidBellFrequencyHz,
        double MidBellGainDb,
        double MidBellQ,
        double HighShelfFrequencyHz,
        double HighShelfGainDb,
        double HighShelfQ,
        double LowThresholdDb,
        double LowRatio,
        double LowAttackMs,
        double LowReleaseMs,
        double MidThresholdDb,
        double MidRatio,
        double MidAttackMs,
        double MidReleaseMs,
        double HighThresholdDb,
        double HighRatio,
        double HighAttackMs,
        double HighReleaseMs,
        double SaturationDrive,
        double StereoWidth,
        double LimiterCeilingDbTp,
        double LimiterLookaheadMs,
        int TargetLufs);

    private async Task RunBusyTask(string status, Func<Task> action)
    {
        ErrorMessage = string.Empty;
        IsBusy = true;
        ProgressPercent = 0;
        StatusMessage = status;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Operation failed: {ex.Message}";
            StatusMessage = "The operation could not complete. Adjust settings and retry.";
        }
        finally
        {
            if (ProgressPercent < 100)
            {
                ProgressPercent = 0;
            }

            IsBusy = false;
            RaiseCommandStates();
        }
    }

    private void RefreshAnalysisBindings()
    {
        OnPropertyChanged(nameof(IntegratedLufsText));
        OnPropertyChanged(nameof(TruePeakText));
        OnPropertyChanged(nameof(RmsText));
        OnPropertyChanged(nameof(CrestFactorText));
    }

    private void RefreshComparisonBindings()
    {
        OnPropertyChanged(nameof(SourceIntegratedLufsText));
        OnPropertyChanged(nameof(SourceTruePeakText));
        OnPropertyChanged(nameof(SourceRmsText));
        OnPropertyChanged(nameof(SourceCrestFactorText));
        OnPropertyChanged(nameof(MasteredIntegratedLufsText));
        OnPropertyChanged(nameof(MasteredTruePeakText));
        OnPropertyChanged(nameof(MasteredRmsText));
        OnPropertyChanged(nameof(MasteredCrestFactorText));
    }

    private void RaiseCommandStates()
    {
        LoadFileCommand?.RaiseCanExecuteChanged();
        AnalyzeCommand?.RaiseCanExecuteChanged();
        MasterCommand?.RaiseCanExecuteChanged();
        PreviewOriginalCommand?.RaiseCanExecuteChanged();
        PreviewMasteredCommand?.RaiseCanExecuteChanged();
        PreviewCommand?.RaiseCanExecuteChanged();
        RestartPreviewCommand?.RaiseCanExecuteChanged();
        SetPreviewOriginalViewCommand?.RaiseCanExecuteChanged();
        SetPreviewMasteredViewCommand?.RaiseCanExecuteChanged();
        ExportCommand?.RaiseCanExecuteChanged();
        ApplyPresetCommand?.RaiseCanExecuteChanged();
        SetSimpleModeCommand?.RaiseCanExecuteChanged();
        SetAdvancedModeCommand?.RaiseCanExecuteChanged();
    }

    private AudioFileData? CurrentPreviewAudio => IsPreviewOriginalView ? _sourceAudio : _masteredAudio;

    private void SetSimpleMode()
    {
        IsAdvancedMode = false;
        StatusMessage = "Simple mode enabled. Core controls are shown for quick mastering.";
    }

    private void SetAdvancedMode()
    {
        IsAdvancedMode = true;
        StatusMessage = "Advanced mode enabled. Full stage controls are available.";
    }

    private void AttachEqChangeHandlers()
    {
        Settings.Equalizer.LowShelf.PropertyChanged += (_, _) => UpdateEqResponse();
        Settings.Equalizer.MidBell.PropertyChanged += (_, _) => UpdateEqResponse();
        Settings.Equalizer.HighShelf.PropertyChanged += (_, _) => UpdateEqResponse();
    }

    private void UpdateEqResponse()
    {
        const int points = 180;

        var response = new PointCollection(points);
        for (var i = 0; i < points; i++)
        {
            var frequency = 20.0 * Math.Pow(10, (i / (double)(points - 1)) * Math.Log10(20000.0 / 20.0));

            var db = 0.0;
            db += BandContribution(frequency, Settings.Equalizer.LowShelf.FrequencyHz, Settings.Equalizer.LowShelf.GainDb, 0.6, true);
            db += BandContribution(frequency, Settings.Equalizer.MidBell.FrequencyHz, Settings.Equalizer.MidBell.GainDb, Settings.Equalizer.MidBell.Q, false);
            db += BandContribution(frequency, Settings.Equalizer.HighShelf.FrequencyHz, Settings.Equalizer.HighShelf.GainDb, 0.6, true);

            db = Math.Clamp(db, -12, 12);
            var x = (i / (double)(points - 1)) * GraphWidth;
            var y = EqHeight * (0.5 - db / 24.0);
            response.Add(new Point(x, y));
        }

        EqResponsePoints = response;
        OnPropertyChanged(nameof(EqResponsePoints));
    }

    private static double BandContribution(double frequency, double center, double gainDb, double q, bool shelf)
    {
        if (shelf)
        {
            var ratio = frequency / Math.Max(center, 20);
            var t = Math.Clamp((Math.Log10(Math.Max(ratio, 1e-6)) + 1.2) / 2.4, 0, 1);
            return gainDb * t;
        }

        var sigma = Math.Max(0.08, 1.0 / Math.Max(q, 0.3));
        var distance = Math.Log10(Math.Max(frequency, 20) / Math.Max(center, 20));
        return gainDb * Math.Exp(-(distance * distance) / (2 * sigma * sigma));
    }

    private static PointCollection CreateSpectrumPoints(IReadOnlyList<double> normalizedSpectrum)
    {
        if (normalizedSpectrum.Count == 0)
        {
            return new PointCollection();
        }

        if (normalizedSpectrum.Count == 1)
        {
            var single = Math.Clamp(normalizedSpectrum[0], 0, 1);
            return new PointCollection
            {
                new Point(0, SpectrumHeight - (single * SpectrumHeight))
            };
        }

        var points = new PointCollection(normalizedSpectrum.Count);
        for (var i = 0; i < normalizedSpectrum.Count; i++)
        {
            var x = (i / (double)(normalizedSpectrum.Count - 1)) * GraphWidth;
            var y = SpectrumHeight - Math.Clamp(normalizedSpectrum[i], 0, 1) * SpectrumHeight;
            points.Add(new Point(x, y));
        }

        return points;
    }

    private void BuildComparisonVisuals()
    {
        if (_sourceAudio is null)
        {
            OriginalWaveformPoints = new PointCollection();
            OriginalVisualizerPoints = new PointCollection();
        }
        else
        {
            OriginalWaveformPoints = CreateWaveformPoints(_sourceAudio);
            OriginalVisualizerPoints = CreateEnergyPoints(_sourceAudio);
        }

        if (_masteredAudio is null)
        {
            MasteredWaveformPoints = new PointCollection();
            MasteredVisualizerPoints = new PointCollection();
        }
        else
        {
            MasteredWaveformPoints = CreateWaveformPoints(_masteredAudio);
            MasteredVisualizerPoints = CreateEnergyPoints(_masteredAudio);
        }

        OnPropertyChanged(nameof(OriginalWaveformPoints));
        OnPropertyChanged(nameof(OriginalVisualizerPoints));
        OnPropertyChanged(nameof(MasteredWaveformPoints));
        OnPropertyChanged(nameof(MasteredVisualizerPoints));
    }

    private static PointCollection CreateWaveformPoints(AudioFileData audio)
    {
        const int pointCount = 280;
        var sampleCount = Math.Min(audio.Left.Length, audio.Right.Length);
        if (sampleCount < 2)
        {
            return new PointCollection();
        }

        var points = new PointCollection(pointCount);
        var yCenter = PreviewHeight * 0.5;
        var segment = Math.Max(1, sampleCount / pointCount);
        var maxAbs = 1e-6;
        for (var i = 0; i < sampleCount; i += Math.Max(1, segment * 2))
        {
            var sample = (audio.Left[i] + audio.Right[i]) * 0.5;
            maxAbs = Math.Max(maxAbs, Math.Abs(sample));
        }

        for (var i = 0; i < pointCount; i++)
        {
            var start = i * segment;
            var end = Math.Min(sampleCount, start + segment);
            if (start >= end)
            {
                break;
            }

            var peak = 0.0;
            for (var index = start; index < end; index++)
            {
                var sample = (audio.Left[index] + audio.Right[index]) * 0.5;
                peak = Math.Max(peak, Math.Abs(sample));
            }

            var normalized = Math.Clamp(peak / maxAbs, 0.0, 1.0);
            var signed = (i % 2 == 0 ? -1.0 : 1.0) * normalized;

            var x = i * (GraphWidth / (double)(pointCount - 1));
            var y = yCenter - (signed * (PreviewHeight * 0.42));
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static PointCollection CreateEnergyPoints(AudioFileData audio)
    {
        const int pointCount = 140;
        var sampleCount = Math.Min(audio.Left.Length, audio.Right.Length);
        if (sampleCount < 2)
        {
            return new PointCollection();
        }

        var points = new PointCollection(pointCount);
        var segment = Math.Max(1, sampleCount / pointCount);
        var smoothed = 0.0;
        var maxRms = 1e-6;

        for (var i = 0; i < pointCount; i++)
        {
            var start = i * segment;
            var end = Math.Min(sampleCount, start + segment);
            if (start >= end)
            {
                break;
            }

            var energy = 0.0;
            for (var index = start; index < end; index++)
            {
                var sample = (audio.Left[index] + audio.Right[index]) * 0.5;
                energy += sample * sample;
            }

            maxRms = Math.Max(maxRms, Math.Sqrt(energy / (end - start)));
        }

        for (var i = 0; i < pointCount; i++)
        {
            var start = i * segment;
            var end = Math.Min(sampleCount, start + segment);
            if (start >= end)
            {
                break;
            }

            var energy = 0.0;
            for (var index = start; index < end; index++)
            {
                var sample = (audio.Left[index] + audio.Right[index]) * 0.5;
                energy += sample * sample;
            }

            var rms = Math.Sqrt(energy / (end - start));
            smoothed = (smoothed * 0.72) + (rms * 0.28);
            var level = Math.Clamp(smoothed / maxRms, 0.0, 1.0);

            var x = i * (GraphWidth / (double)(pointCount - 1));
            var y = PreviewHeight - 2 - (level * (PreviewHeight - 4));
            points.Add(new Point(x, y));
        }

        return points;
    }

    private static string FormatMetric(double? value, string unit)
    {
        return value.HasValue ? $"{value.Value:F1} {unit}" : "-";
    }

    private static string FormatTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(seconds, 0));
        return t.TotalHours >= 1
            ? t.ToString(@"hh\:mm\:ss")
            : t.ToString(@"mm\:ss");
    }
}
