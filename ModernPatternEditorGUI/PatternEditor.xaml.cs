﻿using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.Actions.MachineActions;
using BuzzGUI.Common.Actions.PatternActions;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Common.Templates;
using BuzzGUI.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WDE.ModernPatternEditor.Actions;
using WDE.ModernPatternEditor.Chords;
using WDE.ModernPatternEditor.MPEStructures;

namespace WDE.ModernPatternEditor
{
    public partial class PatternEditor : UserControl, INotifyPropertyChanged
    {
        internal readonly string Version = "0.9.0.4 Beta";
        ISong song;

        internal MPEPatternDatabase MPEPatternsDB;
        internal PatternClipboard clipboard = new PatternClipboard();
        internal PlayRecordManager playRecordManager;
        internal ChordsWindow chordsWindow;

        public double ScaleFactor
        {
            get { return (PatternControl.Scale - 1.0) * 10.0; }
            set
            {
                PatternControl.Scale = 1.0 + value / 10.0;
                PropertyChanged.Raise(this, "ScaleFactor");
                UpdateAll();
            }
        }

        public bool MidiEdit { get; set; }

        public int SelectedStepsDown { get; set; }
        public int SelectedStepsRight { get; set; }

        public ISong Song
        {
            get { return song; }
            set
            {
                if (song != null)
                {
                    song.PropertyChanged -= song_PropertyChanged;
                    //song.MachineAdded -= song_MachineAdded;
                    //song.MachineRemoved -= song_MachineRemoved;
                    song.Buzz.PropertyChanged -= Buzz_PropertyChanged;
                    song.Wavetable.WaveChanged -= Wavetable_WaveChanged;

                    song.SequenceAdded -= Song_SequenceAdded;
                    song.SequenceRemoved -= Song_SequenceRemoved;
                    song.SequenceChanged -= Song_SequenceChanged;

                    // foreach (var m in machines)
                    //	m.Machine = null;
                }

                ColumnRenderer.BeatVisual.InvalidateResources();
                ColumnRenderer.BeatVisualCache.Clear();

                song = value;

                if (song != null)
                {
                    song.PropertyChanged += song_PropertyChanged;
                    //song.MachineAdded += song_MachineAdded;
                    //song.MachineRemoved += song_MachineRemoved;
                    song.Buzz.PropertyChanged += Buzz_PropertyChanged;
                    song.Wavetable.WaveChanged += Wavetable_WaveChanged;

                    song.SequenceAdded += Song_SequenceAdded;
                    song.SequenceRemoved += Song_SequenceRemoved;
                    song.SequenceChanged += Song_SequenceChanged;

                    // foreach (var m in song.Machines)
                    //	song_MachineAdded(m);

                    for (int i = 0; i < song.Wavetable.Waves.Count; i++)
                        Wavetable_WaveChanged(i);
                }
            }
        }

        private void Song_SequenceChanged(int obj)
        {
            playRecordManager.RefreshPlayPosData();
        }

        private void Song_SequenceRemoved(int obj)
        {
            playRecordManager.RefreshPlayPosData();
        }

        private void Song_SequenceAdded(int obj)
        {
            playRecordManager.RefreshPlayPosData();
        }


        void Wavetable_WaveChanged(int index)
        {
            var oldvm = waves.FirstOrDefault(w => w.Index == index);
            if (oldvm != null)
            {
                if (oldvm == SelectedWave)
                    SelectedWave = null;

                waves.Remove(oldvm);
            }

            if (song.Wavetable.Waves[index] != null)
            {
                var newvm = new WaveVM() { Wavetable = song.Wavetable, Index = index };
                var inspos = waves.FindIndex(w => w.Index > index);
                if (inspos < 0) inspos = waves.Count;
                waves.Insert(inspos, newvm);
                SelectedWave = newvm;
            }
            else
            {
                if (SelectedWave == null && waves.Count > 0)
                    SelectedWave = waves[0];
            }
        }

        void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ActiveView":
                    if (Global.Buzz.ActiveView != BuzzView.PatternView)
                        ClearEditContext();
                    break;
                case "Playing":
                    {
                        if (Global.Buzz.Recording)
                        {
                            // Init previous position etc
                            this.playRecordManager.Init();
                        }
                        if (!Global.Buzz.Playing)
                        {
                            playRecordManager.Stop();
                        }
                    }
                    break;
            }
        }

        void song_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
        }

        MachineVM selectedMachine;
        public MachineVM SelectedMachine
        {
            get { return selectedMachine; }
            set
            {
                selectedMachine = value;
                PropertyChanged.Raise(this, "SelectedMachine");
                playRecordManager.Init();

                if (selectedMachine != null)
                {
                    var m = selectedMachine.Machine;
                    if (m.DLL.Info.Type == MachineType.Generator || m.IsControlMachine)
                        m.Graph.Buzz.MIDIFocusMachine = m;
                }

            }
        }

        ObservableCollection<WaveVM> waves = new ObservableCollection<WaveVM>();
        public ObservableCollection<WaveVM> Waves { get { return waves; } }

        WaveVM selectedWave;
        public WaveVM SelectedWave
        {
            get { return selectedWave; }
            set
            {
                if (value != selectedWave)
                {
                    selectedWave = value;
                    PropertyChanged.Raise(this, "SelectedWave");
                    PropertyChanged.Raise(this, "SelectedWaveIndex");
                }
            }
        }

        public int SelectedWaveIndex
        {
            get
            {
                if (SelectedWave == null)
                    return -1;
                else
                    return Waves.IndexOf(SelectedWave);
            }

            set
            {
                if (waves.Count > 0)
                {
                    int newi = Math.Min(Math.Max(value, 0), Waves.Count - 1);
                    SelectedWave = Waves[newi];
                }
                else
                {
                    SelectedWave = null;
                }
            }
        }

        bool playNotes = true;
        public bool PlayNotes
        {
            get { return playNotes; }
            set
            {
                playNotes = value;
                PropertyChanged.Raise(this, "PlayNotes");
            }
        }

        bool isHelpVisible = false;
        public bool IsHelpVisible
        {
            get { return isHelpVisible; }
            set
            {
                isHelpVisible = value;
                PropertyChanged.Raise(this, "IsHelpVisible");
                helpControl.Visibility = isHelpVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        bool isKeyboardMappingVisible = false;
        public bool IsKeyboardMappingVisible
        {
            get { return isKeyboardMappingVisible; }
            set
            {
                isKeyboardMappingVisible = value;
                PropertyChanged.Raise(this, "IsKeyboardMappingVisible");
                PropertyChanged.Raise(this, "KeyboardMappingVisibility");
            }
        }

        public Visibility KeyboardMappingVisibility { get { return IsKeyboardMappingVisible ? Visibility.Visible : Visibility.Collapsed; } }
        public IEnumerable<KeyboardMapping> KeyboardMappings { get; private set; }

        KeyboardMapping selectedKeyboardMapping;
        public KeyboardMapping SelectedKeyboardMapping
        {
            get { return selectedKeyboardMapping; }
            set
            {
                selectedKeyboardMapping = value;
                PropertyChanged.Raise(this, "SelectedKeyboardMapping");
            }
        }

        static readonly string[] rootNotes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        public IEnumerable<string> RootNotes { get { return rootNotes; } }

        int selectedRootNote = 0;
        public int SelectedRootNote
        {
            get { return selectedRootNote; }
            set
            {
                selectedRootNote = value;
                PropertyChanged.Raise(this, "SelectedRootNote");
            }
        }


        public bool isSelectPatternEditorVisible;
        public bool IsSelectPatternEditorVisible
        {
            get { return isSelectPatternEditorVisible; }
            set
            {
                isSelectPatternEditorVisible = value;
                PropertyChanged.Raise(this, "IsSelectPatternEditorVisible");
                PropertyChanged.Raise(this, "SelectPatternEditorVisibility");
            }
        }
        public Visibility SelectPatternEditorVisibility { get { return IsSelectPatternEditorVisible ? Visibility.Visible : Visibility.Collapsed; } }

        public bool isImportExportPatternVisible;
        public bool IsImportExportPatternVisible
        {
            get { return isImportExportPatternVisible; }
            set
            {
                isImportExportPatternVisible = value;
                PropertyChanged.Raise(this, "IsImportExportPatternVisible");
                PropertyChanged.Raise(this, "SelectImportExportPatternVisibility");
            }
        }
        public Visibility SelectImportExportPatternVisibility { get { return IsImportExportPatternVisible ? Visibility.Visible : Visibility.Collapsed; } }

        string statusBarItem1;
        public string StatusBarItem1
        {
            get { return statusBarItem1; }
            set
            {
                if (value != statusBarItem1)
                {
                    statusBarItem1 = value;
                    PropertyChanged.Raise(this, "StatusBarItem1");
                }

            }
        }

        string statusBarItem2;
        public string StatusBarItem2
        {
            get { return statusBarItem2; }
            set
            {
                if (value != statusBarItem2)
                {
                    statusBarItem2 = value;
                    PropertyChanged.Raise(this, "StatusBarItem2");
                }

            }
        }

        string statusBarItem3;
        public string StatusBarItem3
        {
            get { return statusBarItem3; }
            set
            {
                if (value != statusBarItem3)
                {
                    statusBarItem3 = value;
                    PropertyChanged.Raise(this, "StatusBarItem3");
                }

            }
        }


        public static PatternEditorSettings Settings = new PatternEditorSettings();
        public readonly IGUICallbacks cb;

        public EditContext EditContext;
        public ICommand CreatePatternCommand { get; private set; }
        public ICommand ClonePatternCommand { get; private set; }
        public ICommand DeletePatternCommand { get; private set; }
        DispatcherTimer timer;

        void GeneralSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "WPFIdealFontMetrics":
                    PropertyChanged.Raise(this, "TextFormattingMode");
                    break;
            }
        }

        void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ColorNote": UpdateAll(); break;
                case "CursorRowHighlight": patternControl.UpdateCursor(); break;
                case "CursorScrollMode": UpdateAll(); break;
                case "FontFamily": UpdateAll(); break;
                case "FontSize": UpdateAll(); break;
                case "FontStyle": UpdateAll(); break;
                case "FontWeight": UpdateAll(); break;
                case "FontStretch": UpdateAll(); break;
                case "FontClearType": UpdateAll(); break;
                case "TextDropShadow": UpdateAll(); break;
                case "TextFormattingMode": UpdateAll(); break;
                case "HexRowNumbers": UpdateAll(); break;
                case "ColumnLabels": patternControl.CreateHeaders(); break;
                case "ParameterKnobs": patternControl.CreateHeaders(); break;
                case "BuzzToolbars": PatternEditorUtils.BuzzToolbars(cb.GetEditorHWND(), Settings.BuzzToolbars); break;
            }
        }

        void UpdateAll()
        {
            ColumnRenderer.Font.Update();
            var m = SelectedMachine;
            SelectedMachine = null;
            SelectedMachine = m;
            patternControl.UpdateScrollMargins();
        }

        void Commands()
        {
            CreatePatternCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachine != null,
                ExecuteDelegate = x =>
                {
                    DoAction(new CreatePatternAction(SelectedMachine.Machine, SelectedMachine.Machine.GetNewPatternName(),
                        SelectedMachine.SelectedPattern != null ? SelectedMachine.SelectedPattern.Pattern.Length : 16));
                    patternControl.Focus();
                }
            };

            ClonePatternCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachine != null && SelectedMachine.SelectedPattern != null,
                ExecuteDelegate = x =>
                {
                    DoAction(new CreatePatternAction(SelectedMachine.Machine, SelectedMachine.Machine.GetNewPatternName(), 0, SelectedMachine.SelectedPattern.Pattern));
                    patternControl.Focus();
                }
            };

            DeletePatternCommand = new SimpleCommand
            {
                CanExecuteDelegate = x => SelectedMachine != null && SelectedMachine.SelectedPattern != null,
                ExecuteDelegate = x =>
                {
                    DoAction(new DeletePatternAction(SelectedMachine.SelectedPattern.Pattern));
                    patternControl.Focus();
                }
            };

            this.InputBindings.Add(new InputBinding(CreatePatternCommand, new KeyGesture(Key.Return, ModifierKeys.Control)));
            this.InputBindings.Add(new InputBinding(ClonePatternCommand, new KeyGesture(Key.Return, ModifierKeys.Control | ModifierKeys.Shift)));
            this.InputBindings.Add(new InputBinding(DeletePatternCommand, new KeyGesture(Key.Delete, ModifierKeys.Control)));
        }

        public PatternEditor(IGUICallbacks cb)
        {
            this.cb = cb;
            EditContext = new EditContext(this);
            SelectedStepsDown = 1;
            SelectedStepsRight = 0;
            Global.GeneralSettings.PropertyChanged += new PropertyChangedEventHandler(GeneralSettings_PropertyChanged);
            Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsWindow.AddSettings("Modern Pattern Editor", Settings);
            DataContext = this;
            Commands();

            ResourceDictionary rd = GetBuzzThemeResources();
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);

            InitializeComponent();

            patternControl.Editor = this;
            patternControl.Resources.MergedDictionaries.Add(this.Resources);
            playRecordManager = new PlayRecordManager(this);

            MPEPatternsDB = new MPEPatternDatabase(this);

            this.ContextMenu = new ContextMenu();
            PatternEditorUtils.BuzzToolbars(cb.GetEditorHWND(), Settings.BuzzToolbars);

            this.Loaded += (sender, e) =>
            {
                KeyboardMappings = KeyboardMappingFile.Default.Mappings;
                PropertyChanged.Raise(this, "KeyboardMappings");
                SelectedKeyboardMapping = KeyboardMappingFile.Default.DefaultMapping;
            };

            btDefThis.Click += (sender, e) =>
            {
                PatternEditorUtils.WriteRegistry<string>(selectedMachine.Machine.DLL.Name, editorMachine.Name, PatternEditorUtils.regPathBuzzMachineDefaultPE);
            };

            btDefAll.Click += (sender, e) =>
            {
                PatternEditorUtils.WriteRegistry<string>(PatternEditorUtils.regDefaultPE, editorMachine.Name, PatternEditorUtils.regPathBuzzSettings);
            };

            btExportPattern.Click += (sender, e) =>
            {
                PatternXMLImportExport.ExportPattern(MPEPatternsDB.GetMPEPattern(SelectedMachine.SelectedPattern.Pattern));
            };

            btExportPatterns.Click += (sender, e) =>
            {
                PatternXMLImportExport.ExportPatterns(MPEPatternsDB);
            };

            btImportPatterns.Click += (sender, e) =>
            {
                PatternXMLImportExport.ImportPatterns(this);
            };


            this.KeyDown += (sender, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (e.Key == Key.Add)
                    {
                        patternBox.SelectedIndex++;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Subtract)
                    {
                        if (patternBox.SelectedIndex > 0) patternBox.SelectedIndex--;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Return)
                    {
                        Global.Buzz.ActivateSequenceEditor();
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Space)
                    {
                        if (SelectedMachine != null && SelectedMachine.SelectedPattern != null)
                            SelectedMachine.SelectedPattern.Pattern.IsPlayingSolo ^= true;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Divide)
                    {
                        if (SelectedMachine != null && SelectedMachine.Machine.BaseOctave > 0)
                            SelectedMachine.Machine.BaseOctave--;

                        e.Handled = true;
                    }
                    else if (e.Key == Key.Multiply)
                    {
                        if (SelectedMachine != null && SelectedMachine.Machine.BaseOctave < 9)
                            SelectedMachine.Machine.BaseOctave++;

                        e.Handled = true;
                    }
                    else if (e.Key == Key.F1)
                    {
                        IsHelpVisible ^= true;
                        e.Handled = true;
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.Down)
                    {
                        // machineBox.SelectedIndex++;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Up)
                    {
                        //if (machineBox.SelectedIndex > 0) machineBox.SelectedIndex--;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Add)
                    {
                        SelectedMachine.UndoableTrackCount++;
                    }
                    else if (e.Key == Key.Subtract)
                    {
                        SelectedMachine.UndoableTrackCount--;
                    }
                }
                else if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    if (e.Key == Key.System)
                    {
                        switch (e.SystemKey)
                        {
                            //case Key.M: machineBox.IsDropDownOpen = true; e.Handled = true; break;
                            case Key.P: patternBox.IsDropDownOpen = true; e.Handled = true; break;
                            case Key.B: baseOctaveBox.IsDropDownOpen = true; e.Handled = true; break;
                            case Key.R: IsKeyboardMappingVisible = true; rootNoteBox.IsDropDownOpen = true; e.Handled = true; break;
                            case Key.Y: IsKeyboardMappingVisible = true; kbMappingBox.IsDropDownOpen = true; e.Handled = true; break;
                            case Key.N: PlayNotes ^= true; e.Handled = true; break;
                        }
                    }
                }

            };

            this.PreviewGotKeyboardFocus += (sender, e) =>
            {
                Global.Buzz.EditContext = EditContext;
            };

            InvalidateVisual(); // Force draw?

            //machineBox.DropDownClosed += (sender, e) => { patternControl.Focus(); };
            patternBox.DropDownClosed += (sender, e) => { patternControl.Focus(); };
            baseOctaveBox.DropDownClosed += (sender, e) => { patternControl.Focus(); };
            rootNoteBox.DropDownClosed += (sender, e) => { patternControl.Focus(); };
            kbMappingBox.DropDownClosed += (sender, e) => { patternControl.Focus(); };

            this.IsVisibleChanged += (sender, e) =>
            {
                if (IsVisible && timer == null)
                {
                    SetTimer();
                }
                else if (!IsVisible && timer != null)
                {
                    timer.Stop();
                    timer = null;
                }
            };

            btChords.Click += (sender, e) =>
            {
                if (chordsWindow == null)
                {
                    chordsWindow = new ChordsWindow(this);
                    chordsWindow.Show();
                    chordsWindow.Closed += (sender2, e2) =>
                    {
                        chordsWindow = null;
                    };
                }

            };
        }

        public void Release()
        {
            if (chordsWindow != null)
                chordsWindow.Close();
            MPEPatternsDB.Release();
            if (SelectedMachine != null)
                SelectedMachine.Machine = null;
            SelectedMachine = null;
            Song = null;
            Global.GeneralSettings.PropertyChanged -= GeneralSettings_PropertyChanged;
            Settings.PropertyChanged -= Settings_PropertyChanged;
            patternControl.Release();
        }

        void SetTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(20);
            timer.Tick += (sender, e) =>
            {
                if (Settings.FollowPlayingPattern)
                {
                    var pattern = playRecordManager.GetPlayingPattern(selectedMachine.Machine);
                    if (pattern != null && SelectedMachine.SelectedPattern.Pattern != pattern)
                    {
                        SelectPattern(pattern);
                    }
                }

                patternControl.TimerUpdate();
            };
            timer.Start();
        }


        public void Activate()
        {
            this.Focus();
        }

        public void SelectPattern(IPattern p)
        {
            SelectMachine(p.Machine);

            var sel = patternBox.Items.Cast<PatternVM>().Where(vm => vm.Pattern == p).FirstOrDefault();
            if (sel != null) patternBox.SelectedItem = sel;
        }

        public void SelectMachine(IMachine m)
        {
        }

        public void ClearEditContext()
        {
            if (Global.Buzz.EditContext == EditContext)
            {
                Global.Buzz.EditContext = null;
            }
        }

        internal void DoAction(BuzzAction a)
        {
            // ToDo: figure out why some actions hang even when locked...
            // Global.Buzz.Playing = false;
            EditContext.ManagedActionStack.Do(a);
        }

        public TextFormattingMode TextFormattingMode { get { return Global.GeneralSettings.WPFIdealFontMetrics ? TextFormattingMode.Ideal : TextFormattingMode.Display; } }

        public IMachineDLL editorMachine;
        public IMachineDLL EditorMachine
        {
            get { return editorMachine; }
            set
            {
                editorMachine = value;
                string ename = cb.GetEditorMachine();
                if (editorMachine.Name != ename)
                {
                    cb.SetPatternEditorMachine(editorMachine.Name);
                }
            }
        }

        public ObservableCollection<IMachineDLL> EditorMachines { get; set; }

        IMachine targetMachine;
        public IMachine TargetMachine
        {
            get { return targetMachine; }
            private set
            {
                if (targetMachine != null)
                {
                }

                targetMachine = value;

                if (targetMachine != null)
                {
                    string ename = cb.GetEditorMachine();

                    EditorMachine = Global.Buzz.MachineDLLs.Values.FirstOrDefault(x => x.Name == cb.GetEditorMachine());
                    EditorMachines = new ObservableCollection<IMachineDLL>();
                    foreach (var machine in Global.Buzz.MachineDLLs.Values)
                    {
                        if (machine.Info != null && (machine.Info.Flags & MachineInfoFlags.PATTERN_EDITOR) == MachineInfoFlags.PATTERN_EDITOR)
                            EditorMachines.Add(machine);
                    }
                    PropertyChanged.Raise(this, "EditorMachines");
                    PropertyChanged.Raise(this, "EditorMachine");
                }
            }
        }

        public void InitMachine()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                lock (syncLock)
                {
                    string name = cb.GetTargetMachine();
                    if (name != "")
                    {
                        Song = Global.Buzz.Song;
                    }
                }
            }));
        }

        public void TargetMachineChanged()
        {
            lock (syncLock)
            {
                string targetMachine = cb.GetTargetMachine();
                TargetMachine = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == targetMachine);

                MPEPatternsDB.Machine = TargetMachine;

                if (MPEPatternsDB.GetPatterns().Count() == 0 && TargetMachine.Patterns.Count() > 0)
                {
                    MPEPatternsDB.AddPattern(TargetMachine.Patterns[0]);
                }

                if (SelectedMachine == null)
                    SelectedMachine = new MachineVM(this) { Machine = TargetMachine };
            }
        }

        internal void TargetMachine_PatternAdded(IPattern pattern)
        {
            //lock (syncLock)
            {
                MPEPatternsDB.AddPattern(pattern);
            }
        }

        internal void TargetMachine_PatternRemoved(IPattern pattern)
        {
            //lock (syncLock)
            {
                MPEPatternsDB.RemovePattern(pattern);
            }
        }

        // From native
        public void ThemeChanged()
        {
            ResourceDictionary rd = GetBuzzThemeResources();
            if (rd != null) this.Resources.MergedDictionaries.Add(rd);
            InvalidateVisual();
            SelectedMachine.SelectedPattern.Pattern = SelectedMachine.SelectedPattern.Pattern;
        }

        // From native
        public void SetPatternEditorData(byte[] data)
        {
            List<MPEPattern> patterns = PatternEditorUtils.ProcessEditorData(this, data);
            if (patterns.Count != 0)
            {
                MPEPatternsDB.SetPatterns(patterns);
            }
        }

        // From native
        public byte[] GetPatternEditorData()
        {
            if (Settings.PXPDataFormat)
                return PatternEditorUtils.CreatePatternXPPatternData(MPEPatternsDB.GetPatterns());
            else
                return PatternEditorUtils.CreateModernPatternEditorData(MPEPatternsDB.GetPatterns());

        }

        // From native
        public void CreatePattern(string name, int numrows)
        {
        }

        // From native
        public void SetEditorPattern(string name)
        {
            //lock (syncLock)
            {
                if (TargetMachine != null)
                {
                    IPattern pat = TargetMachine.Patterns.FirstOrDefault(x => x.Name == name);
                    if (pat != null)
                        SelectPattern(pat);
                }
            }
        }

        // From native
        public void AddTrack()
        {
            // This is also called when Ctrl+ is pressed. Commented out to avoid other issues.
            //Dispatcher.BeginInvoke(new Action(() =>
            //{
            //    SelectedMachine.UndoableTrackCount++;
            //}));
        }

        // From native
        public void DeleteLastTrack()
        {
            // This is also called when Ctrl- is pressed. Commented out to avoid other issues.
            //Dispatcher.BeginInvoke(new Action(() =>
            //{
            //    SelectedMachine.UndoableTrackCount--;
            //}));
        }

        // From native
        public void CreatePatternCopy(string newName, string oldName)
        {
            MPEPatternsDB.PrepareCopy(newName, oldName);
        }

        PatternPropertiesWindow patternPropertiesWindow;
        // From native
        public void ShowPatternProperties()
        {
            patternPropertiesWindow = new PatternPropertiesWindow(this);

            if (patternPropertiesWindow.ShowDialog() == true)
            {
                lock (syncLock)
                {
                    using (new ActionGroup(EditContext.ActionStack))
                    {
                        IPattern pattern = SelectedMachine.SelectedPattern.Pattern;
                        MPEPattern mpepat = MPEPatternsDB.GetMPEPattern(pattern);

                        if (pattern.Length != patternPropertiesWindow.PatternLenghtInBeats * PatternControl.BUZZ_TICKS_PER_BEAT)
                            DoAction(new MPESetPatternLengthAction(MPEPatternsDB.GetMPEPattern(pattern), pattern.Length / PatternControl.BUZZ_TICKS_PER_BEAT, patternPropertiesWindow.PatternLenghtInBeats)); //pattern.Length = ppw.PatternLenghtInBeats * Global.Buzz.TPB; // ToDo: Undoable 

                        if (mpepat.RowsPerBeat != patternPropertiesWindow.RowsPerBeat)
                        {
                            DoAction(new MPESetPatternRowsPerBeatAction(MPEPatternsDB.GetMPEPattern(pattern), SelectedMachine.SelectedPattern, patternPropertiesWindow.RowsPerBeat));
                        }

                        if (pattern.Name != patternPropertiesWindow.PatternName)
                            DoAction(new RenamePatternAction(MPEPatternsDB.GetMPEPattern(pattern), cb, patternPropertiesWindow.PatternName));

                        var parameters = patternPropertiesWindow.GetSelectedParameters();
                        foreach (var mpePattern in MPEPatternsDB.GetPatterns())
                        {
                            DoAction(new MPEAddOrRemoveColumnsAction(mpePattern, selectedMachine.patterns.FirstOrDefault(x => x.Pattern == mpePattern.Pattern), parameters));
                        }
                        // PropertyChanged.Raise(this, "SelectedMachine");
                    }
                }
            }

            patternPropertiesWindow = null;
        }

        // From native
        public void RecordControlChange(string pmacname, int group, int track, int param, int value)
        {
            IMachine mac = Global.Buzz.Song.Machines.FirstOrDefault(x => x.Name == pmacname);

            if (mac != null)
            {
                playRecordManager.RecordControlChange(mac, group, track, param, value);
            }
        }

        // From native
        public void MidiNote(int channel, int value, int velocity)
        {
            var cursor = patternControl.Pattern.CursorPosition;
            lock (syncLock)
            {
                if (Global.Buzz.Recording)
                {
                    playRecordManager.RecordPlayingMidiNote(channel, value, velocity);
                }
                else if (MidiEdit && cursor.ParameterColumn.Type == ColumnRenderer.ColumnType.Note && velocity != 0)
                {
                    int buzzNote = velocity == 0 ? BuzzNote.Off : BuzzNote.FromMIDINote(value);
                    int time = cursor.TimeInBeat + cursor.Beat * PatternControl.BUZZ_TICKS_PER_BEAT * PatternEvent.TimeBase;
                    MPEPattern mpePattern = MPEPatternsDB.GetMPEPattern(patternControl.Pattern.Pattern);
                    MPEPatternColumn mpeColumn = mpePattern.GetColumn(cursor.ParameterColumn.PatternColumn);
                    Dispatcher.Invoke(() =>
                    {
                        if (patternControl.IsKeyboardFocused)
                        {
                            DoAction(new MPESetOrClearEventsAction(patternControl.Pattern.Pattern, mpeColumn, new PatternEvent[] { new PatternEvent() { Time = time, Value = buzzNote } }, true));
                            patternControl.MoveCursorDelta(SelectedStepsRight, SelectedStepsDown);
                            playRecordManager.UpdatePlayingNotePattern(mpeColumn.Parameter, mpeColumn.ParamTrack, buzzNote, velocity);
                        }
                    });
                }
                TargetMachine.SendMIDINote(channel, value, velocity);
            }
        }

        // From native
        public void MidiControlChange(int ctrl, int channel, int value)
        {
            TargetMachine.SendMIDIControlChange(ctrl, channel, value);
        }

        // From native
        public byte[] ExportMidiEvents(string pattern)
        {
            lock (syncLock)
            {
                IPattern pat = SelectedMachine.Machine.Patterns.FirstOrDefault(x => x.Name == pattern);
                if (pat != null)
                {
                    MPEPattern mpePattern = MPEPatternsDB.GetMPEPattern(pat);
                    return PatternEditorUtils.ExportMidiEvents(mpePattern);
                }
                return new byte[0];
            }
        }

        public bool ImportMidiEvents(string pattern, byte [] data)
        {
            lock (syncLock)
            {
                IPattern pat = SelectedMachine.Machine.Patterns.FirstOrDefault(x => x.Name == pattern);
                if (pat != null)
                {
                    MPEPattern mpePattern = MPEPatternsDB.GetMPEPattern(pat);
                    return PatternEditorUtils.ImportMidiEvents(mpePattern, data);
                }
            }

            return false;
        }

        // From native
        public bool CanUndo()
        {
            if (EditContext != null)
                return EditContext.ActionStack.CanUndo;
            else
                return false;
        }

        // From native
        public bool CanRedo()
        {
            if (EditContext != null)
                return EditContext.ActionStack.CanRedo;
            else
                return false;
        }

        // From native
        public void Undo()
        {
            if (EditContext != null)
            {
                EditContext.ActionStack.Undo();
            }
        }

        // From native
        public void Redo()
        {
            if (EditContext != null)
            {
                EditContext.ActionStack.Redo();
            }
        }

        // From native
        public void DoCopy()
        {
            patternControl.CopyCommand.Execute(patternControl);
        }

        // From native
        public void DoCut()
        {
            patternControl.CutCommand.Execute(patternControl);
        }

        // From native
        public void DoPaste()
        {
            patternControl.PasteCommand.Execute(patternControl);
        }

        public object syncLock = new object();
        // Audio thread.

        public void Work(SongTime songTime)
        {
            if (/*songTime.PosInTick != 0 &&*/ songTime.PosInSubTick == 0)
            {
                if (Global.Buzz.Playing && TargetMachine != null)
                {
                    playRecordManager.Play(songTime);
                }
            }
        }

        internal ResourceDictionary GetBuzzThemeResources()
        {
            ResourceDictionary skin = new ResourceDictionary();

            try
            {
                string selectedTheme = Global.Buzz.SelectedTheme == "<default>" ? "Default" : Global.Buzz.SelectedTheme;
                string skinPath = Global.BuzzPath + "\\Themes\\" + selectedTheme + "\\ModernPatternEditor\\ModernPatternEditor.xaml";
                
                skin = (ResourceDictionary)XamlReaderEx.LoadHack(skinPath);
            }
            catch (Exception)
            {
                string skinPath = Global.BuzzPath + "\\Themes\\Default\\ModernPatternEditor\\ModernPatternEditor.xaml";
                skin.Source = new Uri(skinPath, UriKind.Absolute);
            }

            return skin;
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

    }
}
