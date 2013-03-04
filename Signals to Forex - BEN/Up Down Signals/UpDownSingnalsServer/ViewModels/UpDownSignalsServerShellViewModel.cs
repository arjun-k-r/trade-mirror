﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TraceSourceLogger;
using UpDownSingnalsServer.Commands;
using UpDownSingnalsServer.Models;
using UpDownSingnalsServer.Services;
using UpDownSingnalsServer.Utility;
using Timer = System.Timers.Timer;

namespace UpDownSingnalsServer.ViewModels
{
    public class UpDownSignalsServerShellViewModel : DependencyObject
    {
        private static readonly Type OType = typeof(UpDownSignalsServerShellViewModel);

        public ICommand SearchGoCommand { get; set; }
        public ICommand RefreshCommand { get; set; }
        public ICommand BrowseFileCommand { get; set; }
        public ICommand SendSignalsCommand { get; set; }

        private readonly DBHelper _helper = null;

        public List<User> UpDownSignalsUsersUsers { get; set; }

        private int _signalIndex = 1;

        private static Timer _refreshUsersTimer;
        private const int RefreshPeriod = 150;

        /// <summary>
        /// Holds reference to UI dispatcher
        /// </summary>
        private readonly Dispatcher _currentDispatcher;

        #region SearchTermsCollection

        public static readonly DependencyProperty SearchTermsCollectionProperty =
            DependencyProperty.Register("SearchTermsCollection", typeof(ObservableCollection<string>), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(default(ObservableCollection<string>)));

        public ObservableCollection<string> SearchTermsCollection
        {
            get { return (ObservableCollection<string>)GetValue(SearchTermsCollectionProperty); }
            set { SetValue(SearchTermsCollectionProperty, value); }
        }

        #endregion

        #region SelectedSearchType

        public static readonly DependencyProperty SelectedSearchTypeProperty =
            DependencyProperty.Register("SelectedSearchType", typeof(string), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(default(string)));

        public string SelectedSearchType
        {
            get { return (string)GetValue(SelectedSearchTypeProperty); }
            set { SetValue(SelectedSearchTypeProperty, value); }
        }

        #endregion

        #region SearchItem

        public static readonly DependencyProperty SearchItemProperty =
            DependencyProperty.Register("SearchItem", typeof(string), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(default(string)));

        public string SearchItem
        {
            get { return (string)GetValue(SearchItemProperty); }
            set { SetValue(SearchItemProperty, value); }
        }

        #endregion

        #region IsAllUsersChecked

        public static readonly DependencyProperty IsAllUsersCheckedProperty =
            DependencyProperty.Register("IsAllUsersChecked", typeof(bool), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(true));

        public bool IsAllUsersChecked
        {
            get { return (bool)GetValue(IsAllUsersCheckedProperty); }
            set { SetValue(IsAllUsersCheckedProperty, value); }
        }

        #endregion

        #region IsActiveUsersChecked

        public static readonly DependencyProperty IsActiveUsersCheckedProperty =
            DependencyProperty.Register("IsActiveUsersChecked", typeof(bool), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(default(bool)));

        public bool IsActiveUsersChecked
        {
            get { return (bool)GetValue(IsActiveUsersCheckedProperty); }
            set { SetValue(IsActiveUsersCheckedProperty, value); }
        }

        #endregion

        #region IsRevokedUsersChecked

        public static readonly DependencyProperty IsRevokedUsersCheckedProperty =
            DependencyProperty.Register("IsRevokedUsersChecked", typeof(bool), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(default(bool)));

        public bool IsRevokedUsersChecked
        {
            get { return (bool)GetValue(IsRevokedUsersCheckedProperty); }
            set { SetValue(IsRevokedUsersCheckedProperty, value); }
        }

        #endregion

        #region FilteredUsersCollection

        public static readonly DependencyProperty FilteredUsersCollectionProperty =
            DependencyProperty.Register("FilteredUsersCollection", typeof(ObservableCollection<User>), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata((new ObservableCollection<User>())));

        public ObservableCollection<User> FilteredUsersCollection
        {
            get { return (ObservableCollection<User>)GetValue(FilteredUsersCollectionProperty); }
            set { SetValue(FilteredUsersCollectionProperty, value); }
        }

        #endregion

        #region SignalsCollection

        public static readonly DependencyProperty SignalsCollectionProperty =
            DependencyProperty.Register("SignalsCollection", typeof (ObservableCollection<Signal>), typeof (UpDownSignalsServerShellViewModel), new PropertyMetadata(default(ObservableCollection<User>)));

        public ObservableCollection<Signal> SignalsCollection
        {
            get { return (ObservableCollection<Signal>) GetValue(SignalsCollectionProperty); }
            set { SetValue(SignalsCollectionProperty, value); }
        }

        #endregion

        #region SelectedSignalFilePath

        public static readonly DependencyProperty SelectedSignalFilePathProperty =
            DependencyProperty.Register("SelectedSignalFilePath", typeof (string), typeof (UpDownSignalsServerShellViewModel), new PropertyMetadata(default(string)));

        public string SelectedSignalFilePath
        {
            get { return (string) GetValue(SelectedSignalFilePathProperty); }
            set { SetValue(SelectedSignalFilePathProperty, value); }
        }

        #endregion

        #region Service

        public static readonly DependencyProperty ServiceProperty =
            DependencyProperty.Register("Service", typeof(UpDownSignalsService), typeof(UpDownSignalsServerShellViewModel), new PropertyMetadata(default(UpDownSignalsService)));

        public UpDownSignalsService Service
        {
            get { return (UpDownSignalsService)GetValue(ServiceProperty); }
            set { SetValue(ServiceProperty, value); }
        }

        #endregion
        
        /// <summary>
        /// Default Constructor
        /// </summary>
        public UpDownSignalsServerShellViewModel()
        {
            this.SearchGoCommand = new SearchGoCommand(this);
            this.RefreshCommand = new RefreshCommand(this);
            this.BrowseFileCommand = new BrowseFileCommand(this);
            this.SendSignalsCommand = new SendSignalsCommand(this);

            _refreshUsersTimer = new Timer(RefreshPeriod * 1000);
            _refreshUsersTimer.Elapsed += RefreshUsersTimerElapsed;
            _refreshUsersTimer.AutoReset = true;
            _refreshUsersTimer.Enabled = true;

            SignalsCollection = new ObservableCollection<Signal>();

            this._currentDispatcher = Dispatcher.CurrentDispatcher;

            var connectionManager = new ConnectionManager();
            _helper = new DBHelper(connectionManager);

            UpDownSignalsUsersUsers = _helper.BuildUsersList();

            Service = new UpDownSignalsService();
            //ThreadPool.QueueUserWorkItem(InitializeService, Service);
            InitializeService(Service);

            InitializeSearchTermsCollection();
            InitializeFilteredUsersCollection();
        }

        /// <summary>
        /// Initializes the Search Terms Collection
        /// </summary>
        private void InitializeSearchTermsCollection()
        {
            try
            {
                this.SearchTermsCollection = new ObservableCollection<string>();
                this._currentDispatcher.Invoke(DispatcherPriority.Normal, (Action) (() =>
                                                                                        {
                                                                                            this.SearchTermsCollection.Add("Account ID");
                                                                                            this.SearchTermsCollection.Add("Key String");
                                                                                            this.SearchTermsCollection.Add("Email Address");
                                                                                        }));

            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "InitializeSearchTermsCollection");
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        private void InitializeFilteredUsersCollection()
        {
            try
            {
                this.FilteredUsersCollection=new ObservableCollection<User>();
                UpDownSignalsUsersUsers = UpDownSignalsUsersUsers.OrderBy(x => x.ID).ToList();
                UpDownSignalsUsersUsers.Reverse();

                this._currentDispatcher.Invoke(DispatcherPriority.Normal, (Action) (() =>
                                                                                        {
                                                                                            foreach (var user in UpDownSignalsUsersUsers)
                                                                                            {
                                                                                                this.FilteredUsersCollection.Add(user);
                                                                                            }
                                                                                        }));
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "InitializeFilteredUsersCollection");
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        public void SearchUsers()
        {
            try
            {
                string searchFilter = "All";
                this.FilteredUsersCollection.Clear();

                if (IsAllUsersChecked)
                {
                    searchFilter = "All";
                }
                else if(IsActiveUsersChecked)
                {
                    searchFilter = "Active";
                }
                else if (IsRevokedUsersChecked)
                {
                    searchFilter = "Revoked";
                }

                var searchedUsers = SearchHelper.SearchUser(SelectedSearchType, SearchItem, searchFilter, UpDownSignalsUsersUsers);

                searchedUsers = searchedUsers.OrderBy(x => x.ID).ToList();
                searchedUsers.Reverse();

                foreach (var revokedUser in searchedUsers)
                {
                    this.FilteredUsersCollection.Add(revokedUser);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "SearchUsers");
            }
            
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeMirrorServiceObject"></param>
        private void InitializeService(Object tradeMirrorServiceObject)
        {
            try
            {
                var tradeMirrorService = (UpDownSignalsService)tradeMirrorServiceObject;
                tradeMirrorService.Start();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "InitializeService");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeMirrorServiceObject"></param>
        private void StopService(Object tradeMirrorServiceObject)
        {
            try
            {
                var tradeMirrorService = (UpDownSignalsService)tradeMirrorServiceObject;
                tradeMirrorService.Stop();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "StopService");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void FreeResources()
        {
            StopService(Service);
        }
        
        /// <summary>
        /// 
        /// </summary>
        private void UpdateUserList()
        {
            try
            {
                UpDownSignalsUsersUsers.Clear();
                UpDownSignalsUsersUsers = _helper.BuildUsersList();
                UpDownSignalsUsersUsers = UpDownSignalsUsersUsers.OrderBy(x => x.ID).ToList();
                UpDownSignalsUsersUsers.Reverse();

                FilteredUsersCollection.Clear();
                this._currentDispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    foreach (var user in UpDownSignalsUsersUsers)
                    {
                        this.FilteredUsersCollection.Add(user);
                    }
                }));
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "UpdateUserList");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void RefreshUI()
        {
            try
            {
                this._currentDispatcher.Invoke(DispatcherPriority.Normal, (Action)(() =>
                {
                    UpdateUserList();
                    ResetTimer();
                }));
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "RefreshUI");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void RefreshUsersTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Info("Refreshing Application Data", OType.FullName, "RefreshUsersTimerElapsed");
            RefreshUI();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ResetTimer()
        {
            _refreshUsersTimer.Stop();
            _refreshUsersTimer.Start();
        }

        public void SendSignals()
        {
            string message = String.Empty;

            foreach (var signal in SignalsCollection)
            {
                if(message == String.Empty)
                {
                    message = ConvertSignalIntoOrderFormat(signal);
                }
                else
                {
                    message = message + ";" + ConvertSignalIntoOrderFormat(signal);
                }
            }
            Service.PublishNewSignal(message);
        }

        public void BrowseFile()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            // Set filter options and filter index.
            openFileDialog1.Filter = "CSV (.csv)|*.csv|All Files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;

            openFileDialog1.Multiselect = false;

            // Call the ShowDialog method to show the dialog box.
            bool? userClickedOK = openFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == true)
            {
                SelectedSignalFilePath = openFileDialog1.FileName;

                PopulateSignalsView();
            }
        }

        private void PopulateSignalsView()
        {
            try
            {
                if (!File.Exists(SelectedSignalFilePath))
                {
                    Logger.Debug("Signals file doesnt exist at the browsed location or is Unavailable", OType.FullName, "PopulateSignalsView");
                }

                FileStream fs = new FileStream(SelectedSignalFilePath, FileMode.Open, FileAccess.Read, FileShare.None);
                StreamReader streamReader = new StreamReader(fs);

                SignalsCollection.Clear();

                string tempString = String.Empty;

                while ((tempString = streamReader.ReadLine()) != null)
                {
                    Signal newSignal = ParseSignalInformation(tempString);
                    SignalsCollection.Add(newSignal);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception, OType.FullName, "PopulateSignalsView");
            }
        }

        private Signal ParseSignalInformation(string signalInformation)
        {
            //Symbol, EntrySide, EntryPrice, Model
            string[] splittedSignal = signalInformation.Split(',');

            Signal newSignal = new Signal(_signalIndex, splittedSignal[0], splittedSignal[1], Convert.ToDecimal(splittedSignal[2]), splittedSignal[3]);
            _signalIndex++;
            Logger.Info("New Signal Added = " + newSignal, OType.FullName, "ParseSignalInformation");

            return newSignal;
        }

        private string ConvertSignalIntoOrderFormat(Signal signal)
        {
            string message = signal.Symbol + "," + signal.EntrySide + "," + signal.EntryPrice;

            return message;
        }
    }
}