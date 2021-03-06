﻿using NUnit.Framework;
using System;
using System.Runtime.InteropServices;

using Moq;

using Sws.Spinvoke.Interception.ArgumentPreprocessing;
using Sws.Spinvoke.Interception.MemoryManagement;

namespace Sws.Spinvoke.Interception.Tests
{
	[TestFixture ()]
	public class StructToPointerArgumentPreprocessorTests
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct TestStruct
		{
			public int x;

			public int y;

			public int z;
		}

		[Test ()]
		public void CanProcessReturnsTrueForValueType()
		{
			var subject = new StructToPointerArgumentPreprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			var canProcessStruct = subject.CanProcess (new TestStruct ());

			Assert.IsTrue (canProcessStruct);
		}

		[Test ()]
		public void CanProcessReturnsFalseForReferenceType()
		{
			var subject = new StructToPointerArgumentPreprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			var canProcessObj = subject.CanProcess (new object ());

			Assert.IsFalse (canProcessObj);
		}

		[Test ()]
		public void StructureCanBeMarshaledBackFromPointer()
		{
			var subject = new StructToPointerArgumentPreprocessor (PointerManagementMode.DoNotDestroy, Mock.Of<PointerMemoryManager> ());

			var testData = new TestStruct () {
				x = 3, y = 4, z = 5
			};

			var ptr = (IntPtr)subject.Process (testData);

			var actual = Marshal.PtrToStructure (ptr, typeof(TestStruct));

			Marshal.FreeHGlobal (ptr);

			Assert.AreEqual (testData, actual);
		}
	}
}

