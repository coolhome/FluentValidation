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
	using System.Reflection;
	using Results;

	/// <summary>
	/// Rule definition for collection properties
	/// </summary>
	/// <typeparam name="TElement"></typeparam>
	/// <typeparam name="T"></typeparam>
	public class CollectionPropertyRule<T, TElement> : PropertyRule<T> {

		/// <summary>
		/// Initializes new instance of the CollectionPropertyRule class
		/// </summary>
		internal CollectionPropertyRule(MemberInfo member, Func<T, object> propertyFunc, LambdaExpression expression, Func<CascadeMode> cascadeModeThunk, IPropertyValidatorInvoker<T> validators)
			: base(member, propertyFunc, expression, cascadeModeThunk, typeof(TElement), validators) {
		}

		/// <summary>
		/// Filter that should include/exclude items in the collection.
		/// </summary>
		public Func<TElement, bool> Filter { get; set; }

		/// <summary>
		/// Constructs the indexer in the property name associated with the error message.
		/// By default this is "[" + index + "]"
		/// </summary>
		public Func<object, IEnumerable<TElement>, TElement, int, string> IndexBuilder { get; set; }

		/// <summary>
		/// Creates a new property rule from a lambda expression.
		/// </summary>
		public static CollectionPropertyRule<T, TElement> Create(Expression<Func<T, IEnumerable<TElement>>> expression, Func<CascadeMode> cascadeModeThunk, bool bypassCache = false) {
			var member = expression.GetMember();
			var compiled = AccessorCache<T>.GetCachedAccessor(member, expression, bypassCache, "FV_RuleForEach");
			return new CollectionPropertyRule<T, TElement>(member, x => compiled(x), expression, cascadeModeThunk, new CollectionInvoker<T,TElement,TElement>());
		}

		internal override void ApplyTransformer<TValue, TTransformed>(Func<T, TValue,TTransformed> transformationFunc) {
			_validators = new CollectionInvoker<T,TValue,TTransformed>(transformationFunc);
		}
	}
}
