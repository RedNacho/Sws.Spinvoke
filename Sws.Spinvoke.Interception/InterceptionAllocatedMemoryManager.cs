﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Sws.Spinvoke.Interception.MemoryManagement;

// TODO: PoC. Refactor. Check test cases.
namespace Sws.Spinvoke.Interception
{
	public static class InterceptionAllocatedMemoryManager
	{
		private static readonly PointerMemoryManager PointerMemoryManager = new PointerMemoryManager ();

		public static void BeginNamedBlock(string name)
		{
			PointerMemoryManager.BeginNamedBlock (name);
		}

		public static void EndNamedBlock()
		{
			PointerMemoryManager.EndNamedBlock ();
		}

		public static void GarbageCollectAll()
		{
			PointerMemoryManager.GarbageCollectAll ();
		}

		public static void GarbageCollectNamed(string blockName)
		{
			PointerMemoryManager.GarbageCollectNamed (blockName);
		}

		public static void GarbageCollectCurrentBlock()
		{		
			PointerMemoryManager.GarbageCollectCurrentBlock ();
		}

		public static bool HasUnnamedGarbageCollectibleMemory()
		{
			return PointerMemoryManager.HasUnnamedGarbageCollectibleMemory ();
		}

		public static IEnumerable<string> GetNamedBlocksWithGarbageCollectibleMemory()
		{
			return PointerMemoryManager.GetNamedBlocksWithGarbageCollectibleMemory ();
		}

		public static bool HasGarbageCollectibleMemory()
		{
			return PointerMemoryManager.HasGarbageCollectibleMemory ();
		}

		public static void RegisterForGarbageCollection(IntPtr ptr, Action<IntPtr> freeAction = null)
		{
			freeAction = EnsureFreeAction (freeAction);

			PointerMemoryManager.RegisterForGarbageCollection (ptr, freeAction);
		}

		public static void ReportPointerCallCompleted(IntPtr ptr, PointerManagementMode pointerManagementMode, Action<IntPtr> freeAction = null)
		{
			freeAction = EnsureFreeAction (freeAction);

			PointerMemoryManager.ReportPointerCallCompleted (ptr, pointerManagementMode, freeAction);
		}

		private static Action<IntPtr> EnsureFreeAction(Action<IntPtr> freeAction)
		{
			return freeAction ?? Marshal.FreeHGlobal;
		}
	}
}

