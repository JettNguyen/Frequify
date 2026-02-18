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
    private GenrePreset? _selectedPreset;
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
                ApplyPreset();
                OnPropertyChanged(nameof(SelectedPresetDescription));
                RaiseCommandStates();
            }
        }
    }

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
            ProgressPercent = 20;
            var metrics = await Task.Run(() => _audioAnalyzer.Analyze(_sourceAudio));
            _sourceMetrics = metrics;
            _metrics = _masteredMetrics ?? _sourceMetrics;
            ProgressPercent = 80;

            SpectrumPoints = CreateSpectrumPoints(metrics.Spectrum);
            OnPropertyChanged(nameof(SpectrumPoints));

            RefreshAnalysisBindings();
            RefreshComparisonBindings();
            ProgressPercent = 100;
            StatusMessage = "Analysis complete. Review the metrics before mastering.";
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
            ProgressPercent = 15;
            var chain = new MasteringChain(Settings);
            var output = await Task.Run(() => chain.Process(_sourceAudio));
            ProgressPercent = 70;

            _masteredAudio = output;
            _sourceMetrics ??= await Task.Run(() => _audioAnalyzer.Analyze(_sourceAudio));
            LowGrText = $"{chain.LowBandGainReductionDb:F1} dB";
            MidGrText = $"{chain.MidBandGainReductionDb:F1} dB";
            HighGrText = $"{chain.HighBandGainReductionDb:F1} dB";
            OnPropertyChanged(nameof(LowGrText));
            OnPropertyChanged(nameof(MidGrText));
            OnPropertyChanged(nameof(HighGrText));

            _masteredMetrics = await Task.Run(() => _audioAnalyzer.Analyze(output));
            _metrics = _masteredMetrics;
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

        SelectedPreset.Apply(Settings);
        UpdateEqResponse();
        OnPropertyChanged(nameof(SelectedPresetDescription));
        StatusMessage = $"Preset applied: {SelectedPreset.Name}. Sliders and controls are updated.";
    }

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
