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
	using System.Threading;
	using System.Threading.Tasks;
	using Results;
	using Validators;

	/// <summary>
	/// Rule element for collection validators
	/// </summary>
	public class CollectionRuleElement<T> : RuleElement {
		public CollectionRuleElement(IValidationWorker worker, ValidatorMetadata metadata, PropertyRule rule) : base(worker, metadata, rule) {
		}

		public override Task<bool> ValidateAsync(IValidationContext context, string propertyName, CancellationToken cancellation) {
			if (!(context is ValidationContext ctx)) {
				throw new InvalidOperationException("Cannot use RuleForEach without a ValidationContext. The context supplied was of type " + context.GetType().FullName);
			}

			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(Rule.Expression);
			}

			var propertyContext = new PropertyValidatorContext(context, Rule, Metadata.Clone(propertyName));

			if (Worker is IDelegatingValidator delegatingValidator && !delegatingValidator.CheckCondition(propertyContext.ParentContext)) {
				// Condition failed. Return immediately. 
				return TaskHelpers.FromResult(true);
			}

			if (!(propertyContext.PropertyValue is IEnumerable<T> collectionPropertyValue)) {
				// Property is not IEnumerable. Return immediately.
				return TaskHelpers.FromResult(true);
			}
			
			if (string.IsNullOrEmpty(propertyName)) {
				throw new InvalidOperationException("Could not automatically determine the property name ");
			}

			var results = new List<ValidationFailure>();

			IEnumerable<Task> validators = collectionPropertyValue.Select(async (v, count) => {
				var newContext = ctx.CloneForChildCollectionValidator(context.Model);
				newContext.PropertyChain.Add(propertyName);
				newContext.PropertyChain.AddIndexer(count);

				var newPropertyContext = new PropertyValidatorContext(newContext, Rule, Metadata.Clone(newContext.PropertyChain.ToString()), v);

				await Worker.ValidateAsync(newPropertyContext, cancellation);
				results.AddRange(newContext.Failures);
			});

			return TaskHelpers.Iterate(validators, cancellation).Then(() => {
				results.ForEach(ctx.AddFailure);
				return !results.Any();
			}, runSynchronously: true, cancellationToken: cancellation);

		}

		public override bool Validate(IValidationContext context, string propertyName) {
			if (!(context is ValidationContext ctx)) {
				throw new InvalidOperationException("Cannot use RuleForEach without a ValidationContext. The context supplied was of type " + context.GetType().FullName);
			}

			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(Rule.Expression);
			}

			var propertyContext = new PropertyValidatorContext(context, Rule, Metadata.Clone(propertyName));
			var results = new List<ValidationFailure>();
			var delegatingValidator = Worker as IDelegatingValidator;
			if (delegatingValidator == null || delegatingValidator.CheckCondition(propertyContext.ParentContext)) {
				var collectionPropertyValue = propertyContext.PropertyValue as IEnumerable<T>;

				int count = 0;

				if (collectionPropertyValue != null) {
					if (string.IsNullOrEmpty(propertyName)) {
						throw new InvalidOperationException("Could not automatically determine the property name ");
					}

					foreach (var element in collectionPropertyValue) {
						var newContext = ctx.CloneForChildCollectionValidator(context.Model);
						newContext.PropertyChain.Add(propertyName);
						newContext.PropertyChain.AddIndexer(count++);

						var newPropertyContext = new PropertyValidatorContext(newContext, Rule, Metadata.Clone(newContext.PropertyChain.ToString()), element);
						Worker.Validate(newPropertyContext);
						results.AddRange(newContext.Failures);
					}
				}
			}

			results.ForEach(ctx.AddFailure);
			return !results.Any();
		}

		private string InferPropertyName(LambdaExpression expression) {
			var paramExp = expression.Body as ParameterExpression;

			if (paramExp == null) {
				throw new InvalidOperationException("Could not infer property name for expression: " + expression + ". Please explicitly specify a property name by calling OverridePropertyName as part of the rule chain. Eg: RuleForEach(x => x).NotNull().OverridePropertyName(\"MyProperty\")");
			}

			return paramExp.Name;
		}
	}
}