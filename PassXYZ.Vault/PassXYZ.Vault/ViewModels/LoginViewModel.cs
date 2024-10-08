﻿using PassXYZ.Vault.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Essentials;

using KeePassLib;
using PassXYZLib;
using PassXYZ.Vault.Resx;

namespace PassXYZ.Vault.ViewModels
{
    /// <summary>
    /// Extend class PassXYZLib.User to support preference.
    /// </summary>
    public class LoginUser : PxUser
    {
        private BaseViewModel _baseViewModule;
        private const string PrivacyNotice = "Privacy Notice";
        public override string Username
        {
            get
            {
                if (string.IsNullOrEmpty(base.Username))
                {
                    string usrname = Preferences.Get(nameof(LoginUser), base.Username);
                    base.Username = usrname;
                    if(!IsUserExist)
                    {
                        base.Username = string.Empty;
                    }
                    return base.Username;
                }
                else
                {
                    return base.Username;
                }
            }

            set
            {
                base.Username = value;
                Preferences.Set(nameof(LoginUser), base.Username);
            }
        }

        public static bool IsPrivacyNoticeAccepted
        {
            get
            {
                return Preferences.Get(PrivacyNotice, false);
            }

            set
            {
                Preferences.Set(PrivacyNotice, value);
            }
        }

        public bool IsLogined => _baseViewModule.DataStore.IsOpen;

        public override void Logout()
        {
            if (IsLogined)
            {
                _baseViewModule.DataStore.Logout();
                string path = System.IO.Path.Combine(PxDataFile.TmpFilePath, FileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private bool _isFingerprintEnabled = false;
        public bool IsFingerprintEnabled => _isFingerprintEnabled;

        public async Task<bool> IsFingerprintEnabledAsync()
        {
            string data = await GetSecurityAsync();
            if (string.IsNullOrEmpty(data))
            {
                _isFingerprintEnabled = false;
                return false;
            }
            else 
            {
                _isFingerprintEnabled = true;
                return true;
            }
        }

        /// <summary>
        /// Get password in secure storage
        /// </summary>
        public async Task<string> GetSecurityAsync()
        {
            if (string.IsNullOrWhiteSpace(Username)) { return string.Empty; }

            string data = await SecureStorage.GetAsync(Username);
            if (string.IsNullOrEmpty(data)) 
            {
                _isFingerprintEnabled = true;
            }
            return data;
        }

        /// <summary>
        /// Store password in secure storage
        /// </summary>
        public async Task SetSecurityAsync()
        {
            string passwd = _baseViewModule.DataStore.GetMasterPassword();

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(passwd)) { return; }

            await SecureStorage.SetAsync(Username, passwd);
        }

        public async Task<bool> DisableSecurityAsync()
        {
            if (string.IsNullOrWhiteSpace(Username)) { return false; }

            try
            {
                string data = await SecureStorage.GetAsync(Username);
                if (data != null)
                {
                    return SecureStorage.Remove(Username);
                }
                else 
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Possible that device doesn't support secure storage on device.
                Debug.WriteLine($"{ex}");
                return false;
            }
        }

        public LoginUser(BaseViewModel viewModule)
        {
            _baseViewModule = viewModule;
        }
    }

    public class LoginViewModel : BaseViewModel
    {
        private string _username;
        private string _password;
        private string _password2;
        private bool _isDeviceLockEnabled;
        private Action<string> _signUpAction;
        public Command LoginCommand { get; }
        public Command SignUpCommand { get; }
        public Command CancelCommand { get; }
        public string Username
        {
            get {
                if (IsLogined) { IsDeviceLockEnabled = CurrentUser.IsDeviceLockEnabled; }
                return IsLogined? CurrentUser.Username: _username;
            }

            set
            {
                _ = SetProperty(ref _username, value);
                CurrentUser.Username = value;
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _ = SetProperty(ref _password, value);
                CurrentUser.Password = value;
            }
        }

        public string Password2
        {
            get => _password2;
            set => SetProperty(ref _password2, value);
        }

        public bool IsDeviceLockEnabled
        {
            get => IsLogined ? CurrentUser.IsDeviceLockEnabled : _isDeviceLockEnabled;
            set
            {
                _ = SetProperty(ref _isDeviceLockEnabled, value);
                CurrentUser.IsDeviceLockEnabled = value;
            }
        }

        public static LoginUser CurrentUser { get; set; }

        public bool IsLogined => DataStore.IsOpen;

        public LoginViewModel()
        {
            LoginCommand = new Command(OnLoginClicked, ValidateLogin);
            SignUpCommand = new Command(OnSignUpClicked, ValidateSignUp);
            CancelCommand = new Command(OnCancelClicked);
            this.PropertyChanged +=
                (_, __) => LoginCommand.ChangeCanExecute();

            this.PropertyChanged +=
                (_, __) => SignUpCommand.ChangeCanExecute();

            CurrentUser = new LoginUser(this);
        }

        public LoginViewModel(bool isInitialized) : this()
        {
            if (isInitialized && IsLogined)
            {
                Username = DataStore.CurrentUser.Username;
            }
        }

        public LoginViewModel(Action<string> signUpAction) : this()
        {
            _signUpAction = signUpAction;
        }

        private bool ValidateLogin()
        {
            return !string.IsNullOrWhiteSpace(_username)
                && !string.IsNullOrWhiteSpace(_password)
                && LoginUser.IsPrivacyNoticeAccepted;
        }

        private bool ValidateSignUp()
        {
            return !string.IsNullOrWhiteSpace(_username)
                && !string.IsNullOrWhiteSpace(_password)
                && !string.IsNullOrWhiteSpace(_password2)
                && _password.Equals(_password2)
                && LoginUser.IsPrivacyNoticeAccepted;
        }

        public async void OnAppearing()
        {
            IsBusy = false;

            await SynchronizeUsersAsync();
        }

        public static async Task<bool> SynchronizeUsersAsync()
        {
            IEnumerable<PxUser> pxUsers = null;

#if PASSXYZ_CLOUD_SERVICE
            if (PxCloudConfig.IsConfigured && PxCloudConfig.IsEnabled)
            {
                if (PassXYZ.Vault.App.IsSshOperationTimeout)
                {
                    // If the last connection is timeout, we load local users first.
                    pxUsers = await PxUser.LoadLocalUsersAsync();
                }
                else 
                {
                    ICloudServices<PxUser> sftp = PxCloudConfig.GetCloudServices();
                    pxUsers = await sftp.SynchronizeUsersAsync();
                }
            }
            else
#endif // PASSXYZ_CLOUD_SERVICE
            {
                pxUsers = await PxUser.LoadLocalUsersAsync();
            }

            if (pxUsers != null)
            {
                App.IsBusyToLoadUsers = true;
                App.Users.Clear();
                foreach (PxUser pxUser in pxUsers)
                {
                    App.Users.Add(pxUser);
                }
                App.IsBusyToLoadUsers = false;
                return true;
            }
            else
            {
                Debug.WriteLine("LoginViewModel: SynchronizeUsersAsync failed");
                return false;
            }
        }

        public async void OnLoginClicked()
        {
            try
            {
                IsBusy = true;

                if (string.IsNullOrWhiteSpace(CurrentUser.Password))
                {
                    await Shell.Current.DisplayAlert("", AppResources.settings_empty_password, AppResources.alert_id_ok);
                    IsBusy = false;
                    return;
                }

                bool status = await DataStore.LoginAsync(CurrentUser);

                if (status)
                {
                    if (AppShell.CurrentAppShell != null)
                    {
                        AppShell.CurrentAppShell.SetRootPageTitle(DataStore.RootGroup.Name);

                        string path = Path.Combine(PxDataFile.TmpFilePath, CurrentUser.FileName);
                        if (File.Exists(path))
                        {
                            // If there is file to merge, we merge it first.
                            bool result = await DataStore.MergeAsync(path, PwMergeMethod.KeepExisting);
                            //PwMergeMethod mm = PwMergeMethod.KeepExisting;
                            //List<string> mergeMethodList = new List<string>
                            //{
                            //    nameof(PwMergeMethod.OverwriteExisting),
                            //    nameof(PwMergeMethod.KeepExisting),
                            //    nameof(PwMergeMethod.OverwriteIfNewer),
                            //    nameof(PwMergeMethod.CreateNewUuids),
                            //    nameof(PwMergeMethod.Synchronize)
                            //};

                            //var mmValue = await Shell.Current.DisplayActionSheet(AppResources.settings_merge_method_title, AppResources.action_id_cancel, null, mergeMethodList.ToArray());
                            //if (mmValue == nameof(PwMergeMethod.OverwriteExisting)) { mm = PwMergeMethod.OverwriteExisting; }
                            //else if (mmValue == nameof(PwMergeMethod.KeepExisting)) { mm = PwMergeMethod.KeepExisting; }
                            //else if (mmValue == nameof(PwMergeMethod.OverwriteIfNewer)) { mm = PwMergeMethod.OverwriteIfNewer; }
                            //else if (mmValue == nameof(PwMergeMethod.CreateNewUuids)) { mm = PwMergeMethod.CreateNewUuids; }
                            //else if (mmValue == nameof(PwMergeMethod.Synchronize)) { mm = PwMergeMethod.Synchronize; }
                            //bool result = await DataStore.MergeAsync(path, mm);
                            //string message = "Merge failure";
                            //if (result) { message = "Merged successfully"; }
                            //await Shell.Current.DisplayAlert("", message, AppResources.alert_id_ok);
                        }

                        await Shell.Current.GoToAsync($"//{nameof(ItemsPage)}");
                    }
                    else
                    {
                        await Shell.Current.GoToAsync($"//{nameof(AboutPage)}");
                    }
                }
            }
            catch (Exception ex)
            {
                IsBusy = false;
                string msg = ex.Message;
                if (ex is System.IO.IOException ioException) 
                {
                    Debug.WriteLine("LoginViewModel: Need to recover");
                    msg = Resx.AppResources.message_id_recover_datafile;
                }
                await Shell.Current.DisplayAlert(AppResources.LoginErrorMessage, msg, AppResources.alert_id_ok);
            }
        }
        private async void OnSignUpClicked()
        {
            if(CurrentUser.IsUserExist)
            {
                await Shell.Current.DisplayAlert(AppResources.SignUpPageTitle, AppResources.SignUpErrorMessage1, AppResources.alert_id_ok);
                return;
            }
            try
            {
                await DataStore.SignUpAsync(CurrentUser);
                _signUpAction?.Invoke(CurrentUser.Username);
                _ = await Shell.Current.Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert(AppResources.SignUpPageTitle, ex.Message, AppResources.alert_id_ok);
            }
            Debug.WriteLine($"LoginViewModel: OnSignUpClicked {Username}, DeviceLock: {IsDeviceLockEnabled}");
        }

        private async void OnCancelClicked()
        {
            _ = await Shell.Current.Navigation.PopModalAsync();
        }

        public async void ImportKeyFile()
        {
            var options = new PickOptions
            {
                PickerTitle = AppResources.import_message1,
                //FileTypes = customFileType,
            };

            try
            {
                var result = await FilePicker.PickAsync(options);
                if (result != null)
                {
                    var stream = await result.OpenReadAsync();
                    var fileStream = File.Create(CurrentUser.KeyFilePath);
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(fileStream);
                    fileStream.Close();
                }
                else 
                {
                    await Shell.Current.DisplayAlert(AppResources.action_id_import, AppResources.import_error_msg, AppResources.alert_id_ok);
                }
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong
                Debug.WriteLine($"LoginViewModel: ImportKeyFile, {ex}");
            }
        }

        public string GetMasterPassword()
        {
            return DataStore.GetMasterPassword();
        }

        public string GetDeviceLockData()
        {
            return DataStore.GetDeviceLockData();
        }

        public bool CreateKeyFile(string data)
        {
            return DataStore.CreateKeyFile(data, Username);
        }
    }
}
