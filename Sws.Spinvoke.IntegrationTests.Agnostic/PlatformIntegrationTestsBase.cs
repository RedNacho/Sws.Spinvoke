﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

using Sws.Spinvoke.Core;
using Sws.Spinvoke.Core.Facade;
using Sws.Spinvoke.Interception;
using Sws.Spinvoke.Interception.DynamicProxy;
using Sws.Spinvoke.Interception.Facade;
using Sws.Spinvoke.Ninject;
using Sws.Spinvoke.Ninject.Extensions;
using Sws.Spinvoke.Ninject.Providers;

using CastleProxyGenerator = Castle.DynamicProxy.ProxyGenerator;

using Ninject;
using Ninject.Infrastructure;

using Moq;
namespace Sws.Spinvoke.IntegrationTests.Agnostic
{
	public abstract class PlatformIntegrationTestsBase
	{
		protected abstract INativeLibraryLoader CreateNativeLibraryLoader ();

		protected abstract string LibraryName { get; }

		protected abstract CallingConvention CallingConvention { get; }

		[Test ()]
		public void NativeCodeInvokedThroughGeneratedDelegate ()
		{
			var kernel = new StandardKernel ();

			kernel.Bind<INativeLibraryLoader>().ToMethod (context => CreateNativeLibraryLoader());

			kernel.Load (new SpinvokeModule (StandardScopeCallbacks.Transient, "TestAssembly"));

			using (var nativeDelegateResolver = kernel.Get<INativeDelegateResolver>())
			{
				var delegateInstance = nativeDelegateResolver.Resolve(new NativeDelegateDefinition(LibraryName, "add",
					new DelegateSignature(new [] { typeof(int), typeof(int) }, typeof(int), CallingConvention)));

				var result = delegateInstance.DynamicInvoke(new object[] { 2, 3 });

				Assert.AreEqual(5, result);
			}
		}

		[Test ()]
		public void NativeCodeInvokedThroughDynamicProxy()
		{
			var kernel = new StandardKernel ();

			kernel.Bind<INativeLibraryLoader> ().ToMethod (context => CreateNativeLibraryLoader ());

			kernel.Load (new SpinvokeModule (StandardScopeCallbacks.Transient, "TestAssembly"));

			var proxyGenerator = new CastleProxyGenerator ();

			using (var nativeDelegateResolver = kernel.Get<INativeDelegateResolver>())
			{
				var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<IDynamicProxyTest>(new SpinvokeInterceptor(new NativeDelegateInterceptor(
					LibraryName,
					CallingConvention,
					nativeDelegateResolver)));

				var result = proxy.Add(2, 3);

				Assert.AreEqual(5, result);
			}
		}

		[Test ()]
		public void NativeCodeInvokedThroughNinjectToNativeExtensionMethod()
		{
			var kernel = new StandardKernel ();

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader (), new ProxyGenerator (new CastleProxyGenerator ()));

			kernel.Bind<IDynamicProxyTest> ().ToNative (LibraryName)
				.WithCallingConvention(CallingConvention);

			var proxy = kernel.Get<IDynamicProxyTest> ();

			var result = proxy.Add (2, 3);

			Assert.AreEqual (5, result);
		}

		[Test ()]
		public void ProxyThrowsNotImplementedIfNoFallbackAndUnmapped()
		{
			const int TestInput = -5;

			var kernel = new StandardKernel();

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader(), new ProxyGenerator(new CastleProxyGenerator()));

			kernel.Bind<IDynamicProxyUnmappedTest> ().ToNative (LibraryName)
				.WithCallingConvention(CallingConvention);

			var proxy = kernel.Get<IDynamicProxyUnmappedTest>();

			Assert.Throws<NotImplementedException>(() => proxy.Abs (TestInput));
		}

		[Test ()]
		public void WithNonNativeFallbackThrowsInvalidOperationExceptionIfProxyGeneratorDoesNotSupportTargets()
		{
			var kernel = new StandardKernel();

			var noTargetProxyGeneratorMock = new Mock<IProxyGenerator> ();

			var nonNativeFallbackMock = new Mock<IDynamicProxyUnmappedTest> ();

			noTargetProxyGeneratorMock.Setup (pg => pg.AllowsTarget).Returns (false);

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader(), noTargetProxyGeneratorMock.Object);

			Assert.Throws<InvalidOperationException>(() => kernel.Bind<IDynamicProxyUnmappedTest> ().ToNative (LibraryName)
				.WithCallingConvention(CallingConvention)
				.WithNonNativeFallback (context => nonNativeFallbackMock.Object));
		}

		[Test ()]
		public void ProxyPassesThroughToFallbackIfUnmapped()
		{
			const int TestInput = -5;

			const int Expected = 5;

			var kernel = new StandardKernel();

			var nonNativeFallbackContexts = new List<NonNativeFallbackContext> ();

			var nonNativeFallbackMock = new Mock<IDynamicProxyUnmappedTest> ();

			nonNativeFallbackMock.Setup (nnf => nnf.Abs (It.IsAny<int> ()))
				.Returns ((int value) => Math.Abs (value));

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader(), new ProxyGenerator(new CastleProxyGenerator()));

			kernel.Bind<IDynamicProxyUnmappedTest>().ToNative(LibraryName)
				.WithCallingConvention(CallingConvention)
				.WithNonNativeFallback(nnfc => {
					nonNativeFallbackContexts.Add(nnfc);
					return nonNativeFallbackMock.Object;
				});

			var proxy = kernel.Get<IDynamicProxyUnmappedTest>();

			var actual = proxy.Abs (TestInput);

			nonNativeFallbackMock.Verify (nnf => nnf.Abs (TestInput), Times.Once);
			nonNativeFallbackMock.Verify (nnf => nnf.Abs (It.IsAny<int> ()), Times.Once);

			Assert.AreEqual (1, nonNativeFallbackContexts.Count ());

			var nonNativeFallbackContext = nonNativeFallbackContexts.Single();

			Assert.AreEqual(LibraryName, nonNativeFallbackContext.NativeDelegateInterceptorContext.LibraryName);
			Assert.AreEqual(CallingConvention, nonNativeFallbackContext.NativeDelegateInterceptorContext.CallingConvention);
			Assert.IsNotNull(nonNativeFallbackContext.NativeDelegateInterceptorContext.NativeDelegateResolver);
			Assert.IsNotNull(nonNativeFallbackContext.NinjectContext);

			Assert.AreEqual (Expected, actual);
		}

		[Test ()]
		public void NativeExpressionBuilderMapsIntoNativeCode ()
		{
			var kernel = new StandardKernel ();

			kernel.Bind<INativeLibraryLoader> ().ToMethod (context => CreateNativeLibraryLoader());

			kernel.Load (new SpinvokeModule (StandardScopeCallbacks.Transient, "NativeExpressionBuilderTest"));

			var nativeExpressionBuilder = kernel.Get<INativeExpressionBuilder> ();

			var addExpression = nativeExpressionBuilder.BuildNativeExpression<Func<int, int, int>> (LibraryName, "add", CallingConvention);

			var addFunc = addExpression.Compile ();

			var result = addFunc (5, 7);

			Assert.AreEqual (12, result);
		}

		[Test ()]
		public void CoreFacadeProducesWorkingNativeDelegateResolver ()
		{
			var facade = new SpinvokeCoreFacade.Builder (
				CreateNativeLibraryLoader ()).Build ();

			var nativeDelegateResolver = facade.NativeDelegateResolver;

			var nativeDelegate = nativeDelegateResolver.Resolve (new NativeDelegateDefinition (
				LibraryName,
				"add",
				new DelegateSignature(new [] { typeof(int), typeof(int) }, typeof(int), CallingConvention)
			));

			var result = nativeDelegate.DynamicInvoke (3, 4);

			facade.Dispose ();

			Assert.AreEqual (7, result);
		}

		[Test ()]
		public void CoreFacadeProducesWorkingNativeExpressionBuilder ()
		{
			var facade = new SpinvokeCoreFacade.Builder (
				CreateNativeLibraryLoader()).Build();

			var nativeExpressionBuilder = facade.NativeExpressionBuilder;

			var result = nativeExpressionBuilder.BuildNativeExpression<Func<int, int, int>> (
				LibraryName,
				"add",
				CallingConvention).Compile () (4, 5);

			facade.Dispose ();

			Assert.AreEqual (9, result);
		}

		[Test ()]
		public void InterceptionFacadeProducesWorkingNativeDelegateInterceptorFactory()
		{
			var facade = new SpinvokeInterceptionFacade.Builder().Build();

			var coreFacade = new SpinvokeCoreFacade.Builder(CreateNativeLibraryLoader()).Build();

			var nativeDelegateResolver = coreFacade.NativeDelegateResolver;

			var interceptor = facade.NativeDelegateInterceptorFactory.CreateInterceptor(new NativeDelegateInterceptorContext(
				LibraryName,
				CallingConvention,
				nativeDelegateResolver));

			var proxyGenerator = new CastleProxyGenerator ();

			var proxy = proxyGenerator.CreateInterfaceProxyWithoutTarget<IDynamicProxyTest> (new SpinvokeInterceptor (interceptor));

			var result = proxy.Add(7, 9);

			coreFacade.Dispose ();

			Assert.AreEqual(16, result);
		}

		protected void TestNativeAddFunctionWithDecimalResult<TDynamicProxyWithAddFunction>(Func<TDynamicProxyWithAddFunction, int, int, decimal> addFunction)
			where TDynamicProxyWithAddFunction : class
		{
			var kernel = new StandardKernel();

			SpinvokeNinjectExtensionsConfiguration.Configure(CreateNativeLibraryLoader(), new ProxyGenerator(new CastleProxyGenerator()));

			kernel.Bind<TDynamicProxyWithAddFunction>().ToNative(LibraryName)
				.WithCallingConvention(CallingConvention);

			var proxy = kernel.Get<TDynamicProxyWithAddFunction>();

			var result = addFunction(proxy, 2, 3);

			Assert.AreEqual(5, result);
		}

		protected void TestNativeAddFunction<TDynamicProxyWithAddFunction>(Func<TDynamicProxyWithAddFunction, int, int, int> addFunction)
			where TDynamicProxyWithAddFunction : class
		{
			var kernel = new StandardKernel();

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader(), new ProxyGenerator(new CastleProxyGenerator()));

			kernel.Bind<TDynamicProxyWithAddFunction>().ToNative(LibraryName)
				.WithCallingConvention(CallingConvention);

			var proxy = kernel.Get<TDynamicProxyWithAddFunction>();

			var result = addFunction(proxy, 2, 3);

			Assert.AreEqual(5, result);
		}

		protected void TestNativeReverseStringFunction<TDynamicProxyWithReverseStringFunction>(Func<TDynamicProxyWithReverseStringFunction, string, string> reverseStringFunction)
			where TDynamicProxyWithReverseStringFunction : class
		{
			const string TestString = "I am a test.";

			var expected = new string(TestString.Reverse ().ToArray ());

			var kernel = new StandardKernel();

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader(), new ProxyGenerator(new CastleProxyGenerator()));

			kernel.Bind<TDynamicProxyWithReverseStringFunction>().ToNative(LibraryName)
				.WithCallingConvention(CallingConvention);

			var proxy = kernel.Get<TDynamicProxyWithReverseStringFunction>();

			var actual = reverseStringFunction (proxy, TestString);

			Assert.AreEqual (expected, actual);
		}

		protected void TestNativeReverseStringFunctionWithManualReleaseInput<TDynamicProxyWithReverseStringFunctionWithManualReleaseInput>(
			Func<TDynamicProxyWithReverseStringFunctionWithManualReleaseInput, string, string> reverseStringFunction
		)
			where TDynamicProxyWithReverseStringFunctionWithManualReleaseInput : class
		{
			const string TestString = "I am a test.";

			var expected = new string(TestString.Reverse ().ToArray ());

			var kernel = new StandardKernel();

			SpinvokeNinjectExtensionsConfiguration.Configure (CreateNativeLibraryLoader(), new ProxyGenerator(new CastleProxyGenerator()));

			kernel.Bind<TDynamicProxyWithReverseStringFunctionWithManualReleaseInput>().ToNative(LibraryName)
				.WithCallingConvention(CallingConvention);

			var proxy = kernel.Get<TDynamicProxyWithReverseStringFunctionWithManualReleaseInput>();

			var actual = reverseStringFunction(proxy, TestString);

			var hasGarbageCollectibleMemoryBefore = InterceptionAllocatedMemoryManager.HasGarbageCollectibleMemory ();

			InterceptionAllocatedMemoryManager.GarbageCollectAll ();

			var hasGarbageCollectibleMemoryAfter = InterceptionAllocatedMemoryManager.HasGarbageCollectibleMemory ();

			Assert.AreEqual (expected, actual);
			Assert.IsTrue (hasGarbageCollectibleMemoryBefore);
			Assert.IsFalse (hasGarbageCollectibleMemoryAfter);
		}
	}

	public interface IDynamicProxyTest
	{
		[NativeDelegateDefinitionOverride(FunctionName = "add")]
		int Add(int x, int y);
	}

	public interface IDynamicProxyUnmappedTest
	{
		[NativeDelegateDefinitionOverride(MapNative = false)]
		int Abs(int value);
	}
}

