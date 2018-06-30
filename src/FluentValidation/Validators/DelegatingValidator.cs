#region License

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
// The latest version of this file can be found at https://github.com/jeremyskinner/FluentValidation

#endregion

namespace FluentValidation.Validators {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FluentValidation.Internal;
	using Resources;
	using Results;

	public class DelegatingValidator : IPropertyValidator, IDelegatingValidator, IValidationWorker {
		private readonly Func<IValidationContext, bool> _condition;
		private readonly Func<IValidationContext, CancellationToken, Task<bool>> _asyncCondition;
		public IPropertyValidator InnerValidator { get; private set; }

		public void Validate(IValidationContext context) {
			Validate((PropertyValidatorContext)context).ForEach(context.AddFailure);
		}

		public async Task ValidateAsync(IValidationContext context, CancellationToken cancellationToken) {
			var failures = await ValidateAsync((PropertyValidatorContext) context, cancellationToken);
			failures.ForEach(context.AddFailure);
		}

		public bool ShouldValidateAsync(IValidationContext context) {
			return (InnerValidator is IValidationWorker w && w.ShouldValidateAsync(context)) || _asyncCondition != null;
		}

		public DelegatingValidator(Func<IValidationContext, bool> condition, IPropertyValidator innerValidator) {
			_condition = condition;
			_asyncCondition = null;
			InnerValidator = innerValidator;
		}

		public DelegatingValidator(Func<IValidationContext, CancellationToken, Task<bool>> asyncCondition, IPropertyValidator innerValidator) {
			_condition = _ => true;
			_asyncCondition = asyncCondition;
			InnerValidator = innerValidator;
		}

		public IStringSource ErrorMessageSource {
			get => InnerValidator.ErrorMessageSource;
			set => InnerValidator.ErrorMessageSource = value;
		}

		public IStringSource ErrorCodeSource {
			get => InnerValidator.ErrorCodeSource;
			set => InnerValidator.ErrorCodeSource = value;
		}

		public IEnumerable<ValidationFailure> Validate(PropertyValidatorContext context) {
			if (_condition(context.ParentContext)) {
				return InnerValidator.Validate(context);
			}

			return Enumerable.Empty<ValidationFailure>();
		}

		public async Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext context, CancellationToken cancellation) {
			if (!_condition(context.ParentContext))
				return Enumerable.Empty<ValidationFailure>();

			if (_asyncCondition == null)
				return await InnerValidator.ValidateAsync(context, cancellation);

			bool shouldValidate = await _asyncCondition(context.ParentContext, cancellation);

			if (shouldValidate) {
				return await InnerValidator.ValidateAsync(context, cancellation);
			}

			return Enumerable.Empty<ValidationFailure>();
		}

		public bool SupportsStandaloneValidation => false;

		public Func<PropertyValidatorContext, object> CustomStateProvider {
			get => InnerValidator.CustomStateProvider;
			set => InnerValidator.CustomStateProvider = value;
		}

		public Severity Severity {
			get => InnerValidator.Severity;
			set => InnerValidator.Severity = value;
		}

		IPropertyValidator IDelegatingValidator.InnerValidator => InnerValidator;

		public bool CheckCondition(ValidationContext context) {
			return _condition(context);
		}
	}

	public interface IDelegatingValidator : IPropertyValidator {
		IPropertyValidator InnerValidator { get; }
		bool CheckCondition(ValidationContext context);
	}
}