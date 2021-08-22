﻿using PassXYZ.Vault.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xamarin.Forms;
using Xamarin.Essentials;

using PassXYZ.Vault.Resx;

namespace PassXYZ.Vault.ViewModels
{
    /// <summary>
    /// Extend class PassXYZLib.User to support preference.
    /// </summary>
    public class LoginUser : PassXYZLib.User
    {
        const string CURRENT_USERNAME = "current_username";
        override public string Username 
        { 
            get
            {
                if(string.IsNullOrEmpty(base.Username))
                {
                    string usrname = Preferences.Get(CURRENT_USERNAME, base.Username);
                    base.Username = usrname;
                    if(!base.IsUserExist)
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
                Preferences.Set(CURRENT_USERNAME, base.Username);
            }
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
            get => _username;

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
            get => _isDeviceLockEnabled;
            set {
                _ = SetProperty(ref _isDeviceLockEnabled, value);
                CurrentUser.IsDeviceLockEnabled = value;
            }
        }
        public static LoginUser CurrentUser { get; set; }

        public LoginViewModel()
        {
            LoginCommand = new Command(OnLoginClicked, ValidateLogin);
            SignUpCommand = new Command(OnSignUpClicked, ValidateSignUp);
            CancelCommand = new Command(OnCancelClicked);
            this.PropertyChanged +=
                (_, __) => LoginCommand.ChangeCanExecute();

            this.PropertyChanged +=
                (_, __) => SignUpCommand.ChangeCanExecute();

            CurrentUser = new LoginUser();
        }

        public LoginViewModel(Action<string> signUpAction) : this()
        {
            _signUpAction = signUpAction;
        }

        private bool ValidateLogin()
        {
            return !string.IsNullOrWhiteSpace(_username)
                && !string.IsNullOrWhiteSpace(_password);
        }

        private bool ValidateSignUp()
        {
            return !string.IsNullOrWhiteSpace(_username)
                && !string.IsNullOrWhiteSpace(_password)
                && !string.IsNullOrWhiteSpace(_password2)
                && _password.Equals(_password2);
        }

        public void OnAppearing()
        {
            if (DataStore.RootGroup != null) { DataStore.Logout(); }
        }

        public async void OnLoginClicked()
        {
            // Prefixing with `//` switches to a different navigation stack instead of pushing to the active one
            try
            {
                bool status = await DataStore.LoginAsync(CurrentUser);

                if (status)
                {
                    if (AppShell.CurrentAppShell != null)
                    {
                        AppShell.CurrentAppShell.SetRootPageTitle(DataStore.RootGroup.Name);
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
                await Shell.Current.DisplayAlert(AppResources.LoginErrorMessage, ex.Message, AppResources.alert_id_ok);
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
                    var fileStream = File.Create(CurrentUser.KeFilePath);
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

        public async void ScanKeyFileQRCode()
        {
            Debug.WriteLine("LoginViewModel: ScanKeyFileQRCode");
        }
    }
}
