﻿using System;
using System.Runtime.InteropServices;

using Ninject.Activation;
using Ninject.Infrastructure.Introspection;
using Ninject.Planning.Bindings;
using Ninject.Syntax;

using Sws.Spinvoke.Core;
using Sws.Spinvoke.Interception;

// PoC - unit test, refactor.
namespace Sws.Spinvoke.Ninject.Extensions
{
	public static class BindingToSyntaxExtensions
	{
		private static INativeDelegateResolver NativeDelegateResolver;

		private static IProxyGenerator ProxyGenerator;

		private static INativeDelegateInterceptorFactory NativeDelegateInterceptorFactory;

		internal static void Configure(INativeDelegateResolver nativeDelegateResolver, IProxyGenerator proxyGenerator, INativeDelegateInterceptorFactory nativeDelegateInterceptorFactory)
		{
			NativeDelegateResolver = nativeDelegateResolver;
			ProxyGenerator = proxyGenerator;
			NativeDelegateInterceptorFactory = nativeDelegateInterceptorFactory;
		}

		private static void VerifyConfigured()
		{
			if (NativeDelegateResolver == null || ProxyGenerator == null) {
				throw new InvalidOperationException ("You must call SpinvokeNinjectExtensionsConfiguration.Configure first");
			}
		}

		public static ISpinvokeBindingWhenInNamedWithOrOnSyntax<T> ToNative<T>(this IBindingToSyntax<T> bindingToSyntax, string libraryName)
			where T : class
		{
			Type serviceType;

			var bindingBuilder = bindingToSyntax as BindingBuilder<T>;

			if (bindingBuilder == null) {
				serviceType = typeof(T);
			} else {
				serviceType = bindingBuilder.Binding.Service;
			}

			return ToNative (bindingToSyntax, serviceType, libraryName);
		}

		public static ISpinvokeBindingWhenInNamedWithOrOnSyntax<T> ToNative<T>(this IBindingToSyntax<T> bindingToSyntax, Type serviceType, string libraryName)
			where T : class
		{
			VerifyConfigured ();

			CallingConvention? callingConvention = null;

			Func<NonNativeFallbackContext, T> nonNativeFallbackSource = context => null;

			var nativeDelegateResolver = NativeDelegateResolver;

			var nativeDelegateInterceptorFactory = NativeDelegateInterceptorFactory;

			Func<NativeProxyProviderConfiguration<T>> nativeProxyProviderConfigurationSource = () =>
				new NativeProxyProviderConfiguration<T> () {
					CallingConvention = callingConvention.GetValueOrDefault(CallingConvention.Winapi),
					LibraryName = libraryName,
					NativeDelegateInterceptorFactory = nativeDelegateInterceptorFactory,
					NativeDelegateResolver = nativeDelegateResolver,
					NonNativeFallbackSource = nonNativeFallbackSource,
					ServiceType = serviceType
				};

			bindingToSyntax.BindingConfiguration.ProviderCallback = context => new NativeProxyProvider<T> (nativeProxyProviderConfigurationSource);

			return new SpinvokeBindingConfigurationBuilder<T> (
				bindingToSyntax.BindingConfiguration,
				serviceType.Format(),
				bindingToSyntax.Kernel,
				cc => callingConvention = cc,
				nnfs => {
					if (!ProxyGenerator.AllowsTarget) {
						throw new InvalidOperationException("In order to allow a non native fallback to be supplied, the proxy generator must support a target.");
					}

					nonNativeFallbackSource = nnfs;
				},
				ndr => nativeDelegateResolver = ndr,
				ndif => nativeDelegateInterceptorFactory = ndif
			);
		}

		private class NativeProxyProvider<T> : Provider<T>
			where T : class
		{
			private readonly Func<NativeProxyProviderConfiguration<T>> _configurationSource;

			public NativeProxyProvider(Func<NativeProxyProviderConfiguration<T>> configurationSource)
			{
				if (configurationSource == null)
					throw new ArgumentNullException("configurationSource");

				_configurationSource = configurationSource;
			}

			protected override T CreateInstance (IContext context)
			{
				var configuration = _configurationSource ();

				var nativeDelegateInterceptorContext = new NativeDelegateInterceptorContext (configuration.LibraryName, configuration.CallingConvention, configuration.NativeDelegateResolver);

				var nativeDelegateInterceptor = configuration.NativeDelegateInterceptorFactory.CreateInterceptor(nativeDelegateInterceptorContext);

				var nonNativeFallback = configuration.NonNativeFallbackSource (new NonNativeFallbackContext(nativeDelegateInterceptorContext, context));

				if (nonNativeFallback == null) {
					if (configuration.ServiceType == typeof(T)) {
						return ProxyGenerator.CreateProxy<T> (nativeDelegateInterceptor);
					} else {
						return ProxyGenerator.CreateProxy (configuration.ServiceType, nativeDelegateInterceptor) as T;
					}
				} else {
					if (configuration.ServiceType == typeof(T)) {
						return ProxyGenerator.CreateProxyWithTarget<T> (nativeDelegateInterceptor, nonNativeFallback);
					} else {
						return ProxyGenerator.CreateProxyWithTarget (configuration.ServiceType, nativeDelegateInterceptor, nonNativeFallback) as T;
					}
				}
			}
		}

		private class NativeProxyProviderConfiguration<T>
		{
			public Type ServiceType { get; set; }

			public string LibraryName { get; set; }

			public Func<NonNativeFallbackContext, T> NonNativeFallbackSource { get; set; }

			public CallingConvention CallingConvention { get; set; }

			public INativeDelegateResolver NativeDelegateResolver { get; set; }

			public INativeDelegateInterceptorFactory NativeDelegateInterceptorFactory { get; set; }
		}
	}
}

