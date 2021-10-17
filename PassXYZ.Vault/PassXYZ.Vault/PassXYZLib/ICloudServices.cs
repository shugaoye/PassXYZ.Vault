﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PassXYZLib
{
#if PASSXYZ_CLOUD_SERVICE
    public enum PxCloudType
    {
        OneDrive,
        WebDav,
        SFTP,
        FTP,
        SMB
    }

    public static class PxCloudConfig
    {
        public static PxCloudType CurrentServiceType { get => PxCloudType.SFTP; }

        public static string Username { get; set; }
        public static string Password { get; set; }
        public static string Hostname { get; set; }
        public static string RemoteHomePath { get; set; }

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(Username)
                    && !string.IsNullOrWhiteSpace(Password)
                    && !string.IsNullOrWhiteSpace(Hostname)
                    && !string.IsNullOrWhiteSpace(RemoteHomePath);

        private static PxSFtp pxSFtp = null;
        public static ICloudServices<PxUser> GetCloudServices()
        {
            if (pxSFtp == null)
            {
                pxSFtp = new PxSFtp();
            }
            return pxSFtp;
        }
    }

    public interface ICloudServices<T>
    {
        Task LoginAsync();
        bool IsConnected();
        Task<string> DownloadFileAsync(string filename, bool isMerge = false);
        Task UploadFileAsync(string filename);
        Task<bool> DeleteFileAsync(string filename);
        Task<IEnumerable<T>> LoadRemoteUsersAsync();
        Task<IEnumerable<T>> SynchronizeUsersAsync();
        void Logout();
        bool IsSynchronized { get; }
    }
#endif // PASSXYZ_CLOUD_SERVICE
}
