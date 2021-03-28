﻿/*
 *  Copyright (C) 2018-2020 iSnackyCracky
 *
 *  This file is part of KeePassRDP.
 *
 *  KeePassRDP is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  KeePassRDP is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with KeePassRDP.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using CredentialManagement;
using KeePass.Ecas;
using KeePass.Plugins;
using KeePass.UI;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace KeePassRDP
{
    public sealed class KeePassRDPExt : Plugin
    {
        private IPluginHost m_host = null;
        private CredentialManager _credManager = null;
        private KprConfig _config = null;

        public override string UpdateUrl { get { return "https://raw.githubusercontent.com/iSnackyCracky/KeePassRDP/master/KeePassRDP.ver"; } }

        public override bool Initialize(IPluginHost host)
        {
            Debug.Assert(host != null);
            if (host == null) { return false; }
            m_host = host;

            m_host.MainWindow.AddCustomToolBarButton(Util.ToolbarConnectBtnId, "💻", "Connect to entry via RDP using credentials.");
            m_host.TriggerSystem.RaisingEvent += TriggerSystem_RaisingEvent;

            _credManager = new CredentialManager();
            _config = new KprConfig(m_host.CustomConfig);

            return true;
        }

        private void TriggerSystem_RaisingEvent(object sender, EcasRaisingEventArgs e)
        {
            EcasPropertyDictionary dict = e.Properties;
            if (dict == null) { Debug.Assert(false); return; }

            string command = dict.Get<String>(EcasProperty.CommandID);

            if (command != null && command.Equals(Util.ToolbarConnectBtnId))
            {
                ConnectRDPtoKeePassEntry(false, true);
            }
        }

        public override void Terminate()
        {
            _credManager.RemoveAll();
            m_host.MainWindow.RemoveCustomToolBarButton(Util.ToolbarConnectBtnId);
        }

        public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
        {
            // init a "null" menu item
            ToolStripMenuItem tsmi = null;

            if (t == PluginMenuType.Entry)
            {
                // create entry menu item
                tsmi = new ToolStripMenuItem("KeePassRDP");

                // add the OpenRDP menu entry
                var tsmiOpenRDP = new ToolStripMenuItem
                {
                    ShortcutKeys = KprMenu.GetShortcut(KprMenu.MenuItem.OpenRdpConnection, _config),
                    ShowShortcutKeys = true,
                    Text = KprMenu.GetText(KprMenu.MenuItem.OpenRdpConnection)
                };
                tsmiOpenRDP.Click += OnOpenRDP_Click;
                tsmi.DropDownItems.Add(tsmiOpenRDP);

                // add the OpenRDPAdmin menu entry
                var tsmiOpenRDPAdmin = new ToolStripMenuItem
                {
                    ShortcutKeys = KprMenu.GetShortcut(KprMenu.MenuItem.OpenRdpConnectionAdmin, _config),
                    ShowShortcutKeys = true,
                    Text = KprMenu.GetText(KprMenu.MenuItem.OpenRdpConnectionAdmin)
                };
                tsmiOpenRDPAdmin.Click += OnOpenRDPAdmin_Click;
                tsmi.DropDownItems.Add(tsmiOpenRDPAdmin);

                // add the OpenRDPNoCred menu entry
                var tsmiOpenRDPNoCred = new ToolStripMenuItem
                {
                    ShortcutKeys = KprMenu.GetShortcut(KprMenu.MenuItem.OpenRdpConnectionNoCred, _config),
                    ShowShortcutKeys = true,
                    Text = KprMenu.GetText(KprMenu.MenuItem.OpenRdpConnectionNoCred)
                };
                tsmiOpenRDPNoCred.Click += OnOpenRDPNoCred_Click;
                tsmi.DropDownItems.Add(tsmiOpenRDPNoCred);

                // add the OpenRDPNoCredAdmin menu entry
                var tsmiOpenRDPNoCredAdmin = new ToolStripMenuItem
                {
                    ShortcutKeys = KprMenu.GetShortcut(KprMenu.MenuItem.OpenRdpConnectionNoCredAdmin, _config),
                    ShowShortcutKeys = true,
                    Text = KprMenu.GetText(KprMenu.MenuItem.OpenRdpConnectionNoCredAdmin)
                };
                tsmiOpenRDPNoCredAdmin.Click += OnOpenRDPNoCredAdmin_Click;
                tsmi.DropDownItems.Add(tsmiOpenRDPNoCredAdmin);

                // add the IgnoreCredEntry menu entry
                var tsmiIgnoreCredEntry = new ToolStripMenuItem
                {
                    ShortcutKeys = KprMenu.GetShortcut(KprMenu.MenuItem.IgnoreCredentials, _config),
                    ShowShortcutKeys = true,
                    Text = KprMenu.GetText(KprMenu.MenuItem.IgnoreCredentials)
                };
                tsmiIgnoreCredEntry.Click += OnIgnoreCredEntry_Click;
                tsmi.DropDownItems.Add(tsmiIgnoreCredEntry);

                // disable the entry menu when no database is opened
                tsmi.DropDownOpening += delegate (object sender, EventArgs e)
                {
                    if (IsValid(m_host, false)) { tsmiIgnoreCredEntry.Checked = Util.IsEntryIgnored(m_host.MainWindow.GetSelectedEntry(true, true)); }
                };
            }
            else if (t == PluginMenuType.Main)
            {
                // create the main menu options item
                tsmi = new ToolStripMenuItem(KprMenu.GetText(KprMenu.MenuItem.Options));
                tsmi.Click += OnKPROptions_Click;
            }

            return tsmi;
        }

        private void OnKPROptions_Click(object sender, EventArgs e) { UIUtil.ShowDialogAndDestroy(new KPROptionsForm(_config)); }

        private void OnOpenRDP_Click(object sender, EventArgs e) { ConnectRDPtoKeePassEntry(false, true); }

        private void OnOpenRDPAdmin_Click(object sender, EventArgs e) { ConnectRDPtoKeePassEntry(true, true); }

        private void OnOpenRDPNoCred_Click(object sender, EventArgs e) { ConnectRDPtoKeePassEntry(); }

        private void OnOpenRDPNoCredAdmin_Click(object sender, EventArgs e) { ConnectRDPtoKeePassEntry(true); }

        private void OnIgnoreCredEntry_Click(object sender, EventArgs e)
        {
            if (IsValid(m_host))
            {
                Util.ToggleEntryIgnored(m_host.MainWindow.GetSelectedEntry(true, true));
                var f = (MethodInvoker)delegate { m_host.MainWindow.UpdateUI(false, null, false, null, true, null, true); };
                if (m_host.MainWindow.InvokeRequired) { m_host.MainWindow.Invoke(f); }
                else { f.Invoke(); }
            }
        }

        private PwObjectList<PwEntry> GetRdpAccountEntries(PwGroup pwg)
        {
            // create PwObjectList and fill it with matching entries
            var rdpAccountEntries = new PwObjectList<PwEntry>();
            foreach (PwEntry pe in pwg.Entries)
            {
                string title = pe.Strings.ReadSafe(PwDefs.TitleField);
                bool ignore = Util.IsEntryIgnored(pe);

                string re = ".*(" + _config.CredPickerRegExPre + ").*(" + _config.CredPickerRegExPost + ").*";

                if (!ignore && Regex.IsMatch(title, re, RegexOptions.IgnoreCase)) { rdpAccountEntries.Add(pe); }
            }
            return rdpAccountEntries;
        }

        private PwEntry SelectCred(PwEntry pe)
        {
            PwEntry entry = null;

            var entrySettingsString = pe.Strings.ReadSafe(Util.KprEntrySettingsField);
            var entrySettings = JsonConvert.DeserializeObject<KprEntrySettings>(entrySettingsString);

            var cpGroups = new PwObjectList<PwGroup>();
            var cpGroupUUIDs = new List<byte[]>();


            if (Util.InRdpSubgroup(pe)) { cpGroupUUIDs.Add(pe.ParentGroup.ParentGroup.Uuid.UuidBytes); }
            if (entrySettings != null && entrySettings.CpGroupUUIDs.Count >= 1)
            {
                foreach (string uuidString in entrySettings.CpGroupUUIDs)
                {
                    byte[] uuidBytes = MemUtil.HexStringToByteArray(uuidString);
                    if (uuidBytes != null && !cpGroupUUIDs.Contains(uuidBytes)) { cpGroupUUIDs.Add(uuidBytes); }
                }
            }

            if (cpGroupUUIDs.Count >= 1)
            {
                foreach (byte[] groupUUID in cpGroupUUIDs)
                {
                    var group = m_host.Database.RootGroup.FindGroup(new PwUuid(groupUUID), true);
                    if (group != null) { cpGroups.Add(group); }
                }
            }

            if (cpGroups.UCount >= 1)
            {
                var rdpAccountEntries = new PwObjectList<PwEntry>();
                foreach (PwGroup cpGroup in cpGroups)
                {
                    rdpAccountEntries.Add(GetRdpAccountEntries(cpGroup));
                }

                var frmCredPick = new CredentialPickerForm(_config, m_host.Database)
                {
                    connPE = pe,
                    rdpAccountEntries = rdpAccountEntries
                };

                var res = frmCredPick.ShowDialog();
                if (res == System.Windows.Forms.DialogResult.OK)
                {
                    entry = frmCredPick.returnPE;
                    UIUtil.DestroyForm(frmCredPick);
                }
                else { UIUtil.DestroyForm(frmCredPick); }
            }
            else { entry = pe; }

            if (entry != null) { entry = Util.GetResolvedReferencesEntry(pe, m_host.Database); }

            return entry;
        }

        private void ConnectRDPtoKeePassEntry(bool tmpMstscUseAdmin = false, bool tmpUseCreds = false)
        {
            if (IsValid(m_host))
            {
                // get selected entry for connection
                var connPwEntry = m_host.MainWindow.GetSelectedEntry(true, true);
                string URL = Util.StripUrl(Util.ResolveReferences(connPwEntry, m_host.Database, PwDefs.UrlField));

                if (string.IsNullOrEmpty(URL))
                {
                    MessageBox.Show("The selected entry has no URL/Target to connect to.", "KeePassRDP");
                    return;
                }

                // get credentials for connection
                if (tmpUseCreds) { connPwEntry = SelectCred(connPwEntry); }
                if (connPwEntry == null) { return; }

                var rdpProcess = new Process();

                // if selected, save credentials into the Windows Credential Manager
                if (tmpUseCreds)
                {
                    // First instantiate a new KprCredential object.
                    var cred = new KprCredential(
                        connPwEntry.Strings.ReadSafe(PwDefs.UserNameField),
                        connPwEntry.Strings.ReadSafe(PwDefs.PasswordField),
                        _config.CredVaultUseWindows ? "TERMSRV/" + Util.StripUrl(URL, true) : Util.StripUrl(URL, true),
                        _config.CredVaultUseWindows ? CredentialType.DomainPassword : CredentialType.Generic,
                        Convert.ToInt32(_config.CredVaultTtl)
                    );

                    // Then give the KprCredential to the CredentialManager for managing the Windows Vault.
                    _credManager.Add(cred);

                    System.Threading.Thread.Sleep(300);
                }

                // start RDP / mstsc.exe
                rdpProcess.StartInfo.FileName = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\mstsc.exe");
                rdpProcess.StartInfo.Arguments = "/v:" + URL;
                if (tmpMstscUseAdmin || _config.MstscUseAdmin) { rdpProcess.StartInfo.Arguments += " /admin"; }
                if (_config.MstscUseFullscreen) { rdpProcess.StartInfo.Arguments += " /f"; }
                if (_config.MstscUseSpan) { rdpProcess.StartInfo.Arguments += " /span"; }
                if (_config.MstscUseMultimon) { rdpProcess.StartInfo.Arguments += " /multimon"; }
                if (_config.MstscWidth > 0) { rdpProcess.StartInfo.Arguments += " /w:" + _config.MstscWidth; }
                if (_config.MstscHeight > 0) { rdpProcess.StartInfo.Arguments += " /h:" + _config.MstscHeight; }
                rdpProcess.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                rdpProcess.Start();
            }
        }

        // Check if action is a valid operation
        private bool IsValid(IPluginHost host, bool showMsg = true)
        {
            if (!host.Database.IsOpen)
            {
                if (showMsg) { MessageBox.Show("You have to open a KeePass database first", "KeePassRDP"); }
                return false;
            }
            if (host.MainWindow.GetSelectedEntry(true, true) == null)
            {
                if (showMsg) { MessageBox.Show("You have to select an entry first", "KeePassRDP"); }
                return false;
            }
            return true;
        }

    }
}
