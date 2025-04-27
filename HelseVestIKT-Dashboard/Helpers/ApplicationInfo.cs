using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HelseVestIKT_Dashboard.Helpers
{

	public unsafe struct ApplicationInfo
	{
		// Assume these are the fixed buffers defined in your struct.
		// The actual size should match what the struct defines.
		public fixed byte ApplicationName[128];
		public fixed byte EngineName[128];
		public uint ApplicationVersion;
		public uint EngineVersion;
		public ulong ApiVersion;
	}
	public static unsafe class ApplicationInfoExtensions
	{
		// Copies a string into a fixed byte buffer and ensures it's null-terminated.
		public static void CopyToFixedBuffer(string source, byte* destination, int bufferSize)
		{
			// Reserve space for the null terminator.
			int length = Math.Min(source.Length, bufferSize - 1);
			for (int i = 0; i < length; i++)
			{
				destination[i] = (byte)source[i];
			}
			destination[length] = 0; // null terminator
		}
	}
}
