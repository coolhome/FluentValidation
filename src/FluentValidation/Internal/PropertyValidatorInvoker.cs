#region License
// Copyright (c) .NET Foundation and contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// The latest version of this file can be found at https://github.com/FluentValidation/FluentValidation
#endregion

namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Validators;

	internal class PropertyValidatorInvoker<T, TValue, TTransformed> : List<IPropertyValidator>, IPropertyValidatorInvoker<T> {

		protected Func<T, TValue, TTransformed> Transformer { get; }

		public PropertyValidatorInvoker() {}

		public PropertyValidatorInvoker(Func<T, TValue, TTransformed> transformer) {
			Transformer = transformer;
		}

		public IEnumerable<IPropertyValidator> AsEnumerable() {
			return this;
		}

		void IPropertyValidatorInvoker<T>.Remove(IPropertyValidator item) {
			base.Remove(item);
		}


		public virtual void Validate(ValidationContext<T> context, PropertyRule<T> rule) {
			string displayName = rule.GetDisplayName(context);

			if (rule.PropertyName == null && displayName == null) {
				//No name has been specified. Assume this is a model-level rule, so we should use empty string instead.
				displayName = string.Empty;
			}

			// Construct the full name of the property, taking into account overriden property names and the chain (if we're in a nested validator)
			string propertyName = context.PropertyChain.BuildPropertyName(rule.PropertyName ?? displayName);

			// Ensure that this rule is allowed to run.
			// The validatselector has the opportunity to veto this before any of the validators execute.
			if (!context.Selector.CanExecute(rule, propertyName, context)) {
				return;
			}

			if (rule.Condition != null) {
				if (!rule.Condition(context)) {
					return;
				}
			}

			// TODO: For FV 9, throw an exception by default if synchronous validator has async condition.
			if (rule.AsyncCondition != null) {
				if (!rule.AsyncCondition(context, default).GetAwaiter().GetResult()) {
					return;
				}
			}

			var cascade = rule.CascadeMode;
			var accessor = new Lazy<object>(() => GetPropertyValue(context.InstanceToValidate, rule), LazyThreadSafetyMode.None);
			var totalFailures = context.Failures.Count;

			// Invoke each validator and collect its results.
			foreach (var validator in this) {
				if (validator.ShouldValidateAsynchronously(context)) {
					InvokePropertyValidatorAsync(context, rule, validator, propertyName, accessor, default).GetAwaiter().GetResult();
				}
				else {
					InvokePropertyValidator(context, rule, validator, propertyName, accessor);
				}

				// If there has been at least one failure, and our CascadeMode has been set to StopOnFirst
				// then don't continue to the next rule
#pragma warning disable 618
				if (context.Failures.Count > totalFailures && (cascade == CascadeMode.StopOnFirstFailure || cascade == CascadeMode.Stop)) {
#pragma warning restore 618
					break;
				}
			}

			if (context.Failures.Count > totalFailures) {
				// Callback if there has been at least one property validator failed.
				var failuresThisRound = context.Failures.Skip(totalFailures).ToList();
				rule.OnFailure?.Invoke(context.InstanceToValidate, failuresThisRound);
			}
			else {
				foreach (var dependentRule in rule.DependentRules) {
					dependentRule.Validate(context);
				}
			}
		}

		public virtual async Task ValidateAsync(ValidationContext<T> context, PropertyRule<T> rule, CancellationToken cancellation) {
			if (!context.IsAsync()) {
				context.RootContextData["__FV_IsAsyncExecution"] = true;
			}

			string displayName = rule.GetDisplayName(context);

			if (rule.PropertyName == null && displayName == null) {
				//No name has been specified. Assume this is a model-level rule, so we should use empty string instead.
				displayName = string.Empty;
			}

			// Construct the full name of the property, taking into account overriden property names and the chain (if we're in a nested validator)
			string propertyName = context.PropertyChain.BuildPropertyName(rule.PropertyName ?? displayName);

			// Ensure that this rule is allowed to run.
			// The validatselector has the opportunity to veto this before any of the validators execute.
			if (!context.Selector.CanExecute(rule, propertyName, context)) {
				return;
			}

			if (rule.Condition != null) {
				if (!rule.Condition(context)) {
					return;
				}
			}

			if (rule.AsyncCondition != null) {
				if (! await rule.AsyncCondition(context, cancellation)) {
					return;
				}
			}

			var cascade = rule.CascadeMode;
			var accessor = new Lazy<object>(() => GetPropertyValue(context.InstanceToValidate, rule), LazyThreadSafetyMode.None);
			var totalFailures = context.Failures.Count;

			// Invoke each validator and collect its results.
			foreach (var validator in this) {
				cancellation.ThrowIfCancellationRequested();

				if (validator.ShouldValidateAsynchronously(context)) {
					await InvokePropertyValidatorAsync(context, rule, validator, propertyName, accessor, cancellation);
				}
				else {
					InvokePropertyValidator(context, rule, validator, propertyName, accessor);
				}

				// If there has been at least one failure, and our CascadeMode has been set to StopOnFirst
				// then don't continue to the next rule
#pragma warning disable 618
				if (context.Failures.Count > totalFailures && (cascade == CascadeMode.StopOnFirstFailure || cascade == CascadeMode.Stop)) {
#pragma warning restore 618
					break;
				}
			}

			if (context.Failures.Count > totalFailures) {
				var failuresThisRound = context.Failures.Skip(totalFailures).ToList();
				rule.OnFailure?.Invoke(context.InstanceToValidate, failuresThisRound);
			}
			else {
				foreach (var dependentRule in rule.DependentRules) {
					cancellation.ThrowIfCancellationRequested();
					await dependentRule.ValidateAsync(context, cancellation);
				}
			}
		}

		private async Task InvokePropertyValidatorAsync(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator validator, string propertyName, Lazy<object> accessor, CancellationToken cancellation) {
			if (!validator.Options.InvokeCondition(context)) return;
			if (!await validator.Options.InvokeAsyncCondition(context, cancellation)) return;
			var propertyContext = PropertyValidatorContext.Create(context, rule, propertyName, accessor);
			await validator.ValidateAsync(propertyContext, cancellation);
		}

		private void InvokePropertyValidator(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator validator, string propertyName, Lazy<object> accessor) {
			if (!validator.Options.InvokeCondition(context)) return;
			var propertyContext = PropertyValidatorContext.Create(context, rule, propertyName, accessor);
			validator.Validate(propertyContext);
		}

		private object GetPropertyValue(T instanceToValidate, PropertyRule<T> rule) {
			var value = rule.PropertyFunc(instanceToValidate);
			if (Transformer != null) value = Transformer(instanceToValidate, (TValue) value);
			return value;
		}
	}
}
