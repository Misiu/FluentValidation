﻿namespace FluentValidation.Validators {
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	public interface IValidationWorker {
		/// <summary>
		/// Performs validation.
		/// </summary>
		/// <param name="context">Current validation context</param>
		bool Validate(IValidationContext context);

		/// <summary>
		/// Performs validation asynchronously.
		/// </summary>
		/// <param name="context">Current validation context/</param>
		/// <param name="cancellationToken">Cancellation context</param>
		/// <returns></returns>
		Task<bool> ValidateAsync(IValidationContext context, CancellationToken cancellationToken);

		/// <summary>
		/// Determines whether validation should be run asynchronously.
		/// </summary>
		/// <param name="context">Current validation context</param>
		/// <returns>Bool indicating if validation should be run asynchronously.</returns>
		bool ShouldValidateAsync(IValidationContext context);
	}

	//TODO: Review if IValidator should implement IValidationWorker.
	internal class ValidationWorkerWrapper : IValidationWorker {
		private readonly Func<object, IValidator> _validator;

		public ValidationWorkerWrapper(Func<object, IValidator> validator) {
			_validator = validator;
		}

		public bool Validate(IValidationContext context) {
			return _validator(context.Model).Validate(context).IsValid;
		}

		public async Task<bool> ValidateAsync(IValidationContext context, CancellationToken cancellationToken) {
			var result = await _validator(context.Model).ValidateAsync(context, cancellationToken);
			return result.IsValid;
		}

		public bool ShouldValidateAsync(IValidationContext context) {
			return context.IsAsync;
		}
	}
}