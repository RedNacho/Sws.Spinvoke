﻿using System;
using System.Runtime.InteropServices;
using NUnit.Framework;

using Moq;

using Sws.Spinvoke.Interception.ReturnPostprocessing;
using Sws.Spinvoke.Interception.MemoryManagement;

namespace Sws.Spinvoke.Interception.Tests
{
	[TestFixture ()]
	public class PointerToStructReturnPostprocessorTests
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct TestStruct
		{
			public int x;

			public int y;

			public int z;
		}

		[Test ()]
		public void CanProcessReturnsTrueForIntPtrAndRequiredValueType()
		{
			var subject = new PointerToStructReturnPostprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			var output = new IntPtr (0);

			var canProcess = subject.CanProcess (output, typeof(TestStruct));

			Assert.IsTrue (canProcess);
		}

		[Test ()]
		public void CanProcessReturnsFalseForNonIntPtr()
		{
			var subject = new PointerToStructReturnPostprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			const int Output = 75;

			var canProcess = subject.CanProcess (Output, typeof(TestStruct));

			Assert.IsFalse (canProcess);
		}

		[Test ()]
		public void CanProcessReturnsFalseForRequiredReferenceType()
		{
			var subject = new PointerToStructReturnPostprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			var output = new IntPtr (0);

			var canProcess = subject.CanProcess (output, typeof(object));

			Assert.IsFalse (canProcess);
		}

		[Test ()]
		public void PreallocatedPointerIsMarshaledBackToStruct()
		{
			var subject = new PointerToStructReturnPostprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			var ptr = Marshal.AllocHGlobal (Marshal.SizeOf(typeof(TestStruct)));

			var testData = new TestStruct () { x = 7, y = 8, z = 9 };

			Marshal.StructureToPtr (testData, ptr, true);

			var result = subject.Process (ptr, typeof(TestStruct));

			Marshal.FreeHGlobal (ptr);

			Assert.AreEqual (testData, result);
		}
	}
}

