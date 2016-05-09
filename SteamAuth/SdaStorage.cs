﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Newtonsoft.Json;
using UnofficialSteamAuthenticator.Lib.Models.Sda;

namespace UnofficialSteamAuthenticator.Lib
{
    public sealed class SdaStorage
    {
        public static async void SaveMaFile(ulong usr, StorageFolder folder)
        {
            ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

            StorageFile manifestFile;
            try
            {
                if (folder.Name != "maFiles")
                {
                    folder = await folder.CreateFolderAsync("maFiles", CreationCollisionOption.OpenIfExists);
                }
                manifestFile = await folder.CreateFileAsync("manifest.json", CreationCollisionOption.OpenIfExists);
            }
            catch (Exception)
            {
                // Didn't have permission, not a real folder

                var dialog = new MessageDialog(StringResourceLoader.GetString("Export_Failed_Message"))
                {
                    Title = StringResourceLoader.GetString("Export_Failed_Title")
                };
                dialog.Commands.Add(new UICommand(StringResourceLoader.GetString("UiCommand_Ok_Text")));
                await dialog.ShowAsync();

                return;
            }

            string manifestJson = await FileIO.ReadTextAsync(manifestFile);
            Manifest manifest = JsonConvert.DeserializeObject<Manifest>(manifestJson) ?? new Manifest();

            string password = await GetPassword();

            if (await CheckPassword(manifest, folder, password))
            {
                string salt = FileEncryptor.GetRandomSalt();
                string iV = FileEncryptor.GetInitializationVector();
                var jsonAccount = (string) localSettings.Values["steamGuard-" + usr];
                string encrypted = FileEncryptor.EncryptData(password, salt, iV, jsonAccount);
                string filename = usr + ".maFile";

                var newEntry = new ManifestEntry
                {
                    SteamId = usr,
                    Iv = iV,
                    Salt = salt,
                    Filename = filename
                };

                if (manifest.Entries == null)
                    manifest.Entries = new List<ManifestEntry>
                    {
                        newEntry
                    };

                if (manifest.Entries.Any(entry => entry.SteamId == usr))
                {
                    manifest.Entries = manifest.Entries?.Select(entry => entry.SteamId == usr ? newEntry : entry).ToList();
                }
                else
                {
                    manifest.Entries.Add(newEntry);
                }
                manifest.Encrypted = true;

                StorageFile userFile = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(userFile, encrypted);

                string newManifestJson = JsonConvert.SerializeObject(manifest);
                await FileIO.WriteTextAsync(manifestFile, newManifestJson);

                var dialog = new MessageDialog(StringResourceLoader.GetString("Export_Success_Message"))
                {
                    Title = StringResourceLoader.GetString("Export_Success_Title")
                };
                dialog.Commands.Add(new UICommand(StringResourceLoader.GetString("UiCommand_Ok_Text")));
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new MessageDialog(
                    password.Length >= 3 ?
                        StringResourceLoader.GetString("Encryption_BadPassword_Message") :
                        StringResourceLoader.GetString("Encryption_ShortPassword_Message")
                )
                {
                    Title = StringResourceLoader.GetString("Encryption_BadPassword_Title")
                };
                dialog.Commands.Add(new UICommand(StringResourceLoader.GetString("UiCommand_Ok_Text")));
                await dialog.ShowAsync();
            }
        }

        private static async Task<bool> CheckPassword(Manifest manifest, IStorageFolder folder, string password)
        {
            if (password.Length < 3)
                return false;
            if (manifest.Entries == null)
                return true;

            foreach (ManifestEntry entry in manifest.Entries)
            {
                StorageFile entryFile = await folder.CreateFileAsync(entry.Filename, CreationCollisionOption.OpenIfExists);
                string encrypted = await FileIO.ReadTextAsync(entryFile);

                if (FileEncryptor.DecryptData(password, entry.Salt, entry.Iv, encrypted) == null)
                    return false;
            }

            return true;
        }

        private static async Task<string> GetPassword()
        {
            var tb = new TextBox();
            var dialog = new ContentDialog
            {
                Title = StringResourceLoader.GetString("Encryption_Promt_Title")
            };

            var panel = new StackPanel();

            panel.Children.Add(new TextBlock
            {
                Text = StringResourceLoader.GetString("Encryption_Promt_Message"),
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(tb);

            dialog.Content = panel;
            dialog.PrimaryButtonText = StringResourceLoader.GetString("UiCommand_Ok_Text");
            dialog.SecondaryButtonText = StringResourceLoader.GetString("UiCommand_Cancel_Text");
            ContentDialogResult result = await dialog.ShowAsync();

            return result == ContentDialogResult.Primary ? tb.Text : string.Empty;
        }
    }
}
