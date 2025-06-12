namespace ElGato_API.VMO.ErrorResponse
{
    public class BasicErrorResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public ErrorCodes ErrorCode { get; set; } = ErrorCodes.None;
    }
}