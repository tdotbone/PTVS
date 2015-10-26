/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    partial class PythonDebugPropertyPageControl : UserControl {
        private readonly PythonDebugPropertyPage _propPage;
        private readonly Dictionary<object, bool> _dirtyPages = new Dictionary<object, bool>();
        private readonly ToolTip _debuggerToolTip = new ToolTip();
        private bool _launcherSelectionDirty;
        private int _dirtyCount;
        private Control _curLauncher;

        public PythonDebugPropertyPageControl() {
            InitializeComponent();
        }

        internal PythonDebugPropertyPageControl(PythonDebugPropertyPage newPythonGeneralPropertyPage)
            : this() {
            _propPage = newPythonGeneralPropertyPage;
        }

        internal void LoadSettings() {
            var compModel = _propPage.Project.Site.GetComponentModel();
            var launchProvider = _propPage.Project.GetProjectProperty(PythonConstants.LaunchProvider, false);
            if (String.IsNullOrWhiteSpace(launchProvider)) {
                launchProvider = DefaultLauncherProvider.DefaultLauncherName;
            }

            _launchModeCombo.SelectedIndexChanged -= LaunchModeComboSelectedIndexChanged;

            LauncherInfo currentInfo = null;
            var projectNode = (PythonProjectNode)_propPage.Project;
            foreach (var info in compModel.GetExtensions<IPythonLauncherProvider>()
                .Select(i => new LauncherInfo(projectNode, i))
                .OrderBy(i => i.SortKey)) {

                info.LauncherOptions.DirtyChanged += LauncherOptionsDirtyChanged;
                _launchModeCombo.Items.Add(info);
                if (info.Launcher.Name == launchProvider) {
                    currentInfo = info;
                }
            }

            if (currentInfo != null) {
                _launchModeCombo.SelectedItem = currentInfo;
                SwitchLauncher(currentInfo);
            } else {
                _launchModeCombo.SelectedIndex = -1;
                SwitchLauncher(null);
            }

            _launchModeCombo.SelectedIndexChanged += LaunchModeComboSelectedIndexChanged;
        }

        public string CurrentLauncher {
            get {
                return ((LauncherInfo)_launchModeCombo.SelectedItem).Launcher.Name;
            }
        }

        private void LauncherOptionsDirtyChanged(object sender, DirtyChangedEventArgs e) {
            bool wasDirty;
            if (!_dirtyPages.TryGetValue(sender, out wasDirty)) {
                _dirtyPages[sender] = e.IsDirty;

                if (e.IsDirty) {
                    _dirtyCount++;
                }
            } else if (wasDirty != e.IsDirty) {
                if (e.IsDirty) {
                    _dirtyCount++;
                } else {
                    _dirtyCount--;
                    _dirtyPages.Remove(sender);
                }
            }

            _propPage.IsDirty = _dirtyCount != 0 || _launcherSelectionDirty;
        }

        public void SaveSettings() {
            var launcher = (LauncherInfo)_launchModeCombo.SelectedItem;
            launcher.LauncherOptions.SaveSettings();
        }

        public void ReloadSetting(string settingName) {
            var launcher = (LauncherInfo)_launchModeCombo.SelectedItem;
            launcher.LauncherOptions.ReloadSetting(settingName);
        }

        class LauncherInfo {
            public readonly Control OptionsControl;
            public readonly IPythonLauncherProvider Launcher;
            public readonly IPythonLauncherOptions LauncherOptions;

            public LauncherInfo(PythonProjectNode project, IPythonLauncherProvider launcher) {
                Launcher = launcher;
                LauncherOptions = launcher.GetLauncherOptions(project);
                OptionsControl = LauncherOptions.Control;
                LauncherOptions.LoadSettings();
            }

            public string DisplayName {
                get {
                    var launcher2 = Launcher as IPythonLauncherProvider2;
                    if (launcher2 != null) {
                        return launcher2.LocalizedName;
                    } else {
                        return Launcher.Name;
                    }
                }
            }

            public string SortKey {
                get {
                    var launcher2 = Launcher as IPythonLauncherProvider2;
                    if (launcher2 != null) {
                        return string.Format("{0:D011};{1}", launcher2.SortPriority, launcher2.LocalizedName);
                    } else {
                        return string.Format("{0:D011};{1}", Int32.MaxValue, Launcher.Name);
                    }
                }
            }
        }

        private void SwitchLauncher(LauncherInfo info) {
            if (_curLauncher != null) {
                tableLayout.Controls.Remove(_curLauncher);
            }

            if (info == null) {
                _curLauncher = null;
                _debuggerToolTip.SetToolTip(_launchModeCombo, null);
                return;
            }

            var newLauncher = info.OptionsControl;
            info.LauncherOptions.LoadSettings();
            tableLayout.Controls.Add(newLauncher);
            tableLayout.SetCellPosition(newLauncher, new TableLayoutPanelCellPosition(0, 1));
            tableLayout.SetColumnSpan(newLauncher, 2);
            newLauncher.Dock = DockStyle.Fill;
            _curLauncher = newLauncher;
            _debuggerToolTip.SetToolTip(_launchModeCombo, info.Launcher.Description);
        }

        private void LaunchModeComboSelectedIndexChanged(object sender, EventArgs e) {
            _launcherSelectionDirty = true;
            _propPage.IsDirty = true;
            SwitchLauncher((LauncherInfo)_launchModeCombo.SelectedItem);
        }

        private void _launchModeCombo_Format(object sender, ListControlConvertEventArgs e) {
            var launcher = (LauncherInfo)e.ListItem;
            e.Value = launcher.DisplayName;
        }

    }
}
