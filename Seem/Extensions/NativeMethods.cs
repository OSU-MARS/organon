﻿using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Mars.Seem.Extensions
{
    internal static class NativeMethods
    {
        private const int ProcessorInformation = 11;
        private const uint STATUS_SUCCESS = 0;

        // from https://www.pinvoke.net/default.aspx/powrprof.callntpowerinformation
        // CallNtPowerInformation() (apparently) reports only non-turbo information and therefore always returns constant values.
        public static ProcessorPowerInformation CallNtPowerInformation()
        {
            int procCount = Environment.ProcessorCount;
            PROCESSOR_POWER_INFORMATION[] nativePowerInfo = new PROCESSOR_POWER_INFORMATION[procCount];
            uint ntstatus = NativeMethods.CallNtPowerInformation(NativeMethods.ProcessorInformation,
                                                                 IntPtr.Zero,
                                                                 0,
                                                                 nativePowerInfo,
                                                                 (uint)(nativePowerInfo.Length * Marshal.SizeOf(typeof(PROCESSOR_POWER_INFORMATION))));
            if (ntstatus != STATUS_SUCCESS)
            {
                throw new Win32Exception((int)ntstatus, "P/Invoke of CallNtPowerInformation() failed.");
            }

            ProcessorPowerInformation managedPowerInfo = new(nativePowerInfo);
            return managedPowerInfo;
        }

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern UInt32 CallNtPowerInformation([In] Int32 InformationLevel,
                                                            [In] IntPtr lpInputBuffer,
                                                            [In] UInt32 nInputBufferSize,
                                                            [In, Out] PROCESSOR_POWER_INFORMATION[] lpOutputBuffer,
                                                            [In] UInt32 nOutputBufferSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSOR_POWER_INFORMATION
        {
            public uint Number;
            public uint MaxMhz;
            public uint CurrentMhz;
            public uint MhzLimit;
            public uint MaxIdleState;
            public uint CurrentIdleState;
        }
    }
}
