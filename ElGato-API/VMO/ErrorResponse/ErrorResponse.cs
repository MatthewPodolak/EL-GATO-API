namespace ElGato_API.VMO.ErrorResponse
{
    public class ErrorResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public ErrorCodes ErrorCode { get; set; } = ErrorCodes.None;

        private static string BaseError = "An error occurred: ";
        private static string InternalBaseError = "An internal server error occurred: ";

        public ErrorResponse()
        {
            Success = true;
            ErrorCode = ErrorCodes.None;
            ErrorMessage = null;
        }
        private ErrorResponse(bool success, ErrorCodes code, string? message)
        {
            Success = success;
            ErrorCode = code;
            ErrorMessage = message;
        }

        public static ErrorResponse Ok(string? message = null)
            => new ErrorResponse(true, ErrorCodes.None, message ?? "Sucess.");

        public static ErrorResponse StateNotValid<T>(string? message = null)
        {
            var type = typeof(T).Name;
            var defMsg = $"Model state not valid. Please check {type}.";

            return new ErrorResponse
            (
                success: false,
                code: ErrorCodes.ModelStateNotValid,
                message: BaseError + (message ?? defMsg)
            );
        }       

        public static ErrorResponse NotFound(string? message = null)
            => new ErrorResponse(false, ErrorCodes.NotFound, BaseError + (message ?? "Not found."));

        public static ErrorResponse Internal(string? message = null)
            => new ErrorResponse(false, ErrorCodes.Internal, InternalBaseError + (message ?? ""));

        public static ErrorResponse Failed(string? message = null)
            => new ErrorResponse(false, ErrorCodes.Internal, BaseError + (message ?? "Operation Failed"));

        public static ErrorResponse AlreadyExists(string? message = null)
            => new ErrorResponse(false, ErrorCodes.AlreadyExists, BaseError + (message ?? "Already exists."));

        public static ErrorResponse Forbidden(string? message = null)
            => new ErrorResponse(false, ErrorCodes.Forbidden, BaseError + (message ?? "Operation forbidden."));

        public static ErrorResponse Custom(ErrorCodes code, string message)
            => new ErrorResponse(false, code, BaseError + message);
    }
}