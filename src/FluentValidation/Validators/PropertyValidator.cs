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

using System.Threading;

namespace FluentValidation.Validators {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FluentValidation.Internal;
	using Resources;
	using Results;

	[Obsolete("PropertyValidator is deprecated and will be removed in FluentValidation 9.0. For custom validators, you should either call RuleFor(..).Custom(expression) or inherit from ValidatorBase. For more information on upgrading, see https://fluentvalidation.net/upgrading-to-fluentvalidation-8")]
	public abstract class PropertyValidator : IPropertyValidator, IValidationWorker {
		private IStringSource _errorSource;
		private IStringSource _errorCodeSource;
		public Func<PropertyValidatorContext, object> CustomStateProvider { get; set; }

		public Severity Severity { get; set; }

		protected PropertyValidator(IStringSource errorMessageSource) {
			if(errorMessageSource == null) errorMessageSource = new StaticStringSource("No default error message has been specified.");
			_errorSource = errorMessageSource;
		}

		protected PropertyValidator(string errorMessageResourceName, Type errorMessageResourceType) {
			errorMessageResourceName.Guard("errorMessageResourceName must be specified.", nameof(errorMessageResourceName));
			errorMessageResourceType.Guard("errorMessageResourceType must be specified.", nameof(errorMessageResourceType));

			_errorSource = new LocalizedStringSource(errorMessageResourceType, errorMessageResourceName);
		}

		protected PropertyValidator(string errorMessage) {
			_errorSource = new StaticStringSource(errorMessage);
		}

		public IStringSource ErrorMessageSource {
			get { return _errorSource; }
			set {
				if (value == null) {
					throw new ArgumentNullException("value");
				}

				_errorSource = value;
			}
		}

		public virtual IEnumerable<ValidationFailure> Validate(PropertyValidatorContext context) {
			if (!IsValid(context)) {
				PrepareMessageFormatterForValidationError(context);
				return new[] { CreateValidationError(context) };
			}

			return Enumerable.Empty<ValidationFailure>();
		}

		public virtual Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext context, CancellationToken cancellation) {
			return
				IsValidAsync(context, cancellation)
				.Then(valid => {
					    if (valid) {
						    return Enumerable.Empty<ValidationFailure>();
					    }

						PrepareMessageFormatterForValidationError(context);
						return new[] { CreateValidationError(context) }.AsEnumerable();
				      },
					runSynchronously: true
				);
		}

		protected abstract bool IsValid(PropertyValidatorContext context);

#pragma warning disable 1998
		protected virtual async Task<bool> IsValidAsync(PropertyValidatorContext context, CancellationToken cancellation) {
			return IsValid(context);
		}
#pragma warning restore 1998

		/// <summary>
		/// Prepares the <see cref="MessageFormatter"/> of <paramref name="context"/> for an upcoming <see cref="ValidationFailure"/>.
		/// </summary>
		/// <param name="context">The validator context</param>
		protected virtual void PrepareMessageFormatterForValidationError(PropertyValidatorContext context) {
			context.MessageFormatter.AppendPropertyName(context.DisplayName);
			context.MessageFormatter.AppendPropertyValue(context.PropertyValue);
		}

		/// <summary>
		/// Creates an error validation result for this validator.
		/// </summary>
		/// <param name="context">The validator context</param>
		/// <returns>Returns an error validation result.</returns>
		protected virtual ValidationFailure CreateValidationError(PropertyValidatorContext context) {
			var messageBuilderContext = new MessageBuilderContext(context, _errorSource, this);

			var error = context.Rule.MessageBuilder != null 
				? context.Rule.MessageBuilder(messageBuilderContext) 
				: messageBuilderContext.GetDefaultMessage();

			var failure = new ValidationFailure(context.PropertyName, error, context.PropertyValue);
			failure.FormattedMessageArguments = context.MessageFormatter.AdditionalArguments;
			failure.FormattedMessagePlaceholderValues = context.MessageFormatter.PlaceholderValues;
			failure.ResourceName = _errorSource.ResourceName;
			failure.ErrorCode = (_errorCodeSource != null)
				? _errorCodeSource.GetString(context)
				: ValidatorOptions.ErrorCodeResolver(this);

			if (CustomStateProvider != null) {
				failure.CustomState = CustomStateProvider(context);
			}

			failure.Severity = Severity;
			return failure;
		}


		public IStringSource ErrorCodeSource {
			get => _errorCodeSource;
			set => _errorCodeSource = value ?? throw new ArgumentNullException(nameof(value));
		}

		bool IValidationWorker.Validate(IValidationContext context) {
			SetMetadata(((PropertyValidatorContext) context).Metadata);
			var failures = Validate((PropertyValidatorContext) context).ToList();
			failures.ForEach(context.AddFailure);
			return !failures.Any();
		}

		async Task<bool> IValidationWorker.ValidateAsync(IValidationContext context, CancellationToken cancellationToken) {
			SetMetadata(((PropertyValidatorContext) context).Metadata);
			var failures = (await ValidateAsync((PropertyValidatorContext) context, cancellationToken)).ToList();
			failures.ForEach(context.AddFailure);
			return !failures.Any();
		}

		private void SetMetadata(ValidatorMetadata metadata) {
			// For backwards compatibility, copy from the metadata object
			// to the metadata properties on the legacy PropertyValidator.
			if (metadata.ErrorCodeSource != null) {
				ErrorCodeSource = metadata.ErrorCodeSource;
			}

			if (metadata.ErrorMessageSource != null) {
				ErrorMessageSource = metadata.ErrorMessageSource;
			}

			if (metadata.CustomStateProvider != null) {
				CustomStateProvider = metadata.CustomStateProvider;
			}

			Severity = metadata.Severity;
		}

		public virtual bool ShouldValidateAsync(IValidationContext context) {
			return false;
		}
	}
}