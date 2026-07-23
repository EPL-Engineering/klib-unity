using System.Runtime.InteropServices;
using UnityEngine;

namespace KLib
{
    public class VolumeManager
    {
        //The Unit to use when getting and setting the volume
        public enum VolumeUnit
        {
            //Perform volume action in decibels</param>
            Decibel,
            //Perform volume action in scalar
            Scalar
        }

        public VolumeManager()
        {
            Initialize();
        }

        [DllImport("SystemVolumePlugin")]
        public static extern float GetVolume();

        [DllImport("SystemVolumePlugin")]
        public static extern float GetVolumeDB();

        [DllImport("SystemVolumePlugin")]
        public static extern int SetVolume(float volume);

        [DllImport("SystemVolumePlugin")]
        public static extern int SetVolumeDB(float volume);

        [DllImport("SystemVolumePlugin")]
        public static extern int GetMute();

        [DllImport("SystemVolumePlugin")]
        public static extern int SetMute(int mute);

        [DllImport("SystemVolumePlugin")]
        public static extern int InitializeVolume();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDelegate(string str);

        [DllImport("SystemVolumePlugin")]
        public static extern void SetLoggingCallback(System.IntPtr func);

        [DllImport("SystemVolumePlugin")]
        [return: MarshalAs(UnmanagedType.BStr)]
        public static extern string GetFriendlyName();

        public float GetMasterVolume(VolumeUnit vUnit)
        {
            if (vUnit == VolumeUnit.Scalar)
            {
                return GetVolume();
            }
            return GetVolumeDB();
            //        return GetSystemVolume(vUnit);
        }
        public void SetMasterVolume(float volume, VolumeUnit vUnit)
        {
            Debug.Log("MASTER VOLUME = " + volume + " " + vUnit.ToString());

            if (vUnit == VolumeUnit.Scalar)
            {
                SetVolume(volume);
            }
            else
            {
                SetVolumeDB(volume);
            }

            //        SetSystemVolume(volume, vUnit);
        }

        public bool IsMuted()
        {
            return GetMute() == 1;
        }

        public void Mute(bool mute)
        {
            SetMute(mute ? 1 : 0);
        }

        public string FriendlyName { get { return GetFriendlyName(); } }

        public void PrintLogString(string str)
        {
            Debug.Log(str);
        }

        private void Initialize()
        {
            InitializeVolume();
        }
    }
}