using System;
using System.Runtime.InteropServices;

namespace SplunkTest
{
	public class InteropEvents
	{
		public const uint STANDARD_RIGHTS_REQUIRED = 0x000F0000;
		public const uint SYNCHRONIZE = 0x00100000;
		public const uint EVENT_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0x3);
		public const uint EVENT_MODIFY_STATE = 0x0002;
		public const long ERROR_FILE_NOT_FOUND = 2L;

		[DllImport("Kernel32.dll")]
		public static extern Boolean SetEvent(IntPtr hEvent);

		[DllImport("Kernel32.dll")]
		public static extern Boolean ResetEvent(IntPtr hEvent);

		[DllImport("Kernel32.dll")]
		public static extern IntPtr OpenEvent(UInt32 dwDesiredAccess, Boolean bInheritHandle, String lpName);

		[DllImport("Kernel32.dll")]
		public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, Boolean bManualReset, Boolean bInitialState, String lpName);

		[DllImport("Kernel32.dll")]
		public static extern UInt32 WaitForSingleObject(IntPtr hHandle, Int32 dwMilliseconds);

		public static IntPtr CreateEvent(string eventName)
		{
			return CreateEvent(IntPtr.Zero, true, false, eventName);
		}

		public static IntPtr OpenEvent(string eventName)
		{
			return InteropEvents.OpenEvent(EVENT_ALL_ACCESS | EVENT_MODIFY_STATE, true, eventName);
		}
	}
}