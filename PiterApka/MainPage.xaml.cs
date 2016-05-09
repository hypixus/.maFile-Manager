using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Phone.UI.Input;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.IO;
using Windows.Storage;
using Windows.Storage.Pickers;
using UnofficialSteamAuthenticator.Lib.SteamAuth;
using Newtonsoft.Json;
using System.Net;
using System.Threading.Tasks;

namespace PiterApka
{
    public sealed partial class MainPage : Page
    {
        public string loadedstring;
        public SteamGuardAccount loadedaccount;
        public SteamWeb stweb = new SteamWeb();
        public string currentCode = "";
        public string oldCode = "";

        
        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
            var t = Task.Run(() => tick());        
        }
        public async void tick()
        {
            while (true)
            {
                if (loadedaccount != null)
                {
                    IWebRequest web = new SteamWeb();
                    Callback callback = new Callback(codereturner);
                    while (true)
                    {
                        loadedaccount.GenerateSteamGuardCode(web, callback);
                        await Task.Delay(1000);
                        if (oldCode!=currentCode)
                        {
                            break;
                        }
                    }
                    this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        listBox.Items.Add("Current code: " + currentCode);
                    }).AsTask().Wait();
                    oldCode = currentCode;
                    await Task.Delay(5000);
                }
                else
                {
                    Task.Delay(1000);
                }
            }
        }

        public void codereturner(string toreturn)
        {
            currentCode = toreturn;
        }

        /// <summary>
        /// Navigates to the initial home page.
        /// </summary>
        private void HomeAppBarButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private void Page_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            MessageDialog msgbox = new MessageDialog("Pick .maFile to work with.");
            await msgbox.ShowAsync();
            listBox.Items.Add("Status: Waiting for user to pick file...");
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".maFile");
            StorageFile file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                // Application now has read/write access to the picked file
                listBox.Items.Add("Status: Loading picked file...");
                using (StreamReader sRead = new StreamReader(await file.OpenStreamForReadAsync()))
                loadedstring = await sRead.ReadToEndAsync();
                listBox.Items.Add("Status: Loading account data...");
                msgbox.Content = loadedstring;
                try
                {
                    loadedaccount = JsonConvert.DeserializeObject<SteamGuardAccount>(loadedstring);
                    listBox.Items.Add("Status: Success!");
                }
                catch (Exception)
                {
                    listBox.Items.Add("Status: Error loading account. Isn't it encrypted?");
                }
            }
            else
            {
                listBox.Items.Add("Status: Error loading file.");
            }
        }
        bool sukces = false;
        bool trgt = false;
        bool recieved = false;
        bool refreshed = false;
        public List<Confirmation> curconfirmations = new List<Confirmation>();
        int whattodo = 0;
        // 1 - waiting for action
        // 2 - accepted
        // 3 - denied
        // 4 - skipped
        private async void AcceptAllButton_Click(object sender, RoutedEventArgs e)
        {
            FcCallback fccall = new FcCallback(returnconf);
            BCallback bcall = new BCallback(returnbool);
            listBox.Items.Add("Status: Fetching confirmations...");
            loadedaccount.FetchConfirmations(stweb, fccall);
            while (true)
            {
                if (recieved == true)
                {
                    listBox.Items.Add("Status: Confirmations recieved. Ammount: " + curconfirmations.Count.ToString());
                    break;
                }
            }
            listBox.Items.Add("Status: Processing each confirmation...");
            bool doit = true;
            while (curconfirmations.Count != 0)
            {
                loadedaccount.DenyConfirmation(stweb, curconfirmations[0], bcall);
                if (doit)
                {
                    listBox.Items.Add("Confirmation accepted: " + curconfirmations[0].Description);
                    doit = false;
                }
                else
                {
                    doit = true;
                }
                recieved = false;
                loadedaccount.FetchConfirmations(stweb, fccall);
                while (true)
                {
                    if (recieved == true)
                    {
                        break;
                    }
                }
                Task.Delay(1500);
            }

            listBox.Items.Add("Status: Confirmations accepted successfully.");
        }
        
        
        private async void DeclineAllButton_Click(object sender, RoutedEventArgs e)
        {
            FcCallback fccall = new FcCallback(returnconf);
            BCallback bcall = new BCallback(returnbool);
            listBox.Items.Add("Status: Fetching confirmations...");
            loadedaccount.FetchConfirmations(stweb, fccall);
            while (true)
            {
                if (recieved == true)
                {
                    listBox.Items.Add("Status: Confirmations recieved. Ammount: " + curconfirmations.Count.ToString());
                    break;
                }
            }
            listBox.Items.Add("Status: Processing each confirmation...");
            bool doit = true;
            while (curconfirmations.Count!=0)
            {
                loadedaccount.DenyConfirmation(stweb, curconfirmations[0], bcall);
                if (doit)
                {
                    listBox.Items.Add("Confirmation denied: " + curconfirmations[0].Description);
                    doit = false;
                }
                else
                {
                    doit = true;
                }
                recieved = false;
                loadedaccount.FetchConfirmations(stweb, fccall);
                while (true)
                {
                    if (recieved == true)
                    {
                        break;
                    }
                }
                Task.Delay(1500);
            }
            
            listBox.Items.Add("Status: Confirmations denied successfully.");
        }
        #region callback voids
        public void retsuccess(Success suc)
        {
            sukces = true;
        }
        public void returnbool(bool target)
        {
            trgt = true;
        }
        public void returnconf(List<Confirmation> confirmations, WgTokenInvalidException ex)
        {
            curconfirmations = confirmations;
            recieved = true;
        }
        #endregion

        private void AcceptButton_Click(object sender, RoutedEventArgs e)
        {
            if (whattodo==1)
            {
                whattodo = 2;
            }
        }

        private void DeclineButton_Click(object sender, RoutedEventArgs e)
        {
            if (whattodo == 1)
            {
                whattodo = 3;
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (whattodo == 1)
            {
                whattodo = 4;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            listBox.Items.Add("Status: Initializing session...");
            BfCallback bfcall = new BfCallback(retsuccess);
            loadedaccount.RefreshSession(stweb, bfcall);
            while (true)
            {
                if (sukces == true)
                {
                    listBox.Items.Add("Status: Session refreshed.");
                    refreshed = true;
                    break;
                }
            }
        }

        private void ConfirmationsButton_Click(object sender, RoutedEventArgs e)
        {
            FcCallback fccall = new FcCallback(returnconf);
            BCallback bcall = new BCallback(returnbool);
            listBox.Items.Add("Status: Fetching confirmations...");
            loadedaccount.FetchConfirmations(stweb, fccall);
            while (true)
            {
                if (recieved == true)
                {
                    listBox.Items.Add("Status: Confirmations recieved. Ammount: " + curconfirmations.Count.ToString());
                    break;
                }
            }
            listBox.Items.Add("Status: Processing each confirmation...");
            int x = 1;
            foreach (Confirmation item in curconfirmations)
            {
                listBox.Items.Add("Current confirmation:\n" + item.Description);
                listBox.Items.Add("Confirmation time:\n" + item.DescriptionTime);
                listBox.Items.Add("Confirmation description:\n" + item.Description2);
                whattodo = 1;
                bool br8k = false;
                while (true)
                {
                    switch (whattodo)
                    {
                        default:
                            break;
                        case 2:
                            loadedaccount.AcceptConfirmation(stweb, item, bcall);
                            br8k = true;
                            break;
                        case 3:
                            loadedaccount.AcceptConfirmation(stweb, item, bcall);
                            br8k = true;
                            break;
                        case 4:
                            br8k = true;
                            break;
                    }
                    if (br8k)
                    {
                        break;
                    }
                    Task.Delay(1000);
                }
            }
            listBox.Items.Add("Status: Done.");
        }
    }
}
