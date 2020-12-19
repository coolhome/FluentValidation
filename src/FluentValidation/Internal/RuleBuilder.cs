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
	using System.Linq.Expressions;
	using Validators;

	/// <summary>
	/// Builds a validation rule and constructs a validator.
	/// </summary>
	/// <typeparam name="T">Type of object being validated</typeparam>
	/// <typeparam name="TTransformed">Type of property being validated</typeparam>
	internal class RuleBuilder<T, TTransformed> : IRuleBuilderOptions<T, TTransformed>, IRuleBuilderInitial<T, TTransformed>, IRuleBuilderInitialCollection<T,TTransformed> {
		/// <summary>
		/// The rule being created by this RuleBuilder.
		/// </summary>
		public PropertyRule<T> Rule { get; }

		/// <summary>
		/// Parent validator
		/// </summary>
		public AbstractValidator<T> ParentValidator { get; }

		/// <summary>
		/// Creates a new instance of the <see cref="RuleBuilder{T,TProperty}">RuleBuilder</see> class.
		/// </summary>
		public RuleBuilder(PropertyRule<T> rule, AbstractValidator<T> parent) {
			Rule = rule;
			ParentValidator = parent;
		}

		/// <summary>
		/// Sets the validator associated with the rule.
		/// </summary>
		/// <param name="validator">The validator to set</param>
		/// <returns></returns>
		public IRuleBuilderOptions<T, TTransformed> SetValidator(IPropertyValidator validator) {
			validator.Guard("Cannot pass a null validator to SetValidator.", nameof(validator));
			Rule.AddValidator(validator);
			return this;
		}

		/// <summary>
		/// Sets the validator associated with the rule. Use with complex properties where an IValidator instance is already declared for the property type.
		/// </summary>
		/// <param name="validator">The validator to set</param>
		/// <param name="ruleSets"></param>
		public IRuleBuilderOptions<T, TTransformed> SetValidator(IValidator<TTransformed> validator, params string[] ruleSets) {
			validator.Guard("Cannot pass a null validator to SetValidator", nameof(validator));
			var adaptor = new ChildValidatorAdaptor<T,TTransformed>(validator, validator.GetType()) {
				RuleSets = ruleSets
			};
			SetValidator(adaptor);
			return this;
		}

		/// <summary>
		/// Sets the validator associated with the rule. Use with complex properties where an IValidator instance is already declared for the property type.
		/// </summary>
		/// <param name="validatorProvider">The validator provider to set</param>
		/// <param name="ruleSets"></param>
		public IRuleBuilderOptions<T, TTransformed> SetValidator<TValidator>(Func<T, TValidator> validatorProvider, params string[] ruleSets)
			where TValidator : IValidator<TTransformed> {
			validatorProvider.Guard("Cannot pass a null validatorProvider to SetValidator", nameof(validatorProvider));
			SetValidator(new ChildValidatorAdaptor<T,TTransformed>(context => validatorProvider((T) context.InstanceToValidate), typeof (TValidator)) {
				RuleSets = ruleSets
			});
			return this;
		}

		/// <summary>
		/// Associates a validator provider with the current property rule.
		/// </summary>
		/// <param name="validatorProvider">The validator provider to use</param>
		/// <param name="ruleSets"></param>
		public IRuleBuilderOptions<T, TTransformed> SetValidator<TValidator>(Func<T, TTransformed, TValidator> validatorProvider, params string[] ruleSets) where TValidator : IValidator<TTransformed> {
			validatorProvider.Guard("Cannot pass a null validatorProvider to SetValidator", nameof(validatorProvider));
			SetValidator(new ChildValidatorAdaptor<T,TTransformed>(context => validatorProvider((T) context.InstanceToValidate, (TTransformed) context.PropertyValue), typeof (TValidator)) {
				RuleSets = ruleSets
			});
			return this;
		}

		IRuleBuilderInitial<T, TTransformed> IRuleBuilderInitial<T, TTransformed>.Configure(Action<PropertyRule<T>> configurator) {
			configurator(Rule);
			return this;
		}

		IRuleBuilderOptions<T, TTransformed> IRuleBuilderOptions<T, TTransformed>.Configure(Action<PropertyRule<T>> configurator) {
			configurator(Rule);
			return this;
		}

		IRuleBuilderInitialCollection<T, TTransformed> IRuleBuilderInitialCollection<T, TTransformed>.Configure(Action<CollectionPropertyRule<T, TTransformed>> configurator) {
			configurator((CollectionPropertyRule<T, TTransformed>) Rule);
			return this;
		}

		public IRuleBuilderInitial<T, TNew> Transform<TNew>(Func<TTransformed, TNew> transformationFunc) {
			if (transformationFunc == null) throw new ArgumentNullException(nameof(transformationFunc));
			Rule.ApplyTransformer(transformationFunc);
			return new RuleBuilder<T, TNew>(Rule, ParentValidator);
		}

		/// <summary>
		/// Creates a scope for declaring dependent rules.
		/// </summary>
		public IRuleBuilderOptions<T, TTransformed> DependentRules(Action action) {
			var dependencyContainer = new List<PropertyRule<T>>();

			// Capture any rules added to the parent validator inside this delegate.
			using (ParentValidator.Rules.Capture(dependencyContainer.Add)) {
				action();
			}

			if (Rule.RuleSets.Length > 0) {
				foreach (var dependentRule in dependencyContainer) {
					if (dependentRule is PropertyRule<T> propRule && propRule.RuleSets.Length == 0) {
						propRule.RuleSets = Rule.RuleSets;
					}
				}
			}

			Rule.DependentRules.AddRange(dependencyContainer);
			return this;
		}
	}

}
