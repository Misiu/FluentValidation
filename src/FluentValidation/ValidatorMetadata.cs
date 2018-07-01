﻿namespace FluentValidation {
	using System;
	using Resources;
	using Validators;

	public class ValidatorMetadata {
		public ValidatorMetadata() {
		}

		public ValidatorMetadata(string propertyName) {
			PropertyName = propertyName;
		}

		private IStringSource _errorSource;
		private IStringSource _errorCodeSource;
		public Func<PropertyValidatorContext, object> CustomStateProvider { get; set; }

		public Severity Severity { get; set; }
		
		public IStringSource ErrorMessageSource {
			get => _errorSource;
			set => _errorSource = value ?? throw new ArgumentNullException(nameof(value));
		}
		
		public IStringSource ErrorCodeSource {
			get => _errorCodeSource;
			set => _errorCodeSource = value ?? throw new ArgumentNullException(nameof(value));
		}

		public string PropertyName { get; }

		public ValidatorMetadata Clone(string propertyName) {
			return new ValidatorMetadata(propertyName) {
				_errorCodeSource = _errorCodeSource,
				_errorSource = _errorSource,
				Severity = Severity,
			};
		}
	}
}