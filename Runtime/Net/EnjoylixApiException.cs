using System;
using System.Net;
using EnjoylixSDK.Auth.Models;

namespace EnjoylixSDK.Net
{
	public class EnjoylixApiException : Exception
	{
		public HttpStatusCode StatusCode { get; }
		public string ResponseBody { get; }

		public ValidationErrorResponse ValidationError { get; }

		public EnjoylixApiException(
			string message,
			HttpStatusCode statusCode,
			string responseBody,
			ValidationErrorResponse validationError = null)
				: base(message)
		{
			StatusCode = statusCode;
			ResponseBody = responseBody;
			ValidationError = validationError;
		}
	}
	
	public sealed class EnjoylixNetworkException : EnjoylixApiException
	{
		public int Attempts { get; }

		public EnjoylixNetworkException(
			string message,
			HttpStatusCode statusCode,
			string responseBody,
			int attempts)
				: base(message, statusCode, responseBody)
		{
			Attempts = attempts;
		}
	}

	public sealed class PopupBlockedException : Exception
	{
		public string Url { get; }

		public PopupBlockedException(string url)
				: base("Popup window was blocked by the browser.")
		{
			Url = url;
		}
	}

	public sealed class UserDismissedException : Exception
	{
		public UserDismissedException()
			: base("User closed the external browser or popup without completing the action.") { }
	}

	public sealed class FlowTimeoutException : Exception
	{
		public string Context { get; }
		public TimeSpan Timeout { get; }

		public FlowTimeoutException(string context, TimeSpan timeout)
			: base($"[{context}] no result within {timeout}.")
		{
			Context = context;
			Timeout = timeout;
		}
	}
}