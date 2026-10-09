using System.Linq;
using System.Net;
using EnjoylixSDK.Auth.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace EnjoylixSDK.Net
{
    internal static class ApiClientHelper
    {
        internal static bool IsTransient(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.ConnectionError)
                return true;

            long code = req.responseCode;
            return code == 0 || code == 408 || code == 429 || code >= 500;
        }

        internal static EnjoylixApiException CreateException(string context, UnityWebRequest req, int attempts = 1)
        {
            string responseBody = req.downloadHandler?.text;
            string errorMessage = $"[{context}] code {req.responseCode}, result {req.result}. Error: {req.error}.";
            ValidationErrorResponse parsedValidation = null;

            if (req.responseCode == 422)
            {
                try
                {
                    parsedValidation = JsonUtility.FromJson<ValidationErrorResponse>(responseBody);
                    var details = parsedValidation?.detail != null
                        ? string.Join(", ", parsedValidation.detail.Select(d => d.msg))
                        : null;
                    if (!string.IsNullOrEmpty(details))
                        errorMessage += $" Details: {details}";
                }
                catch
                {
                    errorMessage += $" Details: Couldn't deserialize validation response: {responseBody}";
                }
            }

            if (IsTransient(req))
                return new EnjoylixNetworkException(errorMessage, (HttpStatusCode)req.responseCode, responseBody, attempts);

            return new EnjoylixApiException(errorMessage, (HttpStatusCode)req.responseCode, responseBody, parsedValidation);
        }
    }
}
