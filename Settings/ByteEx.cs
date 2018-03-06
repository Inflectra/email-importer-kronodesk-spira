using System.Runtime.InteropServices;

namespace Inflectra.KronoDesk.Service.Email.Settings
{
	public static class BytesEx
	{
		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern int memcmp(byte[] b1, byte[] b2, long count);

		public static bool Matches(this byte[] byte1, byte[] byte2)
		{
			return (byte1.Length == byte2.Length && memcmp(byte1, byte2, byte1.Length) == 0);
		}
	}
}
