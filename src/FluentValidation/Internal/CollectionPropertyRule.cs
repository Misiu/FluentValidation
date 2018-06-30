﻿#region License

// Copyright (c) Jeremy Skinner (http://www.jeremyskinner.co.uk)
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
// The latest version of this file can be found at https://github.com/JeremySkinner/FluentValidation

#endregion

namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using Results;
	using Validators;

	/// <summary>
	/// Rule definition for collection properties
	/// </summary>
	/// <typeparam name="TProperty"></typeparam>
	public class CollectionPropertyRule<TProperty> : PropertyRule {
		/// <summary>
		/// Initializes new instance of the CollectionPropertyRule class
		/// </summary>
		/// <param name="member"></param>
		/// <param name="propertyFunc"></param>
		/// <param name="expression"></param>
		/// <param name="cascadeModeThunk"></param>
		/// <param name="typeToValidate"></param>
		/// <param name="containerType"></param>
		public CollectionPropertyRule(MemberInfo member, Func<object, object> propertyFunc, LambdaExpression expression, Func<CascadeMode> cascadeModeThunk, Type typeToValidate, Type containerType) : base(member, propertyFunc, expression, cascadeModeThunk, typeToValidate, containerType) {
		}

		/// <summary>
		/// Creates a new property rule from a lambda expression.
		/// </summary>
		public static CollectionPropertyRule<TProperty> Create<T>(Expression<Func<T, IEnumerable<TProperty>>> expression, Func<CascadeMode> cascadeModeThunk) {
			var member = expression.GetMember();
			var compiled = expression.Compile();

			return new CollectionPropertyRule<TProperty>(member, compiled.CoerceToNonGeneric(), expression, cascadeModeThunk, typeof(TProperty), typeof(T));
		}

		/// <summary>
		/// Invokes the validator asynchronously
		/// </summary>
		/// <param name="context"></param>
		/// <param name="validator"></param>
		/// <param name="propertyName"></param>
		/// <param name="cancellation"></param>
		/// <returns></returns>
		protected override Task<bool> InvokePropertyValidatorAsync(IValidationContext context, IValidationWorker validator, ValidatorMetadata metadata, string propertyName, CancellationToken cancellation) {
			if (!(context is ValidationContext ctx)) {
				throw new InvalidOperationException("Cannot use RuleForEach without a ValidationContext. The context supplied was of type " + context.GetType().FullName);
			}
			
			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(Expression);
			}

			var propertyContext = new PropertyValidatorContext(context, this, propertyName);
			var delegatingValidator = validator as IDelegatingValidator;

			if (delegatingValidator == null || delegatingValidator.CheckCondition(propertyContext.ParentContext)) {
				var collectionPropertyValue = propertyContext.PropertyValue as IEnumerable<TProperty>;

				if (collectionPropertyValue != null) {
					if (string.IsNullOrEmpty(propertyName)) {
						throw new InvalidOperationException("Could not automatically determine the property name ");
					}

					var results = new List<ValidationFailure>();

					IEnumerable<Task> validators = collectionPropertyValue.Select(async (v, count) => {
						var newContext = ctx.CloneForChildCollectionValidator(context.Model);
						newContext.PropertyChain.Add(propertyName);
						newContext.PropertyChain.AddIndexer(count);

						var newPropertyContext = new PropertyValidatorContext(newContext, this, newContext.PropertyChain.ToString(), v);

						await validator.ValidateAsync(newPropertyContext, cancellation);
						results.AddRange(newContext.Failures);
					});
					
					return TaskHelpers.Iterate(validators, cancellation).Then(() => {
						results.ForEach(ctx.AddFailure);
						return !results.Any();
					}, runSynchronously: true, cancellationToken: cancellation);
				}
			}

			return TaskHelpers.FromResult(true);
		}

		private string InferPropertyName(LambdaExpression expression) {
			var paramExp = expression.Body as ParameterExpression;

			if (paramExp == null) {
				throw new InvalidOperationException("Could not infer property name for expression: " + expression + ". Please explicitly specify a property name by calling OverridePropertyName as part of the rule chain. Eg: RuleForEach(x => x).NotNull().OverridePropertyName(\"MyProperty\")");
			}

			return paramExp.Name;
		}

		/// <summary>
		/// Invokes the validator
		/// </summary>
		/// <param name="context"></param>
		/// <param name="validator"></param>
		/// <param name="propertyName"></param>
		/// <returns></returns>
		protected override bool InvokePropertyValidator(IValidationContext context, IValidationWorker validator, ValidatorMetadata metadata, string propertyName) {
			if (!(context is ValidationContext ctx)) {
				throw new InvalidOperationException("Cannot use RuleForEach without a ValidationContext. The context supplied was of type " + context.GetType().FullName);
			}
			
			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(Expression);
			}

			var propertyContext = new PropertyValidatorContext(context, this, propertyName);
			var results = new List<ValidationFailure>();
			var delegatingValidator = validator as IDelegatingValidator;
			if (delegatingValidator == null || delegatingValidator.CheckCondition(propertyContext.ParentContext)) {
				var collectionPropertyValue = propertyContext.PropertyValue as IEnumerable<TProperty>;

				int count = 0;

				if (collectionPropertyValue != null) {
					if (string.IsNullOrEmpty(propertyName)) {
						throw new InvalidOperationException("Could not automatically determine the property name ");
					}

					foreach (var element in collectionPropertyValue) {
						var newContext = ctx.CloneForChildCollectionValidator(context.Model);
						newContext.PropertyChain.Add(propertyName);
						newContext.PropertyChain.AddIndexer(count++);

						var newPropertyContext = new PropertyValidatorContext(newContext, this, newContext.PropertyChain.ToString(), element);
						validator.Validate(newPropertyContext);
						results.AddRange(newContext.Failures);
					}
				}
			}

			results.ForEach(ctx.AddFailure);
			return !results.Any();
		}
	}
}