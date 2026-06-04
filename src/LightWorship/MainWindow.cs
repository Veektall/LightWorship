using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Wave;

namespace LightWorship
{
    public class MainWindow : Window
    {
        private readonly DataStore _store = new DataStore();
        private readonly BibleRepository _bible;
        private readonly ProjectionWindow _projection = new ProjectionWindow();
        private readonly LiveListeningService _listener = new LiveListeningService();
        private DeepgramLiveListeningService _deepgramListener;
        private GeminiLiveListeningService _geminiListener;
        private LocalWhisperListeningService _whisperListener;
        private VoiceboxListeningService _voiceboxListener;

        private AppSettings _settings;
        private List<Song> _songs;
        private List<MediaItem> _media;
        private List<ScheduleItem> _schedule;
        private Slide _previewSlide;
        private Slide _liveSlide;
        private Slide _frozenSlide;
        private List<Slide> _previewDeck = new List<Slide>();
        private List<Slide> _liveDeck = new List<Slide>();
        private int _liveDeckIndex;

        private TextBox _bibleSearch;
        private ComboBox _bibleVersion;
        private ComboBox _bibleBook;
        private ComboBox _bibleChapter;
        private ListBox _bibleResults;
        private ListBox _songList;
        private TextBox _songSearch;
        private ListBox _songSections;
        private TextBox _songTitle;
        private TextBox _songAuthor;
        private TextBox _songCopyright;
        private TextBox _songCcli;
        private TextBox _songKey;
        private TextBox _songTimeSignature;
        private TextBox _songTempo;
        private TextBox _songDuration;
        private TextBox _sectionLabel;
        private TextBox _sectionLyrics;
        private ListBox _mediaList;
        private ComboBox _mediaFitMode;
        private ListBox _scheduleList;
        private TextBox _scheduleNotes;
        private TextBox _aiHeard;
        private ListBox _aiResults;
        private ListBox _transcriptList;
        private TextBlock _listeningStatus;
        private CheckBox _autoPreviewSuggestions;
        private CheckBox _externalScriptureIntegration;
        private TextBox _confidenceThreshold;
        private readonly List<TranscriptItem> _transcriptItems = new List<TranscriptItem>();
        private TextBox _textTitle;
        private TextBox _textBody;
        private string _textBackgroundPath;
        private Border _previewBox;
        private Border _liveBox;
        private TextBlock _status;
        private Border _connectionDot;
        private TextBlock _connectionStatusText;
        private ProgressBar _volumeMeter;
        private CheckBox _loadScheduleStartup;
        private CheckBox _openProjectorStartup;
        private CheckBox _startListeningStartup;
        private ComboBox _templateSelector;
        private TextBox _songSequence;
        private ComboBox _textTemplateSelector;
        private readonly List<Border> _sidebarButtons = new List<Border>();
        private string _lastInjectedReference = "";
        private DateTime _lastInjectedAt = DateTime.MinValue;

        public MainWindow()
        {
            _bible = new BibleRepository(_store);
            _settings = _store.Load(_store.SettingsPath, new AppSettings());
            ApplyTranscriptionDefaults();
            _songs = _store.Load(_store.SongsPath, SeedSongs());
            _media = _store.Load(_store.MediaPath, new List<MediaItem>());
            if (_settings.LoadLastScheduleOnStartup)
            {
                _schedule = _store.Load(_store.SchedulePath, new List<ScheduleItem>());
            }
            else
            {
                _schedule = new List<ScheduleItem>();
            }
            _bible.Load();

            Title = "LightWorship";
            Width = 1280;
            Height = 780;
            MinWidth = 1024;
            MinHeight = 640;
            Background = Brush("#14171c");
            Foreground = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");
            KeyDown += OnKeyDown;
            Closed += (s, e) =>
            {
                _listener.Dispose();
                if (_deepgramListener != null) _deepgramListener.Dispose();
                if (_geminiListener != null) _geminiListener.Dispose();
                if (_whisperListener != null) _whisperListener.Dispose();
                if (_voiceboxListener != null) _voiceboxListener.Dispose();
            };
            _listener.PhraseRecognized += OnPhraseRecognized;
            _listener.ListenFailed += (s, ex) => Dispatcher.Invoke(() => SetStatus("Listening failed: " + ex.Message));

            BuildUi();
            RefreshAll();
            SetStatus("Ready. KJV loaded: " + _bible.Verses.Count + " verses.");

            if (_settings.OpenProjectorOnStartup)
            {
                ShowProjection(true);
            }

            if (_settings.StartListeningOnStartup)
            {
                StartListening();
            }
        }

        private void ApplyTranscriptionDefaults()
        {
            if (String.IsNullOrWhiteSpace(_settings.GeminiApiKey))
            {
                _settings.GeminiApiKey = AppSettings.DefaultGeminiApiKey;
            }

            if (String.IsNullOrWhiteSpace(_settings.GeminiModel))
            {
                _settings.GeminiModel = AppSettings.DefaultGeminiModel;
            }

            if (String.IsNullOrWhiteSpace(_settings.TranscriptionEngine))
            {
                _settings.TranscriptionEngine = "Deepgram";
            }
            else if (String.Equals(_settings.TranscriptionEngine, "GeminiLive", StringComparison.OrdinalIgnoreCase))
            {
                _settings.TranscriptionEngine = "Deepgram";
            }

            if (String.IsNullOrWhiteSpace(_settings.DeepgramApiKey))
            {
                _settings.DeepgramApiKey = AppSettings.DefaultDeepgramApiKey;
            }

            if (String.IsNullOrWhiteSpace(_settings.DeepgramModel))
            {
                _settings.DeepgramModel = "nova-3";
            }

            var hadEngine = !String.IsNullOrWhiteSpace(_settings.TranscriptionEngine);
            var hadExe = !String.IsNullOrWhiteSpace(_settings.WhisperExecutablePath);
            var hadModel = !String.IsNullOrWhiteSpace(_settings.WhisperModelPath);
            var exe = FindUpward("tools\\whisper\\bin-x64\\Release\\whisper-cli.exe");
            var model = FindUpward("tools\\whisper\\models\\ggml-tiny.en.bin");

            if (String.IsNullOrWhiteSpace(_settings.WhisperExecutablePath) && !String.IsNullOrWhiteSpace(exe))
            {
                _settings.WhisperExecutablePath = exe;
            }

            if (String.IsNullOrWhiteSpace(_settings.WhisperModelPath) && !String.IsNullOrWhiteSpace(model))
            {
                _settings.WhisperModelPath = model;
            }

            if (!hadEngine && !hadExe && !hadModel &&
                !String.IsNullOrWhiteSpace(_settings.WhisperExecutablePath) &&
                !String.IsNullOrWhiteSpace(_settings.WhisperModelPath))
            {
                _settings.TranscriptionEngine = "GeminiLive";
            }
        }

        private static string FindUpward(string relativePath)
        {
            var starts = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Environment.CurrentDirectory
            };

            foreach (var start in starts)
            {
                var current = new DirectoryInfo(start);
                while (current != null)
                {
                    var candidate = Path.Combine(current.FullName, relativePath);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    current = current.Parent;
                }
            }

            return "";
        }

        private void BuildUi()
        {
            var root = new Grid { Margin = new Thickness(12) };
            // Three-column layout: Left Navigation (160), Middle Content (2.5*), Right Viewports (1.2*)
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var tabs = new TabControl { Background = Brush("#1b2028"), BorderBrush = Brush("#303743") };
            
            // Programmatically hide standard TabControl horizontal headers
            var tabTemplate = new ControlTemplate(typeof(TabControl));
            var presenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            presenterFactory.SetValue(ContentPresenter.NameProperty, "PART_SelectedContentHost");
            presenterFactory.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
            tabTemplate.VisualTree = presenterFactory;
            tabs.Template = tabTemplate;

            tabs.Items.Add(Tab("Bible", BuildBibleTab()));
            tabs.Items.Add(Tab("Songs", BuildSongsTab()));
            tabs.Items.Add(Tab("Media", BuildMediaTab()));
            tabs.Items.Add(Tab("Text", BuildTextTab()));
            tabs.Items.Add(Tab("Schedule", BuildScheduleTab()));
            tabs.Items.Add(Tab("AI Assist", BuildAiAssistTab()));
            tabs.Items.Add(Tab("Settings", BuildSettingsTab()));
            Grid.SetColumn(tabs, 1);
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            // Left obsidian vertical sidebar panel
            var sidebar = CreateSidebar(tabs);
            Grid.SetColumn(sidebar, 0);
            Grid.SetRow(sidebar, 1);
            root.Children.Add(sidebar);

            var toolbar = new DockPanel { Margin = new Thickness(0, 0, 0, 10), Background = Brush("#101318") };
            var leftTools = new WrapPanel { Margin = new Thickness(8) };
            leftTools.Children.Add(Button("New Schedule", (s, e) => NewSchedule()));
            leftTools.Children.Add(Button("Add Song", (s, e) => { tabs.SelectedIndex = 1; NewSong(); }));
            leftTools.Children.Add(Button("Add Scripture", (s, e) => { tabs.SelectedIndex = 0; _bibleSearch.Focus(); }));
            leftTools.Children.Add(Button("Add Media", (s, e) => { tabs.SelectedIndex = 2; ImportMedia("Image"); }));
            leftTools.Children.Add(Button("AI Assist", (s, e) => { tabs.SelectedIndex = 5; _aiHeard.Focus(); }));
            leftTools.Children.Add(Button("Settings", (s, e) => { tabs.SelectedIndex = 6; }));
            toolbar.Children.Add(leftTools);

            var rightTools = new WrapPanel { Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Right };
            rightTools.Children.Add(Button("Black", (s, e) => SendSpecial(Slide.Black())));
            rightTools.Children.Add(Button("Clear", (s, e) => { _projection.ClearText(); SetStatus("Text cleared from output."); }));
            rightTools.Children.Add(Button("Logo", (s, e) => ShowLogo()));
            DockPanel.SetDock(rightTools, Dock.Right);
            toolbar.Children.Add(rightTools);
            Grid.SetRow(toolbar, 0);
            Grid.SetColumnSpan(toolbar, 3);
            root.Children.Add(toolbar);

            var right = new Grid { Margin = new Thickness(12, 0, 0, 0) };
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(right, 2);
            Grid.SetRow(right, 1);
            root.Children.Add(right);

            _previewBox = PreviewPanel("Preview");
            _liveBox = PreviewPanel("Live");
            
            // Enforce strict 16:9 aspect ratios on preview viewports
            var previewContainer = new AspectRatioPanel { Background = Brushes.Transparent };
            previewContainer.Children.Add(_previewBox);
            Grid.SetRow(previewContainer, 0);
            right.Children.Add(previewContainer);

            var liveContainer = new AspectRatioPanel { Background = Brushes.Transparent };
            liveContainer.Children.Add(_liveBox);
            Grid.SetRow(liveContainer, 1);
            right.Children.Add(liveContainer);

            var controls = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            controls.Children.Add(Button("Go Live", (s, e) => GoLive()));
            controls.Children.Add(Button("Prev Slide", (s, e) => PreviousLiveSlide()));
            controls.Children.Add(Button("Next Slide", (s, e) => NextLiveSlide()));
            controls.Children.Add(Button("Projector", (s, e) => ShowProjection(true)));
            controls.Children.Add(Button("Test Window", (s, e) => ShowProjection(false)));
            controls.Children.Add(Button("Black", (s, e) => SendSpecial(Slide.Black())));
            controls.Children.Add(Button("Clear Text", (s, e) => { _projection.ClearText(); SetStatus("Text cleared from output."); }));
            controls.Children.Add(Button("Logo", (s, e) => ShowLogo()));
            controls.Children.Add(Button("Freeze", (s, e) => Freeze()));
            Grid.SetRow(controls, 2);
            right.Children.Add(controls);

            // Rich premium status bar with status message, audio volume meter, and connection state dot
            var statusBarBorder = new Border
            {
                Background = Brush("#101318"),
                Margin = new Thickness(0, 10, 0, 0),
                Padding = new Thickness(12, 6, 12, 6)
            };
            var statusBar = new Grid();
            statusBarBorder.Child = statusBar;
            statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _status = new TextBlock
            {
                Foreground = Brush("#94a3b8"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_status, 0);
            statusBar.Children.Add(_status);

            var rightStatusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var micLabel = new TextBlock
            {
                Text = "🎤 Mic:",
                Foreground = Brush("#94a3b8"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            rightStatusPanel.Children.Add(micLabel);

            _volumeMeter = new ProgressBar
            {
                Width = 90,
                Height = 8,
                Value = 0,
                Minimum = 0,
                Maximum = 100,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brush("#1f2937"),
                Foreground = Brush("#a855f7"),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 16, 0)
            };
            rightStatusPanel.Children.Add(_volumeMeter);

            _connectionDot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Brush("#ef4444"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            rightStatusPanel.Children.Add(_connectionDot);

            _connectionStatusText = new TextBlock
            {
                Text = "Offline",
                Foreground = Brush("#94a3b8"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightStatusPanel.Children.Add(_connectionStatusText);

            Grid.SetColumn(rightStatusPanel, 1);
            statusBar.Children.Add(rightStatusPanel);

            Grid.SetRow(statusBarBorder, 2);
            Grid.SetColumnSpan(statusBarBorder, 3);
            root.Children.Add(statusBarBorder);
        }

        private UIElement BuildBibleTab()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var top = new DockPanel { Margin = new Thickness(8) };
            _bibleSearch = Input("John 3:16 or phrase search");
            _bibleSearch.KeyDown += (s, e) => { if (e.Key == Key.Enter) { SearchBible(); e.Handled = true; } };
            DockPanel.SetDock(_bibleSearch, Dock.Left);
            top.Children.Add(_bibleSearch);
            top.Children.Add(Button("Search", (s, e) => SearchBible()));
            top.Children.Add(Button("Preview", (s, e) => PreviewBibleSelection()));
            top.Children.Add(Button("Add To Schedule", (s, e) => AddPreviewToSchedule()));
            Grid.SetRow(top, 0);
            grid.Children.Add(top);

            var browse = new WrapPanel { Margin = new Thickness(8, 0, 8, 0) };
            browse.Children.Add(Text("Browse"));
            _bibleVersion = Select(_store.GetBibleFiles().Keys.Cast<object>().ToList());
            _bibleVersion.SelectionChanged += (s, e) => LoadSelectedBibleVersion();
            browse.Children.Add(_bibleVersion);
            _bibleBook = Select(_bible.Books.Cast<object>().ToList());
            _bibleBook.SelectionChanged += (s, e) => LoadBibleChapters();
            _bibleChapter = Select(new List<object>());
            browse.Children.Add(_bibleBook);
            browse.Children.Add(_bibleChapter);
            browse.Children.Add(Button("Load Chapter", (s, e) => LoadSelectedChapter()));
            Grid.SetRow(browse, 1);
            grid.Children.Add(browse);

            _bibleResults = List();
            _bibleResults.MouseDoubleClick += (s, e) => { PreviewBibleSelection(); GoLive(); };
            Grid.SetRow(_bibleResults, 2);
            grid.Children.Add(_bibleResults);
            _bibleVersion.SelectedItem = _store.GetBibleFiles().ContainsKey(_settings.DefaultBibleVersion) ? _settings.DefaultBibleVersion : "KJV";
            _bibleBook.SelectedItem = "Genesis";
            return grid;
        }

        private UIElement BuildSongsTab()
        {
            var grid = new Grid { Margin = new Thickness(8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });

            var left = new DockPanel();
            _songSearch = Input("Search songs");
            _songSearch.KeyUp += (s, e) => RefreshSongs();
            DockPanel.SetDock(_songSearch, Dock.Top);
            left.Children.Add(_songSearch);
            var buttons = new WrapPanel();
            buttons.Children.Add(Button("New", (s, e) => NewSong()));
            buttons.Children.Add(Button("Save", (s, e) => SaveSong()));
            buttons.Children.Add(Button("Preview", (s, e) => PreviewSongSection()));
            buttons.Children.Add(Button("Add To Schedule", (s, e) => AddPreviewToSchedule()));
            DockPanel.SetDock(buttons, Dock.Bottom);
            left.Children.Add(buttons);
            _songList = List();
            _songList.SelectionChanged += (s, e) => LoadSelectedSong();
            left.Children.Add(_songList);
            grid.Children.Add(left);

            var editor = new Grid { Margin = new Thickness(12, 0, 0, 0) };
            editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editor.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.45, GridUnitType.Star) });
            editor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            editor.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.9, GridUnitType.Star) });

            _songTitle = Input("Song title");
            _songAuthor = Input("Author");
            _songCopyright = Input("Copyright");
            _songCcli = Input("CCLI number");
            _songKey = Input("Key");
            _songTimeSignature = Input("Time signature");
            _songTempo = Input("Tempo");
            _songDuration = Input("Duration");
            _songSequence = Input("Sequence (e.g., V1, C, V2, C, B, C)");
            _songSequence.MinWidth = 280;
            _songSections = List();
            _songSections.SelectionChanged += (s, e) => LoadSelectedSection();
            _sectionLabel = Input("Verse 1 / Chorus / Bridge");
            _sectionLyrics = Input("Lyrics");
            _sectionLyrics.AcceptsReturn = true;
            _sectionLyrics.TextWrapping = TextWrapping.Wrap;
            _sectionLyrics.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            editor.Children.Add(Labeled("Title", _songTitle, 0));
            editor.Children.Add(Labeled("Author", _songAuthor, 1));
            var meta = new WrapPanel();
            meta.Children.Add(_songCopyright);
            meta.Children.Add(_songCcli);
            meta.Children.Add(_songKey);
            meta.Children.Add(_songTimeSignature);
            meta.Children.Add(_songTempo);
            meta.Children.Add(_songDuration);
            meta.Children.Add(_songSequence);
            Grid.SetRow(meta, 2);
            editor.Children.Add(meta);
            Grid.SetRow(_songSections, 3);
            editor.Children.Add(_songSections);
            var sectionButtons = new WrapPanel();
            sectionButtons.Children.Add(Button("Add Section", (s, e) => AddSection()));
            sectionButtons.Children.Add(Button("Update Section", (s, e) => UpdateSection()));
            sectionButtons.Children.Add(Button("Delete Section", (s, e) => DeleteSection()));
            Grid.SetRow(sectionButtons, 4);
            editor.Children.Add(sectionButtons);
            var sectionEditor = new Grid();
            sectionEditor.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sectionEditor.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sectionEditor.Children.Add(_sectionLabel);
            Grid.SetRow(_sectionLyrics, 1);
            sectionEditor.Children.Add(_sectionLyrics);
            Grid.SetRow(sectionEditor, 5);
            editor.Children.Add(sectionEditor);

            Grid.SetColumn(editor, 1);
            grid.Children.Add(editor);
            return grid;
        }

        private UIElement BuildMediaTab()
        {
            var grid = TwoRow();
            var buttons = new WrapPanel { Margin = new Thickness(8) };
            buttons.Children.Add(Button("Import Image", (s, e) => ImportMedia("Image")));
            buttons.Children.Add(Button("Import Video", (s, e) => ImportMedia("Video")));
            _mediaFitMode = Select(new List<object> { "Fill", "Fit", "Stretch", "Center" });
            _mediaFitMode.SelectedItem = "Fill";
            buttons.Children.Add(_mediaFitMode);
            buttons.Children.Add(Button("Preview", (s, e) => PreviewMedia()));
            buttons.Children.Add(Button("Add To Schedule", (s, e) => AddPreviewToSchedule()));
            Grid.SetRow(buttons, 0);
            grid.Children.Add(buttons);
            _mediaList = List();
            _mediaList.MouseDoubleClick += (s, e) => { PreviewMedia(); GoLive(); };
            Grid.SetRow(_mediaList, 1);
            grid.Children.Add(_mediaList);
            return grid;
        }

        private UIElement BuildTextTab()
        {
            var grid = TwoRow();
            var top = new StackPanel { Margin = new Thickness(8) };
            _textTitle = Input("Slide title");
            _textBody = Input("Text to project");
            _textBody.AcceptsReturn = true;
            _textBody.TextWrapping = TextWrapping.Wrap;
            _textBody.Height = 220;
            top.Children.Add(_textTitle);
            top.Children.Add(_textBody);
            
            var templatePanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            templatePanel.Children.Add(Text("Theme Template:"));
            _textTemplateSelector = Select(SlideDeckService.Templates.Cast<object>().ToList());
            _textTemplateSelector.SelectedItem = SlideDeckService.Templates.FirstOrDefault(t => t.Name == _settings.SelectedTemplateName) ?? SlideDeckService.Templates[0];
            templatePanel.Children.Add(_textTemplateSelector);
            top.Children.Add(templatePanel);

            var buttons = new WrapPanel();
            buttons.Children.Add(Button("Preview Text", (s, e) => PreviewTextSlide()));
            buttons.Children.Add(Button("Background Image", (s, e) =>
            {
                var dlg = FileDialog("Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*");
                if (dlg.ShowDialog() == true)
                {
                    _textBackgroundPath = dlg.FileName;
                    SetStatus("Text slide background selected: " + Path.GetFileName(dlg.FileName));
                }
            }));
            buttons.Children.Add(Button("Add To Schedule", (s, e) => AddPreviewToSchedule()));
            top.Children.Add(buttons);
            Grid.SetRow(top, 0);
            grid.Children.Add(top);
            return grid;
        }

        private UIElement BuildScheduleTab()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var buttons = new WrapPanel { Margin = new Thickness(8) };
            buttons.Children.Add(Button("Preview", (s, e) => PreviewScheduleItem()));
            buttons.Children.Add(Button("Go Live", (s, e) => { PreviewScheduleItem(); GoLive(); }));
            buttons.Children.Add(Button("Up", (s, e) => MoveSchedule(-1)));
            buttons.Children.Add(Button("Down", (s, e) => MoveSchedule(1)));
            buttons.Children.Add(Button("Remove", (s, e) => RemoveScheduleItem()));
            buttons.Children.Add(Button("Save", (s, e) => SaveSchedule()));
            Grid.SetRow(buttons, 0);
            grid.Children.Add(buttons);
            _scheduleList = List();
            _scheduleList.SelectionChanged += (s, e) => LoadScheduleNotes();
            _scheduleList.MouseDoubleClick += (s, e) => { PreviewScheduleItem(); GoLive(); };
            Grid.SetRow(_scheduleList, 1);
            grid.Children.Add(_scheduleList);
            var notesPanel = new DockPanel { Margin = new Thickness(8) };
            notesPanel.Children.Add(Text("Notes"));
            _scheduleNotes = Input("Notes for selected schedule item");
            _scheduleNotes.AcceptsReturn = true;
            _scheduleNotes.TextWrapping = TextWrapping.Wrap;
            _scheduleNotes.Height = 80;
            notesPanel.Children.Add(_scheduleNotes);
            notesPanel.Children.Add(Button("Save Notes", (s, e) => SaveScheduleNotes()));
            Grid.SetRow(notesPanel, 2);
            grid.Children.Add(notesPanel);
            return grid;
        }

        private UIElement BuildAiAssistTab()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.9, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.1, GridUnitType.Star) });
            var top = new StackPanel { Margin = new Thickness(8) };
            top.Children.Add(Text("Live transcription and scripture/song suggestion queue."));
            _aiHeard = Input("Heard phrase");
            _aiHeard.KeyDown += (s, e) => { if (e.Key == Key.Enter) { RunAiAssist(); e.Handled = true; } };
            top.Children.Add(_aiHeard);
            var buttons = new WrapPanel();
            buttons.Children.Add(Button("Analyze", (s, e) => RunAiAssist()));
            buttons.Children.Add(Button("Start Listening", (s, e) => StartListening()));
            buttons.Children.Add(Button("Stop Listening", (s, e) => StopListening()));
            buttons.Children.Add(Button("Preview Selected", (s, e) => PreviewAiSelection()));
            buttons.Children.Add(Button("Add To Schedule", (s, e) => { PreviewAiSelection(); AddPreviewToSchedule(); }));
            buttons.Children.Add(Button("Clear Transcript", (s, e) => ClearTranscript()));
            top.Children.Add(buttons);
            var options = new WrapPanel();
            _autoPreviewSuggestions = new CheckBox
            {
                Content = "Auto-preview top suggestion",
                IsChecked = _settings.AutoPreviewLiveSuggestions,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 12, 8)
            };
            _autoPreviewSuggestions.Checked += (s, e) => SaveListeningOptions();
            _autoPreviewSuggestions.Unchecked += (s, e) => SaveListeningOptions();
            _confidenceThreshold = Input(_settings.ListeningConfidenceThreshold.ToString("0.00"));
            _confidenceThreshold.MinWidth = 80;
            _confidenceThreshold.Width = 90;
            options.Children.Add(_autoPreviewSuggestions);
            _externalScriptureIntegration = new CheckBox
            {
                Content = "Send detected Bible reference to EasyWorship/sermon app",
                IsChecked = _settings.ExternalScriptureIntegrationEnabled,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 12, 8)
            };
            _externalScriptureIntegration.Checked += (s, e) => SaveListeningOptions();
            _externalScriptureIntegration.Unchecked += (s, e) => SaveListeningOptions();
            options.Children.Add(_externalScriptureIntegration);
            options.Children.Add(Text("Engine: " + GetTranscriptionEngineLabel()));
            options.Children.Add(Text("Min confidence"));
            options.Children.Add(_confidenceThreshold);
            options.Children.Add(Button("Save Listening Options", (s, e) => SaveListeningOptions()));
            top.Children.Add(options);
            _listeningStatus = Text("Listening: off");
            top.Children.Add(_listeningStatus);
            Grid.SetRow(top, 0);
            grid.Children.Add(top);

            _transcriptList = List();
            Grid.SetRow(_transcriptList, 1);
            grid.Children.Add(_transcriptList);

            _aiResults = List();
            _aiResults.MouseDoubleClick += (s, e) => { PreviewAiSelection(); GoLive(); };
            Grid.SetRow(_aiResults, 2);
            grid.Children.Add(_aiResults);
            return grid;
        }

        private UIElement BuildSettingsTab()
        {
            var stack = new StackPanel { Margin = new Thickness(12) };
            stack.Children.Add(Text("Default output screen index"));
            var screen = Input(_settings.OutputScreenIndex.ToString());
            stack.Children.Add(screen);
            
            stack.Children.Add(Text("Default Bible version"));
            var defaultBible = Select(_store.GetBibleFiles().Keys.Cast<object>().ToList());
            defaultBible.SelectedItem = _settings.DefaultBibleVersion;
            stack.Children.Add(defaultBible);
            
            stack.Children.Add(Text("Logo image"));
            var logo = Input(_settings.LogoImagePath ?? "");
            stack.Children.Add(logo);
            
            stack.Children.Add(Text("Media library folder"));
            var mediaLibrary = Input(String.IsNullOrWhiteSpace(_settings.MediaLibraryPath) ? Path.Combine(_store.Root, "Media", "Library") : _settings.MediaLibraryPath);
            stack.Children.Add(mediaLibrary);
            
            stack.Children.Add(Text("Live transcription engine"));
            var transcriptionEngine = Select(new List<object> { "VoiceboxLocal", "Deepgram", "GeminiLive", "WindowsSpeech", "LocalWhisper" });
            transcriptionEngine.SelectedItem = String.IsNullOrWhiteSpace(_settings.TranscriptionEngine) ? "Deepgram" : _settings.TranscriptionEngine;
            stack.Children.Add(transcriptionEngine);
            
            stack.Children.Add(Text("Audio input"));
            var audioInput = Select(GetAudioInputChoices());
            audioInput.SelectedItem = GetSelectedAudioInputChoice();
            stack.Children.Add(audioInput);
            
            stack.Children.Add(Text("Deepgram API key"));
            var deepgramKey = Input(String.IsNullOrWhiteSpace(_settings.DeepgramApiKey) ? AppSettings.DefaultDeepgramApiKey : _settings.DeepgramApiKey);
            stack.Children.Add(deepgramKey);
            
            stack.Children.Add(Text("Deepgram model"));
            var deepgramModel = Input(String.IsNullOrWhiteSpace(_settings.DeepgramModel) ? "nova-3" : _settings.DeepgramModel);
            stack.Children.Add(deepgramModel);

            stack.Children.Add(Text("Voicebox server URL"));
            var voiceboxUrl = Input(String.IsNullOrWhiteSpace(_settings.VoiceboxServerUrl) ? "http://127.0.0.1:17493" : _settings.VoiceboxServerUrl);
            stack.Children.Add(voiceboxUrl);

            stack.Children.Add(Text("Voicebox model"));
            var voiceboxModel = Input(String.IsNullOrWhiteSpace(_settings.VoiceboxModel) ? "turbo" : _settings.VoiceboxModel);
            stack.Children.Add(voiceboxModel);

            stack.Children.Add(Text("Voicebox language"));
            var voiceboxLanguage = Input(String.IsNullOrWhiteSpace(_settings.VoiceboxLanguage) ? "en" : _settings.VoiceboxLanguage);
            stack.Children.Add(voiceboxLanguage);

            stack.Children.Add(Text("Voicebox chunk seconds"));
            var voiceboxChunkSeconds = Input(_settings.VoiceboxChunkSeconds <= 0 ? "5" : _settings.VoiceboxChunkSeconds.ToString());
            stack.Children.Add(voiceboxChunkSeconds);
            
            stack.Children.Add(Text("Gemini API key"));
            var geminiKey = Input(_settings.GeminiApiKey ?? AppSettings.DefaultGeminiApiKey);
            stack.Children.Add(geminiKey);
            
            stack.Children.Add(Text("Gemini Live model"));
            var geminiModel = Input(_settings.GeminiModel ?? AppSettings.DefaultGeminiModel);
            stack.Children.Add(geminiModel);
            
            stack.Children.Add(Text("Whisper executable"));
            var whisperExe = Input(_settings.WhisperExecutablePath ?? "");
            stack.Children.Add(whisperExe);
            
            stack.Children.Add(Text("Whisper model"));
            var whisperModel = Input(_settings.WhisperModelPath ?? "");
            stack.Children.Add(whisperModel);
            
            stack.Children.Add(Text("Whisper chunk seconds"));
            var whisperChunkSeconds = Input(_settings.WhisperChunkSeconds <= 0 ? "4" : _settings.WhisperChunkSeconds.ToString());
            stack.Children.Add(whisperChunkSeconds);

            stack.Children.Add(Text("External scripture target window title"));
            var externalTargetTitle = Input(String.IsNullOrWhiteSpace(_settings.ExternalTargetWindowTitle) ? "EasyWorship" : _settings.ExternalTargetWindowTitle);
            stack.Children.Add(externalTargetTitle);

            stack.Children.Add(Text("External scripture input hotkey"));
            var externalHotkey = Input(_settings.ExternalScriptureInputHotkey ?? "");
            stack.Children.Add(externalHotkey);

            stack.Children.Add(Text("External focus delay milliseconds"));
            var externalDelay = Input(_settings.ExternalFocusDelayMs <= 0 ? "350" : _settings.ExternalFocusDelayMs.ToString());
            stack.Children.Add(externalDelay);

            var externalSendEnter = new CheckBox
            {
                Content = "Press Enter after sending reference",
                IsChecked = _settings.ExternalSendEnterAfterReference,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 8)
            };
            stack.Children.Add(externalSendEnter);

            stack.Children.Add(Text("Startup behaviors"));
            _loadScheduleStartup = new CheckBox
            {
                Content = "Load last active schedule on startup",
                IsChecked = _settings.LoadLastScheduleOnStartup,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 8)
            };
            stack.Children.Add(_loadScheduleStartup);

            _openProjectorStartup = new CheckBox
            {
                Content = "Open projector fullscreen on startup",
                IsChecked = _settings.OpenProjectorOnStartup,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 8)
            };
            stack.Children.Add(_openProjectorStartup);

            _startListeningStartup = new CheckBox
            {
                Content = "Start live microphone listening on startup",
                IsChecked = _settings.StartListeningOnStartup,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 4, 0, 8)
            };
            stack.Children.Add(_startListeningStartup);

            stack.Children.Add(Text("Global slide theme template"));
            _templateSelector = Select(SlideDeckService.Templates.Cast<object>().ToList());
            _templateSelector.SelectedItem = SlideDeckService.Templates.FirstOrDefault(t => t.Name == _settings.SelectedTemplateName) ?? SlideDeckService.Templates[0];
            stack.Children.Add(_templateSelector);

            var buttons = new WrapPanel();
            buttons.Children.Add(Button("Browse Logo", (s, e) =>
            {
                var dlg = FileDialog("Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*");
                if (dlg.ShowDialog() == true) logo.Text = dlg.FileName;
            }));
            buttons.Children.Add(Button("Use Default Media Folder", (s, e) =>
            {
                mediaLibrary.Text = Path.Combine(_store.Root, "Media", "Library");
            }));
            buttons.Children.Add(Button("Browse Whisper Exe", (s, e) =>
            {
                var dlg = FileDialog("Executable files|*.exe|All files|*.*");
                if (dlg.ShowDialog() == true) whisperExe.Text = dlg.FileName;
            }));
            buttons.Children.Add(Button("Browse Whisper Model", (s, e) =>
            {
                var dlg = FileDialog("Whisper model files|*.bin|All files|*.*");
                if (dlg.ShowDialog() == true) whisperModel.Text = dlg.FileName;
            }));
            buttons.Children.Add(Button("Import Bible JSON", (s, e) =>
            {
                var dlg = FileDialog("Bible JSON files|*.json|All files|*.*");
                if (dlg.ShowDialog() == true)
                {
                    var name = Path.GetFileNameWithoutExtension(dlg.FileName).ToUpperInvariant();
                    _store.ImportBibleFile(dlg.FileName, name);
                    SetStatus("Bible imported. Restart the app or reopen Settings to select it.");
                }
            }));
            buttons.Children.Add(Button("Test Deepgram", (s, e) => TestDeepgramConnection(deepgramKey.Text)));
            buttons.Children.Add(Button("Test External Scripture Send", (s, e) =>
            {
                var previousEnabled = _settings.ExternalScriptureIntegrationEnabled;
                var previousTarget = _settings.ExternalTargetWindowTitle;
                var previousHotkey = _settings.ExternalScriptureInputHotkey;
                var previousDelay = _settings.ExternalFocusDelayMs;
                var previousEnter = _settings.ExternalSendEnterAfterReference;

                _settings.ExternalScriptureIntegrationEnabled = true;
                _settings.ExternalTargetWindowTitle = externalTargetTitle.Text;
                _settings.ExternalScriptureInputHotkey = externalHotkey.Text;
                int delay;
                if (Int32.TryParse(externalDelay.Text, out delay)) _settings.ExternalFocusDelayMs = Math.Max(100, Math.Min(2000, delay));
                _settings.ExternalSendEnterAfterReference = externalSendEnter.IsChecked == true;

                var result = ExternalScriptureInjector.Send("Genesis 1:3", _settings);
                _settings.ExternalScriptureIntegrationEnabled = previousEnabled;
                _settings.ExternalTargetWindowTitle = previousTarget;
                _settings.ExternalScriptureInputHotkey = previousHotkey;
                _settings.ExternalFocusDelayMs = previousDelay;
                _settings.ExternalSendEnterAfterReference = previousEnter;

                SetStatus(result.Success ? result.Message : "External scripture send failed: " + result.Message);
            }));
            buttons.Children.Add(Button("Save Settings", (s, e) =>
            {
                int screenIndex;
                if (Int32.TryParse(screen.Text, out screenIndex)) _settings.OutputScreenIndex = screenIndex;
                _settings.DefaultBibleVersion = defaultBible.SelectedItem as string ?? "KJV";
                _settings.LogoImagePath = logo.Text;
                _settings.MediaLibraryPath = mediaLibrary.Text;
                _settings.TranscriptionEngine = transcriptionEngine.SelectedItem as string ?? "Deepgram";
                _settings.AudioInputDeviceNumber = ParseAudioInputChoice(audioInput.SelectedItem as string);
                _settings.DeepgramApiKey = deepgramKey.Text;
                _settings.DeepgramModel = String.IsNullOrWhiteSpace(deepgramModel.Text) ? "nova-3" : deepgramModel.Text;
                _settings.VoiceboxServerUrl = voiceboxUrl.Text;
                _settings.VoiceboxModel = voiceboxModel.Text;
                _settings.VoiceboxLanguage = voiceboxLanguage.Text;
                int voiceboxSeconds;
                if (Int32.TryParse(voiceboxChunkSeconds.Text, out voiceboxSeconds)) _settings.VoiceboxChunkSeconds = Math.Max(2, Math.Min(20, voiceboxSeconds));
                _settings.GeminiApiKey = geminiKey.Text;
                _settings.GeminiModel = geminiModel.Text;
                _settings.WhisperExecutablePath = whisperExe.Text;
                _settings.WhisperModelPath = whisperModel.Text;
                int chunkSeconds;
                if (Int32.TryParse(whisperChunkSeconds.Text, out chunkSeconds)) _settings.WhisperChunkSeconds = Math.Max(2, Math.Min(20, chunkSeconds));
                _settings.ExternalTargetWindowTitle = externalTargetTitle.Text;
                _settings.ExternalScriptureInputHotkey = externalHotkey.Text;
                int externalDelayMs;
                if (Int32.TryParse(externalDelay.Text, out externalDelayMs)) _settings.ExternalFocusDelayMs = Math.Max(100, Math.Min(2000, externalDelayMs));
                _settings.ExternalSendEnterAfterReference = externalSendEnter.IsChecked == true;
                
                _settings.LoadLastScheduleOnStartup = _loadScheduleStartup.IsChecked == true;
                _settings.OpenProjectorOnStartup = _openProjectorStartup.IsChecked == true;
                _settings.StartListeningOnStartup = _startListeningStartup.IsChecked == true;
                if (_templateSelector.SelectedItem is SlideTemplate template)
                {
                    _settings.SelectedTemplateName = template.Name;
                    
                    // Reapply global template styling dynamically to active preview and live slides
                    if (_previewSlide != null)
                    {
                        SlideDeckService.ApplyTemplate(_previewSlide, _settings);
                        RenderMini(_previewBox, _previewSlide);
                    }
                    if (_liveSlide != null)
                    {
                        SlideDeckService.ApplyTemplate(_liveSlide, _settings);
                        RenderMini(_liveBox, _liveSlide);
                        _projection.Render(_liveSlide);
                    }
                }
                
                _store.Save(_store.SettingsPath, _settings);
                SetStatus("Settings saved.");
            }));
            stack.Children.Add(buttons);
            stack.Children.Add(Text("Shortcuts: Enter/Space Go Live, Right next schedule, Left previous schedule, B black, C clear, L logo, F freeze, Ctrl+F Bible search."));
            return new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }

        private void SearchBible()
        {
            _bibleResults.ItemsSource = _bible.Search(_bibleSearch.Text, 150);
            SetStatus("Bible search complete.");
        }

        private void LoadBibleChapters()
        {
            var book = _bibleBook.SelectedItem as string;
            if (String.IsNullOrWhiteSpace(book)) return;
            _bibleChapter.ItemsSource = _bible.GetChapters(book).Cast<object>().ToList();
            _bibleChapter.SelectedIndex = 0;
        }

        private void LoadSelectedBibleVersion()
        {
            var version = _bibleVersion == null ? null : _bibleVersion.SelectedItem as string;
            if (String.IsNullOrWhiteSpace(version))
            {
                return;
            }

            var files = _store.GetBibleFiles();
            if (!files.ContainsKey(version))
            {
                return;
            }

            _bible.Load(version, files[version]);
            _settings.DefaultBibleVersion = version;
            _store.Save(_store.SettingsPath, _settings);
            LoadBibleChapters();
            SetStatus("Bible version loaded: " + version + " (" + _bible.Verses.Count + " verses).");
        }

        private void LoadSelectedChapter()
        {
            var book = _bibleBook.SelectedItem as string;
            if (String.IsNullOrWhiteSpace(book) || _bibleChapter.SelectedItem == null) return;
            var chapter = Convert.ToInt32(_bibleChapter.SelectedItem);
            _bibleResults.ItemsSource = _bible.GetChapter(book, chapter);
            SetStatus(book + " " + chapter + " loaded.");
        }

        private void PreviewBibleSelection()
        {
            var selected = _bibleResults.SelectedItems.Cast<BibleVerse>().ToList();
            if (selected.Count == 0 && _bibleResults.SelectedItem is BibleVerse one)
            {
                selected.Add(one);
            }
            if (selected.Count == 0)
            {
                selected = _bible.Search(_bibleSearch.Text, 20);
            }
            SetPreview(_bible.CreateSlide(selected, _settings));
            SetPreviewDeck(SlideDeckService.FromBible(selected, _settings, 2, _bible.VersionName));
        }

        private void NewSong()
        {
            var song = new Song { Title = "New Song", Author = "" };
            song.Sections.Add(new SongSection { Label = "Verse 1", Lyrics = "" });
            _songs.Add(song);
            RefreshSongs();
            _songList.SelectedItem = song;
            SaveSongs();
        }

        private void LoadSelectedSong()
        {
            var song = _songList.SelectedItem as Song;
            if (song == null) return;
            _songTitle.Text = song.Title;
            _songAuthor.Text = song.Author;
            _songCopyright.Text = song.Copyright;
            _songCcli.Text = song.CcliNumber;
            _songKey.Text = song.Key;
            _songTimeSignature.Text = song.TimeSignature;
            _songTempo.Text = song.Tempo == 0 ? "" : song.Tempo.ToString();
            _songDuration.Text = song.Duration;
            _songSequence.Text = song.CustomSequence ?? "";
            _songSections.ItemsSource = null;
            _songSections.ItemsSource = song.Sections;
        }

        private void SaveSong()
        {
            var song = _songList.SelectedItem as Song;
            if (song == null) return;
            song.Title = _songTitle.Text;
            song.Author = _songAuthor.Text;
            song.Copyright = _songCopyright.Text;
            song.CcliNumber = _songCcli.Text;
            song.Key = _songKey.Text;
            song.TimeSignature = _songTimeSignature.Text;
            int tempo;
            song.Tempo = Int32.TryParse(_songTempo.Text, out tempo) ? tempo : 0;
            song.Duration = _songDuration.Text;
            song.CustomSequence = _songSequence.Text;
            SaveSongs();
            RefreshSongs();
            _songList.SelectedItem = song;
            SetStatus("Song saved.");
        }

        private void AddSection()
        {
            var song = _songList.SelectedItem as Song;
            if (song == null) return;
            song.Sections.Add(new SongSection { Label = _sectionLabel.Text, Lyrics = _sectionLyrics.Text });
            SaveSongs();
            LoadSelectedSong();
        }

        private void UpdateSection()
        {
            var section = _songSections.SelectedItem as SongSection;
            if (section == null) return;
            section.Label = _sectionLabel.Text;
            section.Lyrics = _sectionLyrics.Text;
            SaveSongs();
            LoadSelectedSong();
        }

        private void DeleteSection()
        {
            var song = _songList.SelectedItem as Song;
            var section = _songSections.SelectedItem as SongSection;
            if (song == null || section == null) return;
            song.Sections.Remove(section);
            SaveSongs();
            LoadSelectedSong();
        }

        private void LoadSelectedSection()
        {
            var section = _songSections.SelectedItem as SongSection;
            if (section == null) return;
            _sectionLabel.Text = section.Label;
            _sectionLyrics.Text = section.Lyrics;
        }

        private void PreviewSongSection()
        {
            var song = _songList.SelectedItem as Song;
            if (song == null) return;

            var section = _songSections.SelectedItem as SongSection;
            if (section != null)
            {
                SetPreviewDeck(SlideDeckService.FromSong(song, section, _settings));
            }
            else
            {
                SetPreviewDeck(SlideDeckService.FromSong(song, _settings));
            }
        }

        private void ImportMedia(string kind)
        {
            var filter = kind == "Image"
                ? "Image files|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
                : "Video files|*.mp4;*.wmv;*.avi;*.mov;*.m4v|All files|*.*";
            var dlg = FileDialog(filter);
            if (dlg.ShowDialog() != true) return;

            var library = Path.Combine(_store.Root, "Media", "Library");
            if (!String.IsNullOrWhiteSpace(_settings.MediaLibraryPath))
            {
                library = _settings.MediaLibraryPath;
            }
            Directory.CreateDirectory(library);
            var dest = Path.Combine(library, Path.GetFileName(dlg.FileName));
            if (!File.Exists(dest))
            {
                File.Copy(dlg.FileName, dest);
            }

            _media.Add(new MediaItem { Name = Path.GetFileNameWithoutExtension(dest), Path = dest, Kind = kind });
            SaveMedia();
            RefreshMedia();
            SetStatus(kind + " imported.");
        }

        private void PreviewMedia()
        {
            var item = _mediaList.SelectedItem as MediaItem;
            if (item == null) return;
            SetPreview(new Slide
            {
                Kind = item.Kind == "Video" ? SlideKind.Video : SlideKind.Image,
                Title = item.Name,
                MediaPath = item.Path,
                FitMode = _mediaFitMode == null ? "Fill" : (_mediaFitMode.SelectedItem as string ?? "Fill"),
                Body = item.Kind == "Video" ? "" : "",
                FontFamily = _settings.FontFamily,
                FontSize = _settings.FontSize,
                TextColor = _settings.TextColor
            });
        }

        private void PreviewTextSlide()
        {
            var slide = new Slide
            {
                Kind = SlideKind.Text,
                Title = _textTitle.Text,
                Body = _textBody.Text,
                BackgroundPath = _textBackgroundPath
            };

            if (_textTemplateSelector.SelectedItem is SlideTemplate template)
            {
                slide.FontFamily = template.FontFamily;
                slide.FontSize = template.FontSize;
                slide.TextColor = template.TextColor;
                slide.BackgroundColor = template.BackgroundColor;
                slide.GradientStartColor = template.GradientStartColor;
                slide.GradientEndColor = template.GradientEndColor;
                if (String.IsNullOrWhiteSpace(slide.BackgroundPath))
                {
                    slide.BackgroundPath = template.BackgroundPath;
                }
            }
            else
            {
                SlideDeckService.ApplyTemplate(slide, _settings);
            }

            SetPreview(slide);
        }

        private void AddPreviewToSchedule()
        {
            if (_previewSlide == null) return;
            _schedule.Add(new ScheduleItem { Title = _previewSlide.Title, Slide = _previewSlide, Notes = "" });
            SaveSchedule();
            RefreshSchedule();
            SetStatus("Added to schedule.");
        }

        private void NewSchedule()
        {
            _schedule.Clear();
            SaveSchedule();
            RefreshSchedule();
            SetStatus("New schedule started.");
        }

        private void PreviewScheduleItem()
        {
            var item = _scheduleList.SelectedItem as ScheduleItem;
            if (item == null) return;
            SetPreview(item.Slide);
            if (_scheduleNotes != null) _scheduleNotes.Text = item.Notes ?? "";
        }

        private void LoadScheduleNotes()
        {
            var item = _scheduleList.SelectedItem as ScheduleItem;
            if (_scheduleNotes == null) return;
            _scheduleNotes.Text = item == null ? "" : (item.Notes ?? "");
        }

        private void SaveScheduleNotes()
        {
            var item = _scheduleList.SelectedItem as ScheduleItem;
            if (item == null || _scheduleNotes == null) return;
            item.Notes = _scheduleNotes.Text;
            SaveSchedule();
            SetStatus("Schedule notes saved.");
        }

        private void RunAiAssist()
        {
            var heard = _aiHeard.Text ?? "";
            var slides = AnalyzeHeardPhrase(heard);
            SetStatus(slides.Count == 0 ? "AI Assist found no local match." : "AI Assist found " + slides.Count + " local suggestion(s).");
        }

        private List<Slide> AnalyzeHeardPhrase(string heard)
        {
            var assist = new AiAssistService(_bible, () => _songs, _settings);
            var slides = assist.Analyze(heard, 10);
            _aiResults.ItemsSource = slides;
            return slides;
        }

        private static List<object> GetAudioInputChoices()
        {
            var choices = new List<object> { "Default microphone" };
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                try
                {
                    var caps = WaveInEvent.GetCapabilities(i);
                    choices.Add(i + ": " + caps.ProductName);
                }
                catch
                {
                }
            }

            return choices;
        }

        private string GetSelectedAudioInputChoice()
        {
            if (_settings.AudioInputDeviceNumber < 0)
            {
                return "Default microphone";
            }

            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                if (i == _settings.AudioInputDeviceNumber)
                {
                    try
                    {
                        return i + ": " + WaveInEvent.GetCapabilities(i).ProductName;
                    }
                    catch
                    {
                        return "Default microphone";
                    }
                }
            }

            return "Default microphone";
        }

        private static int ParseAudioInputChoice(string choice)
        {
            if (String.IsNullOrWhiteSpace(choice) || choice.StartsWith("Default", StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            var colon = choice.IndexOf(':');
            int value;
            return colon > 0 && Int32.TryParse(choice.Substring(0, colon), out value) ? value : -1;
        }

        private void StartListening()
        {
            SaveListeningOptions();
            if (IsVoiceboxSelected())
            {
                _listener.Stop();
                if (_deepgramListener != null) _deepgramListener.Stop();
                if (_geminiListener != null) _geminiListener.Stop();
                if (_whisperListener != null) _whisperListener.Stop();
                ConfigureVoiceboxListener();
                _voiceboxListener.Start();
                if (_listeningStatus != null)
                {
                    _listeningStatus.Text = "Listening: on (Voicebox Local)";
                }

                SetStatus("Voicebox local transcription started.");
                return;
            }

            if (IsDeepgramSelected())
            {
                _listener.Stop();
                if (_voiceboxListener != null) _voiceboxListener.Stop();
                if (_geminiListener != null) _geminiListener.Stop();
                if (_whisperListener != null) _whisperListener.Stop();
                ConfigureDeepgramListener();
                _deepgramListener.Start();
                if (_listeningStatus != null)
                {
                    _listeningStatus.Text = "Listening: connecting (Deepgram)";
                }

                SetStatus("Deepgram live transcription is connecting.");
                return;
            }

            if (IsGeminiLiveSelected())
            {
                _listener.Stop();
                if (_voiceboxListener != null) _voiceboxListener.Stop();
                if (_deepgramListener != null) _deepgramListener.Stop();
                if (_whisperListener != null) _whisperListener.Stop();
                ConfigureGeminiLiveListener();
                _geminiListener.Start();
                if (_listeningStatus != null)
                {
                    _listeningStatus.Text = "Listening: connecting (Gemini Live)";
                }

                SetStatus("Gemini Live listening is connecting.");
                return;
            }

            if (IsLocalWhisperSelected())
            {
                _listener.Stop();
                if (_voiceboxListener != null) _voiceboxListener.Stop();
                if (_deepgramListener != null) _deepgramListener.Stop();
                if (_geminiListener != null) _geminiListener.Stop();
                ConfigureLocalWhisperListener();
                _whisperListener.Start();
                if (_listeningStatus != null)
                {
                    _listeningStatus.Text = _whisperListener.IsRunning ? "Listening: on (Local Whisper)" : "Listening: unavailable";
                }

                SetStatus(_whisperListener.IsRunning ? "Local Whisper listening started." : "Local Whisper could not start. Check Settings paths.");
                return;
            }

            if (_whisperListener != null) _whisperListener.Stop();
            if (_voiceboxListener != null) _voiceboxListener.Stop();
            if (_deepgramListener != null) _deepgramListener.Stop();
            if (_geminiListener != null) _geminiListener.Stop();
            _listener.Start();
            if (_listeningStatus != null)
            {
                _listeningStatus.Text = _listener.IsRunning ? "Listening: on (Windows Speech)" : "Listening: unavailable";
            }

            if (_listener.IsRunning)
            {
                OnConnectionStatusChanged(this, "Connected");
                if (_volumeMeter != null) _volumeMeter.Value = 0;
            }
            else
            {
                OnConnectionStatusChanged(this, "Disconnected");
            }

            SetStatus(_listener.IsRunning ? "Live listening started." : "Live listening could not start on this machine.");
        }

        private void StopListening()
        {
            _listener.Stop();
            if (_deepgramListener != null) _deepgramListener.Stop();
            if (_geminiListener != null) _geminiListener.Stop();
            if (_whisperListener != null) _whisperListener.Stop();
            if (_voiceboxListener != null) _voiceboxListener.Stop();
            if (_listeningStatus != null)
            {
                _listeningStatus.Text = "Listening: off";
            }

            OnConnectionStatusChanged(this, "Disconnected");
            if (_volumeMeter != null) _volumeMeter.Value = 0;

            SetStatus("Live listening stopped.");
        }

        private bool IsLocalWhisperSelected()
        {
            return String.Equals(_settings.TranscriptionEngine, "LocalWhisper", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsGeminiLiveSelected()
        {
            return String.Equals(_settings.TranscriptionEngine, "GeminiLive", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsDeepgramSelected()
        {
            return String.Equals(_settings.TranscriptionEngine, "Deepgram", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsVoiceboxSelected()
        {
            return String.Equals(_settings.TranscriptionEngine, "VoiceboxLocal", StringComparison.OrdinalIgnoreCase);
        }

        private string GetTranscriptionEngineLabel()
        {
            if (IsVoiceboxSelected()) return "Voicebox Local";
            if (IsDeepgramSelected()) return "Deepgram";
            if (IsGeminiLiveSelected()) return "Gemini Live";
            return IsLocalWhisperSelected() ? "Local Whisper" : "Windows Speech";
        }

        private void ConfigureDeepgramListener()
        {
            if (_deepgramListener != null)
            {
                _deepgramListener.Dispose();
            }

            _deepgramListener = new DeepgramLiveListeningService(_settings);
            _deepgramListener.PhraseRecognized += OnPhraseRecognized;
            _deepgramListener.ListenFailed += (s, ex) => Dispatcher.Invoke(() =>
            {
                if (_listeningStatus != null) _listeningStatus.Text = "Listening: unavailable";
                SetStatus("Deepgram failed: " + ExceptionMessage(ex));
            });
            _deepgramListener.ConnectionStatusChanged += OnConnectionStatusChanged;
            _deepgramListener.AudioLevelAvailable += OnAudioLevelAvailable;
        }

        private void ConfigureVoiceboxListener()
        {
            if (_voiceboxListener != null)
            {
                _voiceboxListener.Dispose();
            }

            _voiceboxListener = new VoiceboxListeningService(_settings);
            _voiceboxListener.PhraseRecognized += OnPhraseRecognized;
            _voiceboxListener.ListenFailed += (s, ex) => Dispatcher.Invoke(() =>
            {
                if (_listeningStatus != null) _listeningStatus.Text = "Listening: unavailable";
                SetStatus("Voicebox failed: " + ExceptionMessage(ex));
            });
        }

        private void ConfigureGeminiLiveListener()
        {
            if (_geminiListener != null)
            {
                _geminiListener.Dispose();
            }

            _geminiListener = new GeminiLiveListeningService(_settings);
            _geminiListener.PhraseRecognized += OnPhraseRecognized;
            _geminiListener.ListenFailed += (s, ex) => Dispatcher.Invoke(() =>
            {
                if (_listeningStatus != null) _listeningStatus.Text = "Listening: unavailable";
                SetStatus("Gemini Live failed: " + ExceptionMessage(ex));
            });
            _geminiListener.ConnectionStatusChanged += OnConnectionStatusChanged;
            _geminiListener.AudioLevelAvailable += OnAudioLevelAvailable;
        }

        private void ConfigureLocalWhisperListener()
        {
            if (_whisperListener != null)
            {
                _whisperListener.Dispose();
            }

            _whisperListener = new LocalWhisperListeningService(_settings);
            _whisperListener.PhraseRecognized += OnPhraseRecognized;
            _whisperListener.ListenFailed += (s, ex) => Dispatcher.Invoke(() =>
            {
                if (_listeningStatus != null) _listeningStatus.Text = "Listening: unavailable";
                SetStatus("Local Whisper failed: " + ExceptionMessage(ex));
            });
            _whisperListener.ConnectionStatusChanged += OnConnectionStatusChanged;
            _whisperListener.AudioLevelAvailable += OnAudioLevelAvailable;
        }

        private static string ExceptionMessage(Exception ex)
        {
            if (ex == null)
            {
                return "Unknown error.";
            }

            var message = ex.Message;
            var inner = ex.InnerException;
            while (inner != null)
            {
                if (!String.IsNullOrWhiteSpace(inner.Message) && message.IndexOf(inner.Message, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    message += " / " + inner.Message;
                }

                inner = inner.InnerException;
            }

            return message;
        }

        private void OnPhraseRecognized(object sender, RecognizedPhraseEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _aiHeard.Text = e.Text;
                var slides = AnalyzeHeardPhrase(e.Text);
                AddTranscript(e.Text, e.Confidence, slides.Count);
                if (_settings.AutoPreviewLiveSuggestions && e.Confidence >= _settings.ListeningConfidenceThreshold && slides.Count > 0)
                {
                    SetPreview(slides[0]);
                }

                TrySendDetectedReferenceToExternalApp(slides, e.Confidence);

                SetStatus("Heard: " + e.Text + " (" + e.Confidence.ToString("0.00") + ")");
            });
        }

        private void TrySendDetectedReferenceToExternalApp(List<Slide> slides, float confidence)
        {
            if (!_settings.ExternalScriptureIntegrationEnabled ||
                confidence < _settings.ListeningConfidenceThreshold ||
                slides == null ||
                slides.Count == 0)
            {
                return;
            }

            var scripture = slides.FirstOrDefault(s => s != null && s.Kind == SlideKind.Bible && !String.IsNullOrWhiteSpace(s.Title));
            if (scripture == null)
            {
                return;
            }

            var reference = scripture.Title.Trim();
            if (String.Equals(reference, _lastInjectedReference, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.Now - _lastInjectedAt).TotalSeconds < 10)
            {
                return;
            }

            var result = ExternalScriptureInjector.Send(reference, _settings);
            if (result.Success)
            {
                _lastInjectedReference = reference;
                _lastInjectedAt = DateTime.Now;
                SetStatus(result.Message);
            }
            else
            {
                SetStatus("External scripture send failed: " + result.Message);
            }
        }

        private void AddTranscript(string text, float confidence, int suggestionCount)
        {
            _transcriptItems.Insert(0, new TranscriptItem
            {
                Time = DateTime.Now,
                Text = text,
                Confidence = confidence,
                SuggestionCount = suggestionCount
            });

            while (_transcriptItems.Count > 100)
            {
                _transcriptItems.RemoveAt(_transcriptItems.Count - 1);
            }

            RefreshTranscript();
        }

        private void RefreshTranscript()
        {
            if (_transcriptList == null)
            {
                return;
            }

            _transcriptList.ItemsSource = null;
            _transcriptList.ItemsSource = _transcriptItems.ToList();
        }

        private void ClearTranscript()
        {
            _transcriptItems.Clear();
            RefreshTranscript();
            SetStatus("Transcript cleared.");
        }

        private void SaveListeningOptions()
        {
            if (_autoPreviewSuggestions != null)
            {
                _settings.AutoPreviewLiveSuggestions = _autoPreviewSuggestions.IsChecked == true;
            }

            if (_externalScriptureIntegration != null)
            {
                _settings.ExternalScriptureIntegrationEnabled = _externalScriptureIntegration.IsChecked == true;
            }

            double threshold;
            if (_confidenceThreshold != null && Double.TryParse(_confidenceThreshold.Text, out threshold))
            {
                _settings.ListeningConfidenceThreshold = Math.Max(0, Math.Min(1, threshold));
            }

            _store.Save(_store.SettingsPath, _settings);
            SetStatus("Listening options saved.");
        }

        private void PreviewAiSelection()
        {
            var slide = _aiResults.SelectedItem as Slide;
            if (slide == null && _aiResults.Items.Count > 0) slide = _aiResults.Items[0] as Slide;
            if (slide != null) SetPreview(slide);
        }

        private void MoveSchedule(int delta)
        {
            var item = _scheduleList.SelectedItem as ScheduleItem;
            if (item == null) return;
            var index = _schedule.IndexOf(item);
            var next = index + delta;
            if (next < 0 || next >= _schedule.Count) return;
            _schedule.RemoveAt(index);
            _schedule.Insert(next, item);
            SaveSchedule();
            RefreshSchedule();
            _scheduleList.SelectedIndex = next;
        }

        private void RemoveScheduleItem()
        {
            var item = _scheduleList.SelectedItem as ScheduleItem;
            if (item == null) return;
            _schedule.Remove(item);
            SaveSchedule();
            RefreshSchedule();
        }

        private void SaveSongs()
        {
            _store.Save(_store.SongsPath, _songs);
        }

        private void SaveMedia()
        {
            _store.Save(_store.MediaPath, _media);
        }

        private void SaveSchedule()
        {
            _store.Save(_store.SchedulePath, _schedule);
            SetStatus("Schedule saved.");
        }

        private void RefreshAll()
        {
            RefreshSongs();
            RefreshMedia();
            RefreshSchedule();
        }

        private void RefreshSongs()
        {
            if (_songList == null) return;
            var query = _songSearch == null ? "" : (_songSearch.Text ?? "");
            var songs = _songs.AsEnumerable();
            if (!String.IsNullOrWhiteSpace(query) && !query.Equals("Search songs", StringComparison.OrdinalIgnoreCase))
            {
                songs = songs.Where(s => (s.Title ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || (s.Author ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || s.Sections.Any(sec => (sec.Lyrics ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            _songList.ItemsSource = null;
            _songList.ItemsSource = songs.OrderBy(s => s.Title).ToList();
        }

        private void RefreshMedia()
        {
            if (_mediaList == null) return;
            _mediaList.ItemsSource = null;
            _mediaList.ItemsSource = _media.OrderBy(m => m.Name).ToList();
        }

        private void RefreshSchedule()
        {
            if (_scheduleList == null) return;
            _scheduleList.ItemsSource = null;
            _scheduleList.ItemsSource = _schedule;
        }

        private void SetPreview(Slide slide)
        {
            _previewSlide = slide;
            _previewDeck = new List<Slide> { slide };
            RenderMini(_previewBox, slide);
            SetStatus("Preview: " + slide.Title);
        }

        private void SetPreviewDeck(List<Slide> slides)
        {
            if (slides == null || slides.Count == 0)
            {
                return;
            }

            _previewDeck = slides;
            _previewSlide = slides[0];
            RenderMini(_previewBox, _previewSlide);
            SetStatus("Preview deck: " + slides.Count + " slide(s).");
        }

        private void GoLive()
        {
            if (_previewSlide == null) return;
            _liveSlide = _previewSlide;
            _liveDeck = _previewDeck == null || _previewDeck.Count == 0 ? new List<Slide> { _previewSlide } : _previewDeck.ToList();
            _liveDeckIndex = Math.Max(0, _liveDeck.IndexOf(_previewSlide));
            RenderMini(_liveBox, _liveSlide);
            if (_frozenSlide != null)
            {
                SetStatus("Live selection changed, but projector output is frozen. Press F to unfreeze.");
                return;
            }

            _projection.Render(_liveSlide);
            SetStatus("Live: " + _liveSlide.Title);
        }

        private void NextLiveSlide()
        {
            MoveLiveSlide(1);
        }

        private void PreviousLiveSlide()
        {
            MoveLiveSlide(-1);
        }

        private void MoveLiveSlide(int delta)
        {
            if (_liveDeck == null || _liveDeck.Count <= 1)
            {
                MoveScheduleSelection(delta);
                return;
            }

            _liveDeckIndex = Math.Max(0, Math.Min(_liveDeck.Count - 1, _liveDeckIndex + delta));
            _liveSlide = _liveDeck[_liveDeckIndex];
            RenderMini(_liveBox, _liveSlide);
            if (_frozenSlide == null)
            {
                _projection.Render(_liveSlide);
            }

            SetStatus("Live slide " + (_liveDeckIndex + 1) + "/" + _liveDeck.Count + ": " + _liveSlide.Title);
        }

        private void SendSpecial(Slide slide)
        {
            _previewSlide = slide;
            GoLive();
        }

        private void ShowLogo()
        {
            if (String.IsNullOrWhiteSpace(_settings.LogoImagePath) || !File.Exists(_settings.LogoImagePath))
            {
                SetStatus("Choose a logo image in Settings first.");
                return;
            }

            SendSpecial(new Slide { Kind = SlideKind.Logo, Title = "Logo", MediaPath = _settings.LogoImagePath });
        }

        private void Freeze()
        {
            if (_frozenSlide == null)
            {
                _frozenSlide = _liveSlide;
                SetStatus("Output frozen.");
            }
            else
            {
                _frozenSlide = null;
                if (_liveSlide != null) _projection.Render(_liveSlide);
                SetStatus("Output unfrozen.");
            }
        }

        private void ShowProjection(bool fullscreen)
        {
            _projection.ShowOnScreen(_settings.OutputScreenIndex, fullscreen);
            if (_liveSlide != null) _projection.Render(_liveSlide);
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.F)
            {
                _bibleSearch.Focus();
                e.Handled = true;
                return;
            }

            if (IsTypingTarget(e.OriginalSource))
            {
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                GoLive();
                e.Handled = true;
            }
            else if (e.Key == Key.B)
            {
                SendSpecial(Slide.Black());
                e.Handled = true;
            }
            else if (e.Key == Key.C)
            {
                _projection.ClearText();
                e.Handled = true;
            }
            else if (e.Key == Key.L)
            {
                ShowLogo();
                e.Handled = true;
            }
            else if (e.Key == Key.F)
            {
                Freeze();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                NextLiveSlide();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                PreviousLiveSlide();
                e.Handled = true;
            }
        }

        private static bool IsTypingTarget(object originalSource)
        {
            var element = originalSource as DependencyObject;
            while (element != null)
            {
                if (element is TextBox || element is PasswordBox || element is ComboBox)
                {
                    return true;
                }

                element = VisualTreeHelper.GetParent(element);
            }

            return false;
        }

        private void MoveScheduleSelection(int delta)
        {
            if (_scheduleList.Items.Count == 0) return;
            var next = _scheduleList.SelectedIndex < 0 ? 0 : _scheduleList.SelectedIndex + delta;
            next = Math.Max(0, Math.Min(_scheduleList.Items.Count - 1, next));
            _scheduleList.SelectedIndex = next;
            PreviewScheduleItem();
        }

        private void RenderMini(Border box, Slide slide)
        {
            if (slide == null)
            {
                box.Child = new Grid { Background = Brushes.Black };
                return;
            }

            var grid = new Grid { Background = Brushes.Black };

            if (slide.Kind == SlideKind.Black)
            {
                box.Child = grid;
                return;
            }

            // 1. Background Brush
            if (!String.IsNullOrWhiteSpace(slide.GradientStartColor) && !String.IsNullOrWhiteSpace(slide.GradientEndColor))
            {
                try
                {
                    var start = (Color)ColorConverter.ConvertFromString(slide.GradientStartColor);
                    var end = (Color)ColorConverter.ConvertFromString(slide.GradientEndColor);
                    grid.Background = new LinearGradientBrush(start, end, 45.0);
                }
                catch { }
            }
            else if (!String.IsNullOrWhiteSpace(slide.BackgroundColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(slide.BackgroundColor);
                    grid.Background = new SolidColorBrush(color);
                }
                catch { }
            }

            // Image/Video background
            if (slide.Kind == SlideKind.Video && File.Exists(slide.MediaPath))
            {
                var videoOverlay = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(40, 168, 85, 247)),
                    BorderBrush = Brush("#a855f7"),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(8),
                    Padding = new Thickness(6, 2, 6, 2),
                    CornerRadius = new CornerRadius(3),
                    Child = new TextBlock { Text = "VIDEO", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = Brushes.White }
                };
                grid.Children.Add(videoOverlay);
            }
            else if (!String.IsNullOrWhiteSpace(slide.BackgroundPath) && File.Exists(slide.BackgroundPath))
            {
                try
                {
                    grid.Background = new ImageBrush(new BitmapImage(new Uri(slide.BackgroundPath)))
                    {
                        Stretch = ToStretch(slide.FitMode)
                    };
                }
                catch { }
            }
            else if (slide.Kind == SlideKind.Image && File.Exists(slide.MediaPath))
            {
                try
                {
                    grid.Background = new ImageBrush(new BitmapImage(new Uri(slide.MediaPath)))
                    {
                        Stretch = ToStretch(slide.FitMode)
                    };
                }
                catch { }
            }

            if (slide.Kind == SlideKind.Logo && !String.IsNullOrWhiteSpace(slide.MediaPath) && File.Exists(slide.MediaPath))
            {
                try
                {
                    grid.Background = new ImageBrush(new BitmapImage(new Uri(slide.MediaPath))) { Stretch = Stretch.Uniform };
                }
                catch { }
                box.Child = grid;
                return;
            }

            // 2. Text layout
            if (!String.IsNullOrWhiteSpace(slide.Body) || !String.IsNullOrWhiteSpace(slide.Title))
            {
                var scale = 0.32; // Scale down for miniature
                var textGrid = new Grid { Margin = new Thickness(20, 15, 20, 15) };
                textGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                textGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                textGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var textColor = (Color)ColorConverter.ConvertFromString(String.IsNullOrWhiteSpace(slide.TextColor) ? "#FFFFFFFF" : slide.TextColor);
                var fontFamily = new FontFamily(String.IsNullOrWhiteSpace(slide.FontFamily) ? "Segoe UI" : slide.FontFamily);
                var baseFontSize = slide.FontSize > 0 ? slide.FontSize : 54;

                if (!String.IsNullOrWhiteSpace(slide.Title))
                {
                    var titleBlock = new TextBlock
                    {
                        Text = slide.Title,
                        Foreground = new SolidColorBrush(textColor),
                        FontFamily = fontFamily,
                        FontSize = Math.Max(10, baseFontSize * 0.45 * scale),
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.NoWrap,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    Grid.SetRow(titleBlock, 0);
                    textGrid.Children.Add(titleBlock);
                }

                if (!String.IsNullOrWhiteSpace(slide.Body))
                {
                    var bodyBlock = new TextBlock
                    {
                        Text = slide.Body,
                        Foreground = new SolidColorBrush(textColor),
                        FontFamily = fontFamily,
                        FontSize = Math.Max(11, baseFontSize * scale),
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.WordEllipsis,
                        TextAlignment = TextAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetRow(bodyBlock, 1);
                    textGrid.Children.Add(bodyBlock);
                }

                if (!String.IsNullOrWhiteSpace(slide.Footer))
                {
                    var footerBlock = new TextBlock
                    {
                        Text = slide.Footer,
                        Foreground = new SolidColorBrush(textColor),
                        FontFamily = fontFamily,
                        FontSize = Math.Max(8, baseFontSize * 0.34 * scale),
                        FontWeight = FontWeights.Normal,
                        TextAlignment = TextAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 6, 0, 0)
                    };
                    Grid.SetRow(footerBlock, 2);
                    textGrid.Children.Add(footerBlock);
                }

                grid.Children.Add(textGrid);
            }

            box.Child = grid;
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (_connectionStatusText != null)
                {
                    _connectionStatusText.Text = status;
                }

                if (_connectionDot != null)
                {
                    if (status.Equals("Connected", StringComparison.OrdinalIgnoreCase))
                    {
                        _connectionDot.Background = Brush("#10b981");
                    }
                    else if (status.Equals("Connecting", StringComparison.OrdinalIgnoreCase))
                    {
                        _connectionDot.Background = Brush("#f59e0b");
                    }
                    else
                    {
                        _connectionDot.Background = Brush("#ef4444");
                    }
                }
            });
        }

        private void OnAudioLevelAvailable(object sender, float level)
        {
            Dispatcher.Invoke(() =>
            {
                if (_volumeMeter != null)
                {
                    _volumeMeter.Value = Math.Max(0, Math.Min(100, level * 100));
                }
            });
        }

        private async void TestDeepgramConnection(string apiKey)
        {
            SetStatus("Testing Deepgram connection...");
            if (String.IsNullOrWhiteSpace(apiKey))
            {
                SetStatus("Deepgram test failed: API key is empty.");
                MessageBox.Show("Please enter a Deepgram API key.", "Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                using (var socket = new System.Net.WebSockets.ClientWebSocket())
                {
                    socket.Options.SetRequestHeader("Authorization", "Token " + apiKey);
                    var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                    var model = String.IsNullOrWhiteSpace(_settings.DeepgramModel) ? "nova-3" : _settings.DeepgramModel;
                    var url = "wss://api.deepgram.com/v1/listen?model=" + Uri.EscapeDataString(model) + "&encoding=linear16&sample_rate=16000&channels=1";
                    await socket.ConnectAsync(new Uri(url), cts.Token);
                    if (socket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        await socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "Test complete", cts.Token);
                        SetStatus("Deepgram connection test succeeded!");
                        MessageBox.Show("Deepgram connection test succeeded!", "Test Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        SetStatus("Deepgram test failed: socket state is " + socket.State);
                        MessageBox.Show("Deepgram test failed: socket state is " + socket.State, "Test Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = ExceptionMessage(ex);
                SetStatus("Deepgram test failed: " + msg);
                MessageBox.Show("Deepgram connection failed:\n\n" + msg, "Test Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private UIElement CreateSidebar(TabControl tabs)
        {
            var sidebar = new Grid { Background = Brush("#101318") };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var brand = new StackPanel { Margin = new Thickness(0, 16, 0, 24), HorizontalAlignment = HorizontalAlignment.Center };
            brand.Children.Add(new TextBlock
            {
                Text = "LIGHT",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brush("#a855f7"),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            brand.Children.Add(new TextBlock
            {
                Text = "WORSHIP",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brush("#94a3b8"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetRow(brand, 0);
            sidebar.Children.Add(brand);

            var navStack = new StackPanel { Margin = new Thickness(0) };
            var tabNames = new[] { "Bible", "Songs", "Media", "Text", "Schedule", "AI Assist", "Settings" };
            var tabIcons = new[] { "📖", "🎵", "🖼️", "📝", "📋", "⚡", "⚙️" };

            _sidebarButtons.Clear();
            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                var btnBorder = new Border
                {
                    Height = 46,
                    Margin = new Thickness(8, 2, 8, 2),
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };

                var btnGrid = new Grid();
                btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Pixel) });
                btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var stripe = new Border
                {
                    Background = Brush("#a855f7"),
                    CornerRadius = new CornerRadius(1.5),
                    Height = 24,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Collapsed
                };
                Grid.SetColumn(stripe, 0);
                btnGrid.Children.Add(stripe);

                var btnContent = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                btnContent.Children.Add(new TextBlock
                {
                    Text = tabIcons[i],
                    FontSize = 16,
                    Foreground = Brush("#94a3b8"),
                    Margin = new Thickness(0, 0, 10, 0)
                });
                btnContent.Children.Add(new TextBlock
                {
                    Text = tabNames[i],
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Foreground = Brush("#94a3b8")
                });
                Grid.SetColumn(btnContent, 1);
                btnGrid.Children.Add(btnContent);

                btnBorder.Child = btnGrid;

                btnBorder.MouseEnter += (s, e) =>
                {
                    if (tabs.SelectedIndex != index)
                    {
                        btnBorder.Background = Brush("#1f2937");
                        ((TextBlock)((StackPanel)btnContent).Children[1]).Foreground = Brushes.White;
                    }
                };
                btnBorder.MouseLeave += (s, e) =>
                {
                    if (tabs.SelectedIndex != index)
                    {
                        btnBorder.Background = Brushes.Transparent;
                        ((TextBlock)((StackPanel)btnContent).Children[1]).Foreground = Brush("#94a3b8");
                    }
                };
                btnBorder.MouseDown += (s, e) =>
                {
                    tabs.SelectedIndex = index;
                };

                navStack.Children.Add(btnBorder);
                _sidebarButtons.Add(btnBorder);
            }
            Grid.SetRow(navStack, 1);
            sidebar.Children.Add(navStack);

            tabs.SelectionChanged += (s, e) =>
            {
                if (e.Source != tabs) return;

                for (int i = 0; i < _sidebarButtons.Count; i++)
                {
                    var border = _sidebarButtons[i];
                    var itemGrid = (Grid)border.Child;
                    var itemStripe = (Border)itemGrid.Children[0];
                    var content = (StackPanel)itemGrid.Children[1];
                    var iconBlock = (TextBlock)content.Children[0];
                    var textBlock = (TextBlock)content.Children[1];

                    if (tabs.SelectedIndex == i)
                    {
                        border.Background = Brush("#1b1f28");
                        itemStripe.Visibility = Visibility.Visible;
                        iconBlock.Foreground = Brush("#a855f7");
                        textBlock.Foreground = Brushes.White;
                        textBlock.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        border.Background = Brushes.Transparent;
                        itemStripe.Visibility = Visibility.Collapsed;
                        iconBlock.Foreground = Brush("#94a3b8");
                        textBlock.Foreground = Brush("#94a3b8");
                        textBlock.FontWeight = FontWeights.Medium;
                    }
                }
            };

            // Set initial selected highlight in constructor context safely
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (tabs.SelectedIndex >= 0 && tabs.SelectedIndex < _sidebarButtons.Count)
                {
                    var border = _sidebarButtons[tabs.SelectedIndex];
                    var itemGrid = (Grid)border.Child;
                    var itemStripe = (Border)itemGrid.Children[0];
                    var content = (StackPanel)itemGrid.Children[1];
                    var iconBlock = (TextBlock)content.Children[0];
                    var textBlock = (TextBlock)content.Children[1];

                    border.Background = Brush("#1b1f28");
                    itemStripe.Visibility = Visibility.Visible;
                    iconBlock.Foreground = Brush("#a855f7");
                    textBlock.Foreground = Brushes.White;
                    textBlock.FontWeight = FontWeights.Bold;
                }
            }));

            return sidebar;
        }

        private void SetStatus(string message)
        {
            if (_status != null) _status.Text = message;
        }

        private static List<Song> SeedSongs()
        {
            return new List<Song>
            {
                new Song
                {
                    Title = "Amazing Grace",
                    Author = "John Newton",
                    Key = "G",
                    TimeSignature = "4/4",
                    Tempo = 80,
                    Sections = new List<SongSection>
                    {
                        new SongSection { Label = "Verse 1", Lyrics = "Amazing grace how sweet the sound\nThat saved a wretch like me\nI once was lost but now am found\nWas blind but now I see" },
                        new SongSection { Label = "Verse 2", Lyrics = "'Twas grace that taught my heart to fear\nAnd grace my fears relieved\nHow precious did that grace appear\nThe hour I first believed" }
                    }
                }
            };
        }

        private static string SongFooter(Song song)
        {
            var parts = new List<string>();
            if (!String.IsNullOrWhiteSpace(song.Author)) parts.Add(song.Author);
            if (!String.IsNullOrWhiteSpace(song.Copyright)) parts.Add(song.Copyright);
            if (!String.IsNullOrWhiteSpace(song.CcliNumber)) parts.Add("CCLI " + song.CcliNumber);
            return String.Join("  |  ", parts);
        }

        private static TabItem Tab(string title, UIElement content)
        {
            return new TabItem { Header = title, Content = content };
        }

        private static Grid TwoRow()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            return grid;
        }

        private static TextBlock Text(string text)
        {
            return new TextBlock { Text = text, Foreground = Brush("#d8dee9"), Margin = new Thickness(0, 8, 8, 4) };
        }

        private static TextBox Input(string text)
        {
            return new TextBox
            {
                Text = text,
                MinWidth = 220,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(8),
                Background = Brushes.White,
                Foreground = Brush("#111827"),
                BorderBrush = Brush("#93a4b8"),
                CaretBrush = Brush("#111827"),
                SelectionBrush = Brush("#93c5fd"),
                SelectionTextBrush = Brush("#111827")
            };
        }

        private static Button Button(string text, RoutedEventHandler handler)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 8, 8),
                Padding = new Thickness(12, 7, 12, 7),
                Background = Brush("#2c3440"),
                Foreground = Brushes.White,
                BorderBrush = Brush("#4b5565")
            };
            button.Click += handler;
            return button;
        }

        private static ListBox List()
        {
            return new ListBox
            {
                Margin = new Thickness(8),
                Background = Brush("#101318"),
                Foreground = Brushes.White,
                BorderBrush = Brush("#303743")
            };
        }

        private static ComboBox Select(List<object> items)
        {
            return new ComboBox
            {
                ItemsSource = items,
                MinWidth = 150,
                Margin = new Thickness(8, 0, 8, 8),
                Padding = new Thickness(6),
                Background = Brushes.White,
                Foreground = Brush("#111827"),
                BorderBrush = Brush("#93a4b8")
            };
        }

        private static Border PreviewPanel(string title)
        {
            return new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Background = Brush("#0c0f14"),
                BorderBrush = Brush("#303743"),
                BorderThickness = new Thickness(1),
                Child = new TextBlock { Text = title, Foreground = Brush("#aeb7c5"), Margin = new Thickness(14) }
            };
        }

        private static UIElement Labeled(string label, UIElement child, int row)
        {
            var panel = new DockPanel();
            panel.Children.Add(Text(label));
            panel.Children.Add(child);
            Grid.SetRow(panel, row);
            return panel;
        }

        private static OpenFileDialog FileDialog(string filter)
        {
            return new OpenFileDialog { Filter = filter, CheckFileExists = true };
        }

        private static SolidColorBrush Brush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private static Stretch ToStretch(string fitMode)
        {
            switch ((fitMode ?? "Fill").Trim().ToLowerInvariant())
            {
                case "fit":
                    return Stretch.Uniform;
                case "stretch":
                    return Stretch.Fill;
                case "center":
                    return Stretch.None;
                default:
                    return Stretch.UniformToFill;
            }
        }
    }

    public class AspectRatioPanel : Panel
    {
        protected override Size MeasureOverride(Size constraint)
        {
            double width = constraint.Width;
            double height = constraint.Height;

            if (double.IsPositiveInfinity(width) && double.IsPositiveInfinity(height))
            {
                width = 640;
                height = 360;
            }
            else if (double.IsPositiveInfinity(width))
            {
                width = height * (16.0 / 9.0);
            }
            else if (double.IsPositiveInfinity(height))
            {
                height = width * (9.0 / 16.0);
            }
            else
            {
                double targetHeight = width * (9.0 / 16.0);
                if (targetHeight <= height)
                {
                    height = targetHeight;
                }
                else
                {
                    width = height * (16.0 / 9.0);
                }
            }

            var size = new Size(width, height);
            foreach (UIElement child in Children)
            {
                child.Measure(size);
            }
            return size;
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            double width = arrangeSize.Width;
            double height = arrangeSize.Height;

            double targetHeight = width * (9.0 / 16.0);
            double x = 0;
            double y = 0;

            if (targetHeight <= height)
            {
                y = (height - targetHeight) / 2.0;
                height = targetHeight;
            }
            else
            {
                double targetWidth = height * (16.0 / 9.0);
                x = (width - targetWidth) / 2.0;
                width = targetWidth;
            }

            var rect = new Rect(x, y, width, height);
            foreach (UIElement child in Children)
            {
                child.Arrange(rect);
            }
            return arrangeSize;
        }
    }
}
