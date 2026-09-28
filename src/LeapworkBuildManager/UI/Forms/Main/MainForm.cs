using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace LeapworkBuildManager
{
    public sealed partial class MainForm : Form
    {
        readonly TextBox build = new BuildNumberBox
        {
            Placeholder = "Type 20262257 → 2026.2.257"
        }, url = new DarkTextBox();
        ComboBox type;
        Label typeLabel;
        readonly ComboBox recent = new DarkComboBox();
        int inferredRuntime;
        bool applyingLayout, startupCentered, userPositioned;
        BuildKind selectedBuildType = BuildKind.Experimental;
        string SelectedBuildType
        {
            get
            {
                return BuildKinds.Display(selectedBuildType);
            }

            set
            {
                selectedBuildType = BuildKinds.Parse(value);
                if (type != null)
                    type.SelectedItem = value;
            }
        }

        readonly Label downloadIdentity = new Label();
        readonly Label destinationLabel = new Label();
        readonly Button clearHistory = new SoftButton();
        readonly Label validation = new Label(), status = new Label(), percentage = new Label(), eta = new Label();
        public Func<bool> ConfirmCloseDownload { get; set; }

        readonly Button check = new SoftButton(), copy = new SoftButton(), open = new SoftButton(), download = new SoftButton(), cancel = new SoftButton(), reset = new SoftButton(), folder = new SoftButton();
        readonly DownloadProgress bar = new DownloadProgress();
        readonly Card historyCard = new Card(), progressCard = new Card(), details = new Card(), resultsCard = new Card(), linkCard = new Card();
        readonly Panel titleBar = new Panel();
        readonly Label titleCaption = new Label();
        readonly Button minimize = new SoftButton { SymbolOnly = true, Padding = Padding.Empty }, close = new SoftButton { SymbolOnly = true, Padding = Padding.Empty };
        bool downloading
        {
            get
            {
                return appState == ApplicationState.Downloading;
            }
        }

        readonly BuildController controller;
        ApplicationState appState
        {
            get
            {
                return controller.State;
            }

            set
            {
                controller.Transition(value);
            }
        }

        readonly Label idleStatus = new Label();
        readonly System.Windows.Forms.Timer expandTimer = new System.Windows.Forms.Timer
        {
            Interval = OperationalSettings.AnimationFrameMilliseconds
        };
        bool panelExpanded;
        int panelHeight;
        readonly Stopwatch expansionClock = new Stopwatch();
        int expansionStart, expansionTarget;
        PictureBox brandLogo;
        readonly BuildService service;
        bool verified
        {
            get
            {
                return controller.IsVerified;
            }
        }

        readonly Button viewLink = new SoftButton();
        readonly Label historyHeading = new Label
        {
            Text = "Recent builds"
        };
        bool linkExpanded;
        DownloadUiModel UiModel
        {
            get
            {
                return DownloadUiModel.Create(controller, linkExpanded, advancedMode, preferences.History.Count > 0);
            }
        }

        readonly Panel viewport = new Panel
        {
            AutoScroll = true
        }, canvas = new BufferedPanel();
        readonly NotifyIcon notification = new NotifyIcon();
        readonly System.Collections.Generic.List<DiagnosticEvent> diagnostics = new System.Collections.Generic.List<DiagnosticEvent>();
        readonly Button help = new SoftButton();
        float layoutScale = 1;
        bool layoutReady;
        public Action<string> CompletionNotice { get; set; }
        public Func<Rectangle> WorkingAreaProvider { get; set; }

        readonly ToolTip detailsTip = new ToolTip
        {
            AutoPopDelay = 20000
        };
        readonly ListBox matches = new ListBox();
        readonly Label resultSummary = new Label();
        readonly Button advanced = new SoftButton();
        bool advancedMode;
        Preferences preferences;
        SettingsWriter settingsWriter;
        readonly System.Windows.Forms.Timer settingsSaveTimer = new System.Windows.Forms.Timer
        {
            Interval = OperationalSettings.SettingsDebounceMilliseconds
        };
        bool settingsClosePending, allowSettingsClose;
        bool preferencesLoaded;
        readonly string preferencesPath;
        readonly OperationCoordinator operations = new OperationCoordinator();
        CancellationTokenSource operation
        {
            get
            {
                return operations.Current;
            }
        }

        Uri current
        {
            get
            {
                return controller.CurrentUrl;
            }
        }

        bool closing, restoring;
        string completedPath
        {
            get
            {
                return controller.CompletedPath;
            }
        }

        public MainForm() : this(PreferenceStore.DefaultPath)
        {
        }

        public MainForm(string settingsPath) : this(settingsPath, new BuildService())
        {
        }
    }
}
