using System.Collections.Generic;

namespace EnjoylixSDK.Auth.Models
{
	public class ValidationErrorDetail
	{
		public List<object> loc;
		public string msg;
		public string type;
	}

	public class ValidationErrorResponse
	{
		public List<ValidationErrorDetail> detail;
	}
}