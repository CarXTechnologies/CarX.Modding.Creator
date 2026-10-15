namespace Plugins.CarX.Modding.Creator.Runtime.Publishing
{
	/// <summary>
	/// Outcome of a publisher call. Vendor SDKs report failures in wildly different shapes (Steam returns a
	/// <c>Result</c> enum, mod.io an <c>Error</c> object), so everything is flattened into a flag plus a message that
	/// is safe to show in the editor UI.
	/// </summary>
	public readonly struct ModOperationResult
	{
		public bool Success { get; }

		public string Message { get; }

		private ModOperationResult(bool success, string message)
		{
			Success = success;
			Message = message ?? string.Empty;
		}

		public static ModOperationResult Ok(string message = null)
		{
			return new ModOperationResult(true, message);
		}

		public static ModOperationResult Fail(string message)
		{
			return new ModOperationResult(false, message);
		}

		public override string ToString()
		{
			return Success ? "Success" : $"Failed: {Message}";
		}
	}

	/// <summary>Same as <see cref="ModOperationResult"/> but carries a payload produced by the call.</summary>
	public readonly struct ModOperationResult<T>
	{
		public bool Success { get; }

		public string Message { get; }

		public T Value { get; }

		private ModOperationResult(bool success, string message, T value)
		{
			Success = success;
			Message = message ?? string.Empty;
			Value = value;
		}

		public static ModOperationResult<T> Ok(T value, string message = null)
		{
			return new ModOperationResult<T>(true, message, value);
		}

		public static ModOperationResult<T> Fail(string message)
		{
			return new ModOperationResult<T>(false, message, default);
		}

		public override string ToString()
		{
			return Success ? $"Success: {Value}" : $"Failed: {Message}";
		}
	}
}
